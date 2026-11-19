using System.Numerics;
using AiBehaviorLab.Brains;
using AiBehaviorLab.Math;
using AiBehaviorLab.Rules;

namespace AiBehaviorLab.Simulation;

/// <summary>
/// 보스 1 대 플레이어 1 전투. 고정 틱, 결정적(같은 시드 → 같은 결과).
/// 한 틱의 순서는 docs/design.md "한 틱의 순서"를 따른다.
/// </summary>
public sealed class CombatSimulation
{
    // 용도별 난수 흐름 번호. 바꾸면 기존 시드의 결과가 달라진다.
    private const ulong PlayerEyesStream = 1;
    private const ulong BossEyesStream = 2;
    private const ulong PlayerHandsStream = 3;
    private const ulong BossHandsStream = 4;
    private const ulong PlayerBrainStream = 5;
    private const ulong BossBrainStream = 6;

    private readonly MatchSetup _setup;
    private readonly Combatant _player;
    private readonly Combatant _boss;
    private readonly PerceptionFilter _playerEyes; // 플레이어가 보는 보스
    private readonly PerceptionFilter _bossEyes;   // 보스가 보는 플레이어
    private readonly DeterministicRandom _playerHands;
    private readonly DeterministicRandom _bossHands;
    private readonly List<CombatEvent>? _events;

    public CombatSimulation(CombatRules rules, MatchSetup setup)
    {
        rules.Validate();
        Rules = rules;
        _setup = setup;

        var toBoss = VectorMath.NormalizeOr(rules.BossStart - rules.PlayerStart, Vector2.UnitY);
        _player = new Combatant(Side.Player, rules.Player, rules.PlayerStart, toBoss);
        _boss = new Combatant(Side.Boss, rules.Boss, rules.BossStart, -toBoss);

        ulong seed = setup.Seed;
        _playerEyes = new PerceptionFilter(
            rules.ToTicks(setup.PlayerLimits.ReactionSeconds), setup.PlayerLimits.PositionError,
            new DeterministicRandom(DeterministicRandom.Derive(seed, PlayerEyesStream)));
        _bossEyes = new PerceptionFilter(
            rules.ToTicks(setup.BossLimits.ReactionSeconds), setup.BossLimits.PositionError,
            new DeterministicRandom(DeterministicRandom.Derive(seed, BossEyesStream)));
        _playerHands = new DeterministicRandom(DeterministicRandom.Derive(seed, PlayerHandsStream));
        _bossHands = new DeterministicRandom(DeterministicRandom.Derive(seed, BossHandsStream));
        _events = setup.RecordEvents ? new List<CombatEvent>() : null;

        _playerEyes.Record(_boss.ToView());
        _bossEyes.Record(_player.ToView());

        setup.PlayerBrain.Reset(new BrainContext(Side.Player, rules,
            new DeterministicRandom(DeterministicRandom.Derive(seed, PlayerBrainStream))));
        setup.BossBrain.Reset(new BrainContext(Side.Boss, rules,
            new DeterministicRandom(DeterministicRandom.Derive(seed, BossBrainStream))));
    }

    public CombatRules Rules { get; }

    /// <summary>다음에 처리할 틱 번호(= 지금까지 처리한 틱 수).</summary>
    public int Tick { get; private set; }

    public MatchResult? Result { get; private set; }
    public bool IsOver => Result is not null;

    public CombatantView Player => _player.ToView();
    public CombatantView Boss => _boss.ToView();
    public CombatStats PlayerStatistics => _player.Statistics;
    public CombatStats BossStatistics => _boss.Statistics;

    public IReadOnlyList<CombatEvent> Events => _events ?? (IReadOnlyList<CombatEvent>)Array.Empty<CombatEvent>();

