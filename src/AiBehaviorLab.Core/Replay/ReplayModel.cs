using System.Text.Json;
using System.Text.Json.Serialization;
using AiBehaviorLab.Brains;
using AiBehaviorLab.Rules;
using AiBehaviorLab.Simulation;

namespace AiBehaviorLab.Replay;

/// <summary>
/// 상태 기록 리플레이. 재생은 기록된 프레임을 그리기만 하므로 결정성이 필요 없다.
/// 프레임마다 상태 해시를 함께 저장해서, 다시 시뮬레이션했을 때 어디서 갈라지는지 확인할 수 있다.
/// </summary>
public sealed record ReplayFile(ReplayHeader Header, IReadOnlyList<ReplayFrame> Frames)
{
    /// <summary>2: 프레임에 판단 근거(PlayerDecision, BossDecision) 추가.</summary>
    public const int CurrentFormatVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static ReplayFile FromJson(string json)
    {
        var file = JsonSerializer.Deserialize<ReplayFile>(json, JsonOptions)
                   ?? throw new InvalidDataException("리플레이 파일이 비어 있다");
        file.Validate();
        return file;
    }

    /// <summary>
    /// 재생과 검증의 전제를 확인한다: 형식 버전, 빠진 값 없음, 프레임 i의 틱 = i, 마지막 틱 = 머리말의 틱 수.
    /// 어기면 InvalidDataException.
    /// </summary>
    public void Validate()
    {
        if (Header is null || Header.Spec is null)
            throw new InvalidDataException("리플레이 머리말이 없다");
        if (Header.FormatVersion != CurrentFormatVersion)
            throw new InvalidDataException(
                $"리플레이 형식 버전이 다르다: 파일 {Header.FormatVersion}, 지원 {CurrentFormatVersion}");
        if (Frames is null || Frames.Count == 0)
            throw new InvalidDataException("리플레이에 프레임이 없다");

        for (int i = 0; i < Frames.Count; i++)
        {
            var frame = Frames[i];
            if (frame is null || frame.Player is null || frame.Boss is null || frame.Events is null || frame.Hash is null)
                throw new InvalidDataException($"프레임 {i}에 빠진 값이 있다");
            if (frame.Tick != i)
                throw new InvalidDataException($"프레임 {i}의 틱이 {frame.Tick}이다(틱이 빠졌거나 순서가 다르다)");
        }

        if (Frames[^1].Tick != Header.Ticks)
            throw new InvalidDataException($"마지막 프레임 틱 {Frames[^1].Tick}이 머리말의 틱 수 {Header.Ticks}와 다르다");
    }

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, ToJson());
    }

    public static ReplayFile Load(string path) => FromJson(File.ReadAllText(path));
}

/// <summary>한 판을 다시 만들 수 있는 설정. 규칙과 Brain은 이름으로 찾는다(ReplayCatalog).</summary>
public sealed record MatchSpec(
    string RulesName,
    ulong Seed,
    string PlayerBrain,
    string BossBrain,
    HumanLimits PlayerLimits,
    HumanLimits BossLimits);

public sealed record ReplayHeader(
    int FormatVersion,
    MatchSpec Spec,
    int TickRate,
    float ArenaRadius,
    Side Winner,
    EndReason Reason,
    int Ticks);

/// <summary>
/// 틱 하나가 끝난 뒤의 상태. Tick 0은 시작 상태다. Events는 이 틱에 일어난 사건.
/// 판단 근거는 이 프레임으로 오는 틱(Tick - 1)에 Brain이 낸 것이다. 설명하지 않는 Brain이면 null.
/// </summary>
public sealed record ReplayFrame(
    int Tick,
    BodyFrame Player,
    BodyFrame Boss,
    IReadOnlyList<EventFrame> Events,
    string Hash,
    DecisionTrace? PlayerDecision = null,
    DecisionTrace? BossDecision = null);

public sealed record BodyFrame(
    float X,
    float Y,
    float FacingX,
    float FacingY,
    float Radius,
    int Hp,
    int MaxHp,
    float Stamina,
    int Potions,
    bool Invulnerable,
    ActionFrame? Action,
    ShapeFrame? Shape);

public sealed record ActionFrame(ActionId Id, int Step, ActionPhase Phase, int Elapsed, int Length);

public enum ShapeKind
{
    Circle,
    Sector,
    Line,
    Ring,
}

/// <summary>
/// 지금 보이는 공격 범위(예고 또는 판정). A, B의 뜻은 모양마다 다르다.
/// Circle: 앞쪽 거리, 반지름 / Sector: 반지름, 전체 각도(도) / Line: 길이, 폭 / Ring: 안쪽 반지름, 바깥 반지름.
/// </summary>
public sealed record ShapeFrame(
    ShapeKind Kind,
    float OriginX,
    float OriginY,
    float DirX,
    float DirY,
    float A,
    float B,
    bool Active);

public sealed record EventFrame(CombatEventKind Kind, Side Source, ActionId? Action, int Value);
