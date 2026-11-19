using AiBehaviorLab.Replay;
using AiBehaviorLab.Simulation;

namespace AiBehaviorLab.Tests;

public class ReplayTests
{
    private static MatchSpec Spec(ulong seed) => new(
        ReplayCatalog.RulesV0, seed, ReplayCatalog.BaselinePlayer, ReplayCatalog.BaselineBoss,
        HumanLimits.Casual, HumanLimits.None);

    [Fact]
    public void Record_HasOneFramePerTickPlusStart()
    {
        var replay = ReplayRecorder.Record(Spec(7));

        Assert.Equal(replay.Header.Ticks + 1, replay.Frames.Count);
        Assert.Equal(0, replay.Frames[0].Tick);
        Assert.Equal(replay.Header.Ticks, replay.Frames[^1].Tick);
        Assert.Contains(replay.Frames, f => f.Boss.Shape is { Active: false }); // 예고
        Assert.Contains(replay.Frames, f => f.Boss.Shape is { Active: true });  // 판정
        Assert.Contains(replay.Frames[^1].Events, e => e.Kind == CombatEventKind.MatchEnded);
    }

    [Fact]
    public void Record_MatchesPlainSimulationResult()
    {
        var spec = Spec(7);
        var replay = ReplayRecorder.Record(spec);
        var result = ReplayRecorder.CreateSimulation(spec, ReplayCatalog.CreateRules(spec.RulesName)).Run();

        Assert.Equal(result.Ticks, replay.Header.Ticks);
        Assert.Equal(result.PlayerHp, replay.Frames[^1].Player.Hp);
        Assert.Equal(result.BossHp, replay.Frames[^1].Boss.Hp);
    }

    [Fact]
    public void Json_RoundTripThenVerifies()
    {
        var replay = ReplayRecorder.Record(Spec(3));
        var loaded = ReplayFile.FromJson(replay.ToJson());

        Assert.Equal(replay.Header, loaded.Header);
        Assert.Equal(replay.Frames.Count, loaded.Frames.Count);
        Assert.Equal(replay.Frames[100].Hash, loaded.Frames[100].Hash);
        Assert.Equal(replay.Frames[100].Boss, loaded.Frames[100].Boss);

        var verification = ReplayVerifier.Verify(loaded);
        Assert.True(verification.Matches);
        Assert.Equal(replay.Frames.Count, verification.FramesChecked);
    }

    [Fact]
    public void Verify_FindsTamperedFrame()
    {
        var replay = ReplayRecorder.Record(Spec(3));
        var frames = replay.Frames.ToList();
        frames[50] = frames[50] with { Hash = "0000000000000000" };

        var verification = ReplayVerifier.Verify(replay with { Frames = frames });
        Assert.False(verification.Matches);
        Assert.Equal(50, verification.FirstMismatchTick);
    }

    [Fact]
    public void Verify_DetectsDifferentSeed()
    {
        var replay = ReplayRecorder.Record(Spec(3));
        var other = replay with { Header = replay.Header with { Spec = Spec(4) } };

        Assert.False(ReplayVerifier.Verify(other).Matches);
    }

    [Fact]
    public void MissingMiddleFrame_IsRejected() // Codex 리뷰 R1
    {
        var replay = ReplayRecorder.Record(Spec(3));
        var frames = replay.Frames.ToList();
        frames.RemoveAt(50);
        var broken = replay with { Frames = frames };

        var verification = ReplayVerifier.Verify(broken);
        Assert.False(verification.Matches);
        Assert.NotNull(verification.Problem);
        Assert.Throws<InvalidDataException>(() => ReplayFile.FromJson(broken.ToJson()));
    }

    [Fact]
    public void OnlyLastFrame_IsRejected() // Codex 리뷰 R1: 마지막 프레임만 남겨도 통과하면 안 된다
    {
        var replay = ReplayRecorder.Record(Spec(3));
        var broken = replay with { Frames = [replay.Frames[^1]] };

        Assert.False(ReplayVerifier.Verify(broken).Matches);
    }

    [Fact]
    public void FromJson_RejectsMissingValues()
    {
        var replay = ReplayRecorder.Record(Spec(3));
        string json = replay.ToJson().Replace("\"Frames\":[", "\"Frames\":[null,");

        Assert.Throws<InvalidDataException>(() => ReplayFile.FromJson(json));
    }

    [Fact]
    public void FromJson_RejectsOtherFormatVersion()
    {
        var replay = ReplayRecorder.Record(Spec(3));
        var old = replay with { Header = replay.Header with { FormatVersion = 999 } };

        Assert.Throws<InvalidDataException>(() => ReplayFile.FromJson(old.ToJson()));
    }
}