    /// <summary>
    /// 지금 상태의 해시(FNV-1a 64비트). 실수는 비트 그대로 넣으므로 아주 작은 차이도 잡는다.
    /// 리플레이 검증에서 다시 돌린 결과와 비교한다.
    /// </summary>
    public ulong ComputeStateHash()
    {
        var hash = new StateHasher();
        hash.Add(Tick);
        _player.AddTo(ref hash);
        _boss.AddTo(ref hash);
        return hash.Value;
    }

    public MatchResult Run()
    {
        while (!IsOver)
            Step();
        return Result!;
    }

    public void Step()
    {
        if (IsOver)
            throw new InvalidOperationException("이미 끝난 판이다");

        // 1. 같은 시점의 관찰로 양쪽이 의도를 정한다.
        var playerIntent = _setup.PlayerBrain.Decide(
            new Observation(Tick, Rules, _player.ToView(), _playerEyes.Perceive()));
        var bossIntent = _setup.BossBrain.Decide(
            new Observation(Tick, Rules, _boss.ToView(), _bossEyes.Perceive()));

        // 2. 의도 적용(행동 시작, 이동).
        ApplyIntent(_player, playerIntent, _playerHands, _setup.PlayerLimits.MistakeChance);
        ApplyIntent(_boss, bossIntent, _bossHands, _setup.BossLimits.MistakeChance);

        // 3. 판정 국면 처리(이동형 이동, 회복, 무적, 공격 판정 준비).
        ProcessActive(_player);
        ProcessActive(_boss);

        // 4. 판정. 같은 틱의 양쪽 공격은 함께 처리한다(먼저 죽어도 이번 틱 공격은 나간다).
        ResolveStrike(_player, _boss);
        ResolveStrike(_boss, _player);

        // 5. 몸 겹침과 경계, 스태미나(이번 틱에 행동하지 않았을 때만), 국면 시간 진행.
        ResolveBodies();
        RegenStamina(_player);
        RegenStamina(_boss);
        AdvanceTimer(_player, _boss.Position);
        AdvanceTimer(_boss, _player.Position);

        Tick++;

        // 6. 관찰 기록, 승패.
        _playerEyes.Record(_boss.ToView());
        _bossEyes.Record(_player.ToView());
        CheckEnd();
    }

    private void ApplyIntent(Combatant self, Intent intent, DeterministicRandom hands, double mistakeChance)
    {
        // 행동 중에는 어떤 의도도 받지 않는다.
        if (self.Action is not null)
            return;

        switch (intent.Kind)
        {
            case IntentKind.Move:
            {
                var direction = VectorMath.NormalizeOr(intent.Direction, Vector2.Zero);
                if (direction == Vector2.Zero)
                    return;
                self.Position += direction * (self.Stats.MoveSpeed / Rules.TickRate);
                self.Facing = direction;
                return;
            }
            case IntentKind.Action:
                StartAction(self, intent, hands, mistakeChance);
                return;
        }
    }

    private void StartAction(Combatant self, Intent intent, DeterministicRandom hands, double mistakeChance)
    {
        if (!Rules.Actions.TryGetValue(intent.Action, out var definition)
            || definition.User != self.Side
            || self.Stamina < definition.StaminaCost
            || (definition.UsesPotion && self.Potions <= 0))
        {
            self.Statistics.RejectedIntents++;
            return;
        }

        var aim = VectorMath.NormalizeOr(intent.Direction, self.Facing);
        if (mistakeChance > 0 && hands.Chance(mistakeChance))
        {
            aim = VectorMath.Rotate(aim, hands.Range(-MathF.PI / 2, MathF.PI / 2));
            self.Statistics.Mistakes++;
        }

        self.Stamina -= definition.StaminaCost;
        if (definition.UsesPotion)
            self.Potions--;

        self.Action = new RunningAction(definition)
        {
            Phase = ActionPhase.Windup,
            AimOrigin = self.Position,
            AimDirection = aim,
        };
        self.Facing = aim;
        self.Statistics.ActionsStarted++;
        Log(CombatEventKind.ActionStarted, self.Side, definition.Id, 0);

        // 선딜 0인 행동(구르기)은 이번 틱부터 판정 국면이다.
        SkipFinishedPhases(self, Vector2.Zero);
    }

