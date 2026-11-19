using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiBehaviorLab.Brains;
using AiBehaviorLab.Replay;
using AiBehaviorLab.Rules;
using AiBehaviorLab.Simulation;
using Godot;
using Side = AiBehaviorLab.Rules.Side;

namespace AiBehaviorLab.Viewer;

/// <summary>
/// 상태 기록 리플레이 관전 화면. 기록된 프레임을 도형으로 그리기만 한다.
/// 명령줄(-- 뒤): --replay &lt;파일&gt; | --seed &lt;n&gt; --limits none|casual|expert, --frame &lt;틱&gt;, --verify 1, --screenshot &lt;png&gt;
/// 키: Space 재생/정지, ←/→ 한 틱, Home 처음
/// </summary>
public partial class ReplayViewer : Node2D
{
    private static readonly float[] Speeds = [0.1f, 0.25f, 0.5f, 1f, 2f, 4f];
    private const int DefaultSpeedIndex = 2;
    private const int FloatingTextTicks = 24;

    private static readonly Vector2 ArenaCenter = new(360f, 340f);
    private const float ArenaPixels = 320f;

    private static readonly Color ArenaFill = new(0.13f, 0.14f, 0.17f);
    private static readonly Color ArenaLine = new(0.45f, 0.47f, 0.52f);
    private static readonly Color BossColor = new(0.86f, 0.33f, 0.3f);
    private static readonly Color PlayerColor = new(0.33f, 0.58f, 0.96f);
    private static readonly Color Windup = new(1f, 0.83f, 0.2f);
    private static readonly Color Strike = new(1f, 0.22f, 0.18f);

    private ReplayFile? _replay;
    private string _source = "";
    private int _frame;
    private bool _playing;
    private float _tickAccumulator;
    private int _speedIndex = DefaultSpeedIndex;

    private LineEdit _seedBox = null!;
    private OptionButton _limitsBox = null!;
    private OptionButton _playerBrainBox = null!;
    private OptionButton _bossBrainBox = null!;
    private OptionButton _speedBox = null!;
    private Button _playButton = null!;
    private HSlider _timeline = null!;
    private Label _timeLabel = null!;
    private Label _verifyLabel = null!;
    private RichTextLabel _panel = null!;
    private FileDialog _fileDialog = null!;

    private string? _screenshotPath;
    private int _screenshotWait = -1;

    private float PixelsPerMeter => ArenaPixels / (_replay?.Header.ArenaRadius ?? 10f);

    public override void _Ready()
    {
        BuildUi();

        var args = ParseArgs(OS.GetCmdlineUserArgs());
        try
        {
            if (args.TryGetValue("--replay", out var path))
            {
                LoadFile(path);
            }
            else
            {
                string limits = args.GetValueOrDefault("--limits", "casual");
                _seedBox.Text = args.GetValueOrDefault("--seed", "7");
                _limitsBox.Selected = limits switch { "none" => 0, "expert" => 2, _ => 1 };
                _playerBrainBox.Selected = System.Array.IndexOf(BrainKinds, args.GetValueOrDefault("--player", "fsm")) is var p and >= 0 ? p : 1;
                _bossBrainBox.Selected = System.Array.IndexOf(BrainKinds, args.GetValueOrDefault("--boss", "fsm")) is var b and >= 0 ? b : 1;
                Generate();
            }
        }
        catch (System.Exception e)
        {
            ShowError($"불러오기 실패: {e.Message}");
        }

        if (args.TryGetValue("--frame", out var frameText) && int.TryParse(frameText, out int frame))
            SetFrame(frame);
        if (args.GetValueOrDefault("--verify") == "1")
            Verify();
        if (args.TryGetValue("--screenshot", out var shot))
        {
            _screenshotPath = shot;
            _screenshotWait = 3;
        }
    }

