using System.Text.Json;
using System.Text.Json.Serialization;
using AiBehaviorLab.Rules;
using AiBehaviorLab.Simulation;

namespace AiBehaviorLab.Replay;

/// <summary>
/// 상태 기록 리플레이. 재생은 기록된 프레임을 그리기만 하므로 결정성이 필요 없다.
/// 프레임마다 상태 해시를 함께 저장해서, 다시 시뮬레이션했을 때 어디서 갈라지는지 확인할 수 있다.
/// </summary>
public sealed record ReplayFile(ReplayHeader Header, IReadOnlyList<ReplayFrame> Frames)
{
    public const int CurrentFormatVersion = 1;

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
        if (file.Header.FormatVersion != CurrentFormatVersion)
            throw new InvalidDataException(
                $"리플레이 형식 버전이 다르다: 파일 {file.Header.FormatVersion}, 지원 {CurrentFormatVersion}");
        return file;
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

/// <summary>틱 하나가 끝난 뒤의 상태. Tick 0은 시작 상태다. Events는 이 틱에 일어난 사건.</summary>
public sealed record ReplayFrame(
    int Tick,
    BodyFrame Player,
    BodyFrame Boss,
    IReadOnlyList<EventFrame> Events,
    string Hash);

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