    private void ProcessActive(Combatant self)
    {
        self.IsInvulnerable = false;
        self.IsDashing = false;
        self.StrikeStep = null;

        var action = self.Action;
        if (action is null || action.Phase != ActionPhase.Active)
            return;

        var step = action.Step;
        if (action.PhaseElapsed == 0 && step.Heal > 0)
        {
            int before = self.Hp;
            self.Hp = System.Math.Min(self.Stats.MaxHp, self.Hp + step.Heal);
            int healed = self.Hp - before;
            self.Statistics.HealAmount += healed;
            Log(CombatEventKind.Healed, self.Side, action.Definition.Id, healed);
        }

        if (step.IsMoving)
        {
            self.Position += action.AimDirection * (step.MoveDistance / step.ActiveTicks);
            self.IsDashing = true;
        }

        if (action.PhaseElapsed < step.InvulnerableTicks)
            self.IsInvulnerable = true;

        if (step.Damage > 0 && !action.StepResolved)
            self.StrikeStep = step;
    }

    private void ResolveStrike(Combatant attacker, Combatant defender)
    {
        var step = attacker.StrikeStep;
        var action = attacker.Action;
        if (step is null || action is null)
            return;

        if (!StrikeOverlaps(attacker, action, step, defender))
            return;

        // 한 단계는 한 번만 판정한다. 무적에 막혀도 그 단계는 끝난다.
        action.StepResolved = true;

        if (defender.IsInvulnerable)
        {
            defender.Statistics.Dodges++;
            Log(CombatEventKind.Dodged, attacker.Side, action.Definition.Id, 0);
            return;
        }

        int damage = System.Math.Min(step.Damage, defender.Hp);
        defender.Hp -= damage;
        attacker.Statistics.Hits++;
        attacker.Statistics.DamageDealt += damage;
        Log(CombatEventKind.Hit, attacker.Side, action.Definition.Id, damage);
    }

    /// <summary>
    /// 공격 범위가 상대 몸과 겹치는가. 예고(Telegraph)와 같은 모양을 쓴다.
    /// 이동형(돌진)은 같은 직선 범위 중에서 이번 틱에 몸이 지나간 구간(앞뒤로 폭/2 + 상대 몸)만 판정한다.
    /// 그래서 가만히 있는 상대는 예고 범위 안일 때만, 그리고 반드시 한 번 맞는다.
    /// </summary>
    private static bool StrikeOverlaps(Combatant attacker, RunningAction action, ActionStep step, Combatant defender)
    {
        var shape = step.Shape!;
        float radius = defender.Stats.BodyRadius;
        if (!shape.Hits(action.AimOrigin, action.AimDirection, defender.Position, radius))
            return false;
        if (!step.IsMoving)
            return true;

        float halfWidth = ((LineShape)shape).Width * 0.5f;
        float travelled = Vector2.Dot(attacker.Position - action.AimOrigin, action.AimDirection);
        float previous = travelled - step.MoveDistance / step.ActiveTicks;
        float along = Vector2.Dot(defender.Position - action.AimOrigin, action.AimDirection);
        return along >= previous - halfWidth - radius && along <= travelled + halfWidth + radius;
    }