    public override void _Process(double delta)
    {
        if (_screenshotWait >= 0 && --_screenshotWait < 0)
        {
            GetViewport().GetTexture().GetImage().SavePng(_screenshotPath!);
            GetTree().Quit();
            return;
        }

        if (!_playing || _replay is null)
            return;

        _tickAccumulator += (float)delta * Speeds[_speedIndex] * _replay.Header.TickRate;
        int advance = (int)_tickAccumulator;
        if (advance <= 0)
            return;
        _tickAccumulator -= advance;

        int last = _replay.Frames.Count - 1;
        SetFrame(_frame + advance);
        if (_frame >= last)
            SetPlaying(false);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true } key)
            return;

        switch (key.Keycode)
        {
            case Key.Space:
                SetPlaying(!_playing);
                break;
            case Key.Left:
                SetPlaying(false);
                SetFrame(_frame - 1);
                break;
            case Key.Right:
                SetPlaying(false);
                SetFrame(_frame + 1);
                break;
            case Key.Home:
                SetFrame(0);
                break;
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }

    // ───────────── 리플레이 ─────────────

    private void Generate()
    {
        // 시드는 러너와 같은 ulong 전체 범위. 위젯 범위로 조용히 바뀌지 않게 글자로 받는다.
        if (!ulong.TryParse(_seedBox.Text.Trim(), out ulong seed))
        {
            ShowError($"시드는 0~{ulong.MaxValue} 사이 정수: \"{_seedBox.Text}\"");
            return;
        }

        var limits = _limitsBox.Selected switch
        {
            0 => HumanLimits.None,
            2 => HumanLimits.Expert,
            _ => HumanLimits.Casual,
        };
        var spec = new MatchSpec(ReplayCatalog.RulesV0, seed,
            ReplayCatalog.PlayerBrainByKind(BrainKinds[_playerBrainBox.Selected])!,
            ReplayCatalog.BossBrainByKind(BrainKinds[_bossBrainBox.Selected])!,
            limits, HumanLimits.None);
        SetReplay(ReplayRecorder.Record(spec), "이 화면에서 생성");
    }

    /// <summary>읽기와 검사가 모두 끝난 뒤에만 지금 판을 바꾼다. 실패하면 보던 판과 위치가 그대로다.</summary>
    private void LoadFile(string path)
    {
        ReplayFile replay;
        try
        {
            replay = ReplayFile.Load(path);
        }
        catch (System.Exception e)
        {
            ShowError($"불러오기 실패: {e.Message}");
            return;
        }
        SetReplay(replay, $"파일 {System.IO.Path.GetFileName(path)}");
    }

    private void ShowError(string message)
    {
        _verifyLabel.Text = message;
        _verifyLabel.Modulate = new Color(1f, 0.5f, 0.45f);
    }

    private void SetReplay(ReplayFile replay, string source)
    {
        replay.Validate();
        _replay = replay;
        _verifyLabel.Modulate = Colors.White;
        _source = source;
        _verifyLabel.Text = "";
        _timeline.MaxValue = replay.Frames.Count - 1;
        SetPlaying(false);
        SetFrame(0);
    }

    private void Verify()
    {
        if (_replay is null)
            return;
        var result = ReplayVerifier.Verify(_replay);
        _verifyLabel.Text = result switch
        {
            { Matches: true } => $"검증: 일치 (프레임 {result.FramesChecked}개, 이 런타임에서 다시 돌림)",
            { Problem: { } problem } => $"검증: 잘못된 리플레이 ({problem})",
            _ => $"검증: 틱 {result.FirstMismatchTick}에서 갈라짐",
        };
        _verifyLabel.Modulate = result.Matches ? new Color(0.6f, 0.95f, 0.6f) : new Color(1f, 0.5f, 0.45f);
    }

    private void SetFrame(int frame)
    {
        if (_replay is null)
            return;
        _frame = Mathf.Clamp(frame, 0, _replay.Frames.Count - 1);
        _timeline.SetValueNoSignal(_frame);
        UpdateTexts();
        QueueRedraw();
    }

    private void SetPlaying(bool playing)
    {
        if (playing && _replay is not null && _frame >= _replay.Frames.Count - 1)
            SetFrame(0);
        _playing = playing;
        _tickAccumulator = 0f;
        _playButton.Text = playing ? "❚❚ 정지" : "▶ 재생";
    }

    // ───────────── 그리기 ─────────────

    public override void _Draw()
    {
        if (_replay is null)
            return;

        var frame = _replay.Frames[_frame];
        float arena = _replay.Header.ArenaRadius * PixelsPerMeter;
        DrawCircle(ArenaCenter, arena, ArenaFill);
        DrawArc(ArenaCenter, arena, 0f, Mathf.Tau, 128, ArenaLine, 2f, true);

        // 범위를 몸보다 먼저 그린다.
        DrawShape(frame.Boss.Shape);
        DrawShape(frame.Player.Shape);

        DrawBody(frame.Boss, BossColor, frame.Boss.Action is { } b ? ActionText(b) : "");
        DrawBody(frame.Player, PlayerColor, frame.Player.Action is { } p ? ActionText(p) : "");

        DrawFloatingTexts();
    }

    private Vector2 ToScreen(float x, float y) => new(ArenaCenter.X + x * PixelsPerMeter, ArenaCenter.Y - y * PixelsPerMeter);

    private void DrawShape(ShapeFrame? shape)
    {
        if (shape is null)
            return;

        var color = shape.Active ? Strike : Windup;
        var fill = new Color(color, shape.Active ? 0.55f : 0.22f);
        var line = new Color(color, 0.9f);

        float angle = Mathf.Atan2(shape.DirY, shape.DirX);
        var origin = new Vector2(shape.OriginX, shape.OriginY);
        var dir = new Vector2(shape.DirX, shape.DirY);

        switch (shape.Kind)
        {
            case ShapeKind.Circle:
                Polygon(Arc(origin + dir * shape.A, shape.B, 0f, Mathf.Tau, 64), fill, line, closed: true);
                break;

            case ShapeKind.Sector:
            {
                float half = Mathf.DegToRad(shape.B) * 0.5f;
                var points = new List<Vector2> { origin };
                points.AddRange(Arc(origin, shape.A, angle - half, angle + half, 32));
                Polygon(points, fill, line, closed: true);
                break;
            }

            case ShapeKind.Line:
            {
                var side = new Vector2(-dir.Y, dir.X) * (shape.B * 0.5f);
                var end = origin + dir * shape.A;
                Polygon([origin + side, end + side, end - side, origin - side], fill, line, closed: true);
                break;
            }

            case ShapeKind.Ring:
            {
                var outer = Arc(origin, shape.B, 0f, Mathf.Tau, 64);
                var inner = Arc(origin, shape.A, 0f, Mathf.Tau, 64);
                for (int i = 0; i < 64; i++)
                {
                    int j = (i + 1) % 64;
                    DrawColoredPolygon([ToScreen(outer[i]), ToScreen(outer[j]), ToScreen(inner[j]), ToScreen(inner[i])], fill);
                }
                Polygon(outer, null, line, closed: true);
                Polygon(inner, null, line, closed: true);
                break;
            }
        }
    }

    private static List<Vector2> Arc(Vector2 center, float radius, float from, float to, int segments)
    {
        var points = new List<Vector2>(segments + 1);
        bool full = Mathf.IsEqualApprox(to - from, Mathf.Tau);
        int count = full ? segments : segments + 1;
        for (int i = 0; i < count; i++)
        {
            float t = from + (to - from) * i / segments;
            points.Add(center + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * radius);
        }
        return points;
    }

    private Vector2 ToScreen(Vector2 world) => ToScreen(world.X, world.Y);

    private void Polygon(List<Vector2> world, Color? fill, Color line, bool closed)
    {
        var screen = world.Select(ToScreen).ToArray();
        if (fill is { } f)
            DrawColoredPolygon(screen, f);
        var outline = closed ? screen.Append(screen[0]).ToArray() : screen;
        DrawPolyline(outline, line, 2f, true);
    }

    private void DrawBody(BodyFrame body, Color color, string label)
    {
        var center = ToScreen(body.X, body.Y);
        float radius = body.Radius * PixelsPerMeter;

        DrawCircle(center, radius, color);
        if (body.Invulnerable)
            DrawArc(center, radius + 4f, 0f, Mathf.Tau, 48, Colors.White, 3f, true);

        // 바라보는 방향 삼각형
        var facing = new Vector2(body.FacingX, -body.FacingY).Normalized();
        var side = new Vector2(-facing.Y, facing.X);
        var tip = center + facing * (radius + 12f);
        var baseCenter = center + facing * (radius + 2f);
        DrawColoredPolygon([tip, baseCenter + side * 7f, baseCenter - side * 7f], Colors.White);

        // 머리 위: HP 막대와 행동
        const float barWidth = 64f;
        var barPosition = center + new Vector2(-barWidth / 2f, -radius - 22f);
        DrawRect(new Rect2(barPosition, new Vector2(barWidth, 6f)), new Color(0f, 0f, 0f, 0.6f));
        DrawRect(new Rect2(barPosition, new Vector2(barWidth * body.Hp / body.MaxHp, 6f)), new Color(0.35f, 0.9f, 0.4f));

        var font = ThemeDB.FallbackFont;
        string head = $"{body.Hp}/{body.MaxHp}" + (label.Length > 0 ? $"  {label}" : "");
        DrawString(font, center + new Vector2(-120f, -radius - 28f), head, HorizontalAlignment.Center, 240f, 14,
            Colors.White);
    }

    private void DrawFloatingTexts()
    {
        var font = ThemeDB.FallbackFont;
        var frames = _replay!.Frames;
        var now = frames[_frame];

        for (int k = System.Math.Max(0, _frame - FloatingTextTicks); k <= _frame; k++)
        {
            foreach (var e in frames[k].Events)
            {
                (BodyFrame target, string text, Color color) = e.Kind switch
                {
                    CombatEventKind.Hit => (e.Source == Side.Boss ? now.Player : now.Boss, $"-{e.Value}", Strike),
                    CombatEventKind.Dodged => (now.Player, "회피", new Color(0.7f, 0.95f, 1f)),
                    CombatEventKind.Healed => (now.Player, $"+{e.Value}", new Color(0.45f, 1f, 0.5f)),
                    _ => (null!, "", Colors.White),
                };
                if (target is null)
                    continue;

                int age = _frame - k;
                float alpha = 1f - (float)age / FloatingTextTicks;
                var position = ToScreen(target.X, target.Y) + new Vector2(-60f, -target.Radius * PixelsPerMeter - 44f - age * 1.5f);
                DrawString(font, position, text, HorizontalAlignment.Center, 120f, 20, new Color(color, alpha));
            }
        }
    }

    private static string ActionText(ActionFrame action) =>
        $"{Names.Of(action.Id)} · {Names.Of(action.Phase)} {action.Length - action.Elapsed}";

    // ───────────── 글자 패널 ─────────────

    private void UpdateTexts()
    {
        if (_replay is null)
            return;

        var header = _replay.Header;
        var frame = _replay.Frames[_frame];
        double seconds = (double)frame.Tick / header.TickRate;
        _timeLabel.Text = $"{seconds,5:F1}s / {(double)header.Ticks / header.TickRate:F1}s  (틱 {frame.Tick})";

        var spec = header.Spec;
        var text = new StringBuilder();
        text.AppendLine($"[b]시드 {spec.Seed}[/b] · {Names.Limits(spec.PlayerLimits)} · {_source}");
        text.AppendLine($"{Names.Brain(spec.PlayerBrain)} vs {Names.Brain(spec.BossBrain)}");
        text.AppendLine($"결과: [b]{Names.Of(header.Winner)} 승[/b] ({Names.Of(header.Reason)})");
        text.AppendLine();
        AppendBody(text, "보스", "#e05a4d", frame.Boss, showStamina: false);
        AppendBody(text, "플레이어", "#5594f5", frame.Player, showStamina: true);
        text.AppendLine();
        text.AppendLine("[b]판단 근거[/b]");
        AppendDecision(text, "보스", "#e05a4d", frame.BossDecision, spec.BossBrain);
        AppendDecision(text, "플레이어", "#5594f5", frame.PlayerDecision, spec.PlayerBrain);
        text.AppendLine();
        text.AppendLine("[b]최근 사건[/b]");
        foreach (var line in RecentEvents(5))
            text.AppendLine("  " + line);

        _panel.Text = text.ToString();
    }

    private static void AppendDecision(StringBuilder text, string name, string color, DecisionTrace? decision, string brain)
    {
        if (decision is null)
        {
            text.AppendLine($"[color={color}]{name}[/color] [color=#8a8f98]{Names.Brain(brain)}: 판단 근거를 내지 않음[/color]");
            return;
        }

        text.AppendLine($"[color={color}]{name}[/color] [b]{decision.State}[/b] [color=#8a8f98]{decision.TicksInState}틱째[/color]");
        if (decision.LastTransition is { } transition)
            text.AppendLine($"  [color=#f0c74a]↳ {transition}[/color]");
        foreach (var note in decision.Notes)
            text.AppendLine($"  [color=#aab0ba]· {note}[/color]");
    }

    private static void AppendBody(StringBuilder text, string name, string color, BodyFrame body, bool showStamina)
    {
        string extra = showStamina ? $" · 스태미나 {body.Stamina:F0} · 회복약 {body.Potions}" : "";
        text.AppendLine($"[color={color}][b]{name}[/b][/color]  HP {body.Hp}/{body.MaxHp}{extra}");
        string action = body.Action is { } a
            ? $"{Names.Of(a.Id)} · {Names.Of(a.Phase)} {a.Length - a.Elapsed}틱 남음" + (a.Step > 0 ? $" · {a.Step + 1}단계" : "")
            : "대기";
        text.AppendLine($"  {action}{(body.Invulnerable ? " · [color=#ffffff]무적[/color]" : "")}");
    }

    private IEnumerable<string> RecentEvents(int count)
    {
        var lines = new List<string>();
        int tickRate = _replay!.Header.TickRate;
        for (int k = _frame; k >= 0 && lines.Count < count; k--)
        {
            var frame = _replay.Frames[k];
            for (int i = frame.Events.Count - 1; i >= 0 && lines.Count < count; i--)
            {
                var e = frame.Events[i];
                string what = e.Kind switch
                {
                    CombatEventKind.ActionStarted => $"{Names.Of(e.Source)} {Names.Of(e.Action!.Value)}",
                    CombatEventKind.Hit => $"[color=#ff6b5e]{Names.Of(e.Source)} {Names.Of(e.Action!.Value)} 명중 -{e.Value}[/color]",
                    CombatEventKind.Dodged => $"[color=#b3f0ff]{Names.Of(e.Action!.Value)} 회피[/color]",
                    CombatEventKind.Healed => $"[color=#73ff80]회복 +{e.Value}[/color]",
                    _ => $"[b]끝: {Names.Of(e.Source)} 승[/b]",
                };
                lines.Add($"{(double)frame.Tick / tickRate,5:F1}s  {what}");
            }
        }
        return lines;
    }

    // ───────────── UI 구성 ─────────────

    private void BuildUi()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        // 오른쪽 위: 판 고르기
        var top = new HBoxContainer { Position = new Vector2(730f, 12f) };
        top.AddThemeConstantOverride("separation", 6);
        layer.AddChild(top);

        top.AddChild(new Label { Text = "시드" });
        _seedBox = new LineEdit { Text = "7", CustomMinimumSize = new Vector2(130f, 0f) };
        _seedBox.TextSubmitted += _ => Generate();
        top.AddChild(_seedBox);

        _limitsBox = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        _limitsBox.AddItem("한계 없음");
        _limitsBox.AddItem("일반 유저");
        _limitsBox.AddItem("고인물");
        _limitsBox.Selected = 1;
        top.AddChild(_limitsBox);

        // 둘째 줄: Brain 고르기와 버튼
        var second = new HBoxContainer { Position = new Vector2(730f, 46f) };
        second.AddThemeConstantOverride("separation", 6);
        layer.AddChild(second);

        second.AddChild(new Label { Text = "플레이어" });
        _playerBrainBox = MakeBrainBox();
        second.AddChild(_playerBrainBox);
        second.AddChild(new Label { Text = "보스" });
        _bossBrainBox = MakeBrainBox();
        second.AddChild(_bossBrainBox);

        second.AddChild(MakeButton("새 판", Generate));
        second.AddChild(MakeButton("파일 열기", () => _fileDialog.PopupCentered(new Vector2I(800, 500))));
        second.AddChild(MakeButton("검증", Verify));

        _verifyLabel = new Label { Position = new Vector2(732f, 82f) };
        layer.AddChild(_verifyLabel);

        // 오른쪽: 상태 패널
        var panelBox = new PanelContainer { Position = new Vector2(730f, 108f), CustomMinimumSize = new Vector2(530f, 538f) };
        layer.AddChild(panelBox);
        _panel = new RichTextLabel
        {
            BbcodeEnabled = true,
            ScrollActive = false,
            CustomMinimumSize = new Vector2(510f, 518f),
        };
        _panel.AddThemeFontSizeOverride("normal_font_size", 14);
        _panel.AddThemeFontSizeOverride("bold_font_size", 14);
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 10);
        margin.AddChild(_panel);
        panelBox.AddChild(margin);

        // 아래: 재생 막대
        var bar = new HBoxContainer { Position = new Vector2(12f, 678f), CustomMinimumSize = new Vector2(1256f, 32f) };
        bar.AddThemeConstantOverride("separation", 6);
        layer.AddChild(bar);

        bar.AddChild(MakeButton("⏮", () => SetFrame(0)));
        bar.AddChild(MakeButton("◀ 1틱", () => { SetPlaying(false); SetFrame(_frame - 1); }));
        _playButton = MakeButton("▶ 재생", () => SetPlaying(!_playing));
        _playButton.CustomMinimumSize = new Vector2(84f, 0f);
        bar.AddChild(_playButton);
        bar.AddChild(MakeButton("1틱 ▶", () => { SetPlaying(false); SetFrame(_frame + 1); }));

        _speedBox = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (float speed in Speeds)
            _speedBox.AddItem($"{speed}배");
        _speedBox.Selected = _speedIndex;
        _speedBox.ItemSelected += index => _speedIndex = (int)index;
        bar.AddChild(_speedBox);

        _timeline = new HSlider
        {
            MinValue = 0,
            Step = 1,
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        _timeline.ValueChanged += value => SetFrame((int)value);
        bar.AddChild(_timeline);

        _timeLabel = new Label { CustomMinimumSize = new Vector2(220f, 0f) };
        bar.AddChild(_timeLabel);

        var help = new Label
        {
            Text = "Space 재생/정지 · ←/→ 한 틱 · Home 처음 · 노랑 = 예고, 빨강 = 판정, 흰 테두리 = 무적",
            Position = new Vector2(14f, 652f),
            Modulate = new Color(1f, 1f, 1f, 0.6f),
        };
        layer.AddChild(help);

        _fileDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            Filters = ["*.json ; 리플레이"],
            UseNativeDialog = true,
        };
        _fileDialog.FileSelected += LoadFile;
        layer.AddChild(_fileDialog);
    }

    private static readonly string[] BrainKinds = ["baseline", "fsm"];

    /// <summary>Brain 종류 선택. 순서는 BrainKinds와 같다. 기본은 FSM.</summary>
    private static OptionButton MakeBrainBox()
    {
        var box = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        box.AddItem("기준선");
        box.AddItem("FSM");
        box.Selected = 1;
        return box;
    }

    private static Button MakeButton(string text, System.Action onPressed)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        button.Pressed += onPressed;
        return button;
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var result = new Dictionary<string, string>();
        for (int i = 0; i + 1 < args.Length; i += 2)
            result[args[i]] = args[i + 1];
        return result;
    }
}
