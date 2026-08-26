using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The shuttle-door replay's elapsed-time projection (extracted from
/// TrapVisualReplay): the late joiner's door lands at the CURRENT state —
/// progress = elapsed exactly (the doors sit where the host's are), the sound
/// latch is past 2 s, the talk latch past 4 s. The door's own Update drives
/// the same thresholds live (ShuttleStartOpen.cs), so the replay and the live
/// animation can never disagree.
/// </summary>
public class ShuttleDoorReplayStateTests
{
	[Fact]
	public void Progress_IsTheElapsedExactly()
	{
		var state = ShuttleDoorReplayState.FromElapsed(7.5f);
		Assert.True(state.Progress == 7.5f, $"the progress must equal the elapsed, got {state.Progress}");
	}

	[Theory]
	[InlineData(0f, false, false)]
	[InlineData(1.9f, false, false)]
	[InlineData(2f, false, false)] // the live door fires the sound PAST 2 — the threshold is strict
	[InlineData(2.1f, true, false)]
	[InlineData(4f, true, false)] // the talk fires PAST 4 — strict
	[InlineData(4.1f, true, true)]
	[InlineData(10f, true, true)] // past self-destroy on the host — the replayed door lands fully open
	[InlineData(100f, true, true)]
	public void SoundAndTalkLatches_FireAtTheLiveThresholds(float elapsed, bool expectedSound, bool expectedTalk)
	{
		var state = ShuttleDoorReplayState.FromElapsed(elapsed);
		Assert.True(state.PlayedSound == expectedSound, $"elapsed {elapsed}: sound latch must be {expectedSound}, got {state.PlayedSound}");
		Assert.True(state.DidTalk == expectedTalk, $"elapsed {elapsed}: talk latch must be {expectedTalk}, got {state.DidTalk}");
	}

	[Theory]
	[InlineData(0f, true)]
	[InlineData(-1f, true)]
	[InlineData(0.01f, false)]
	[InlineData(10f, false)]
	public void ShouldReplayTriggerSound_LiveRelayOnly(float elapsed, bool expected) =>
		Assert.Equal(expected, ShuttleDoorReplayState.ShouldReplayTriggerSound(elapsed));
}