    /// <summary>
    /// 몸 겹침과 경기장 경계를 함께 맞춘다. 이동형 행동 중이면 겹침은 허용한다.
    /// 플레이어를 먼저 밀어내고, 벽 때문에 다 못 밀면 남은 만큼 보스를 민다.
    /// </summary>
    private void ResolveBodies()
    {
        ClampToArena(_boss);
        ClampToArena(_player);
        if (_player.IsDashing || _boss.IsDashing)
            return;

        float minDistance = _player.Stats.BodyRadius + _boss.Stats.BodyRadius;
        var delta = _player.Position - _boss.Position;
        if (delta.LengthSquared() >= minDistance * minDistance)
            return;

        var away = VectorMath.NormalizeOr(delta, -_boss.Facing);
        _player.Position = _boss.Position + away * minDistance;
        ClampToArena(_player);

        delta = _boss.Position - _player.Position;
        if (delta.LengthSquared() < minDistance * minDistance)
        {
            _boss.Position = _player.Position + VectorMath.NormalizeOr(delta, -away) * minDistance;
            ClampToArena(_boss);
        }
    }

    private void ClampToArena(Combatant self)
    {
        float limit = Rules.ArenaRadius - self.Stats.BodyRadius;
        float distance = self.Position.Length();
        if (distance > limit)
            self.Position *= limit / distance;
    }

    private void AdvanceTimer(Combatant self, Vector2 opponentPosition)
    {
        if (self.Action is null)
            return;
        self.Action.PhaseElapsed++;
        SkipFinishedPhases(self, opponentPosition);
    }

    /// <summary>지금 국면의 시간이 다 됐으면 다음 국면으로 넘긴다. 길이 0인 국면은 건너뛴다.</summary>
    private static void SkipFinishedPhases(Combatant self, Vector2 opponentPosition)
    {
        while (self.Action is { } action && action.PhaseElapsed >= action.PhaseLength)
        {
            action.PhaseElapsed = 0;
            switch (action.Phase)
            {
                case ActionPhase.Windup:
                    action.Phase = ActionPhase.Active;
                    break;

                case ActionPhase.Active when action.StepIndex + 1 < action.Definition.Steps.Count:
                    action.StepIndex++;
                    action.Phase = ActionPhase.Windup;
                    action.StepResolved = false;
                    AimStep(self, action, opponentPosition);
                    break;

                case ActionPhase.Active:
                    action.Phase = ActionPhase.Recovery;
                    break;

                default:
                    self.Action = null;
                    break;
            }
        }
    }

    /// <summary>두 번째 이후 단계의 조준. 선딜 시작 때 고정한다(예고 범위 = 판정 범위).</summary>
    private static void AimStep(Combatant self, RunningAction action, Vector2 opponentPosition)
    {
        action.AimOrigin = self.Position;
        if (action.Step.Aim == AimMode.TowardOpponent)
            action.AimDirection = VectorMath.NormalizeOr(opponentPosition - self.Position, action.AimDirection);
        self.Facing = action.AimDirection;
    }

    private void RegenStamina(Combatant self)
    {
        if (self.Action is not null || self.Stats.StaminaRegenPerSecond <= 0f)
            return;
        self.Stamina = MathF.Min(self.Stats.MaxStamina, self.Stamina + self.Stats.StaminaRegenPerSecond / Rules.TickRate);
    }

    private void CheckEnd()
    {
        // 같은 틱에 둘 다 쓰러지면 보스 승(플레이어는 살아남아야 이긴다).
        if (!_player.IsAlive)
            End(Side.Boss, EndReason.PlayerDefeated);
        else if (!_boss.IsAlive)
            End(Side.Player, EndReason.BossDefeated);
        else if (Tick >= Rules.TimeLimitTicks)
            End(Side.Boss, EndReason.Timeout);
    }

    private void End(Side winner, EndReason reason)
    {
        Log(CombatEventKind.MatchEnded, winner, null, (int)reason);
        Result = new MatchResult(
            winner, reason, Tick, Rules.ToSeconds(Tick), _player.Hp, _boss.Hp,
            _player.Statistics, _boss.Statistics, Events);
    }

    private void Log(CombatEventKind kind, Side source, ActionId? action, int value)
    {
        _events?.Add(new CombatEvent(Tick, kind, source, action, value));
    }
}
