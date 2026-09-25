using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// <see cref="RemoteBreakPresentation"/> — the pure rule behind the reported
/// defect: the host mined continuously, every hit played a sound for the host,
/// and the guest heard only part of the block hit/break sounds.
///
/// A break reaches the other sides as TWO facts of one break: the air write that
/// makes the cell air (sent the instant the block is gone) and the drops-carrying
/// break report one frame later (the drops' <c>Item.Start</c> folds in first).
/// The air write always lands first, so the receiving side's block is already
/// gone when the report arrives and the report's own native damage roll — the
/// roll that plays the block's hit/step sounds and spawns its break particles —
/// never runs there. These rows pin WHEN the receiving side must run that roll
/// itself: once per break, never on the side that computed it, and never for a
/// write whose source played nothing.
/// </summary>
public class RemoteBreakPresentationTests
{
	[Theory]
	[InlineData(true, (ushort)0, true, RemoteBreakPresentation.Action.NativeBreak)] // a break this side has not applied yet: the block still stands here
	[InlineData(true, (ushort)0, false, RemoteBreakPresentation.Action.WriteOnly)] // this side's own break echo, a repeated report, a relay of a break it computed: the cell is already air
	[InlineData(true, (ushort)7, true, RemoteBreakPresentation.Action.WriteOnly)] // a placement carries no break claim
	[InlineData(true, (ushort)7, false, RemoteBreakPresentation.Action.WriteOnly)]
	[InlineData(false, (ushort)0, true, RemoteBreakPresentation.Action.WriteOnly)] // an earthquake/environment break, a snapshot row, a correction: silent where it came from, so silent here
	[InlineData(false, (ushort)0, false, RemoteBreakPresentation.Action.WriteOnly)]
	[InlineData(false, (ushort)7, true, RemoteBreakPresentation.Action.WriteOnly)]
	[InlineData(false, (ushort)7, false, RemoteBreakPresentation.Action.WriteOnly)]
	public void Route_PresentsExactlyTheUnappliedBreak(bool playerBreak, ushort writtenBlock, bool cellHeldBlock, RemoteBreakPresentation.Action expected) =>
		Assert.Equal(expected, RemoteBreakPresentation.Route(playerBreak, writtenBlock, cellHeldBlock));

	[Fact]
	public void Route_PresentsABreakOnce_EvenWhenItsWriteIsRepeated()
	{
		// The first write finds the block standing and presents it; every repeat
		// (the host's relay echo, the breaker's fallback re-report, a duplicate
		// air write) finds the cell air. That cell state is the whole once-only
		// guard — no extra ledger, and no double audio for one break.
		Assert.Equal(RemoteBreakPresentation.Action.NativeBreak, RemoteBreakPresentation.Route(playerBreak: true, writtenBlock: 0, cellHeldBlock: true));
		Assert.Equal(RemoteBreakPresentation.Action.WriteOnly, RemoteBreakPresentation.Route(playerBreak: true, writtenBlock: 0, cellHeldBlock: false));
	}

	[Theory]
	[InlineData(10f, 0f, 10f)] // no row yet: the block breaks on its own health
	[InlineData(10f, 4f, 6f)] // the remainder of the row this side already holds — the roll adds to it
	[InlineData(10f, 9.5f, 0.5f)]
	[InlineData(1f, 0f, 1f)] // the fragile block the footstep rule crushes
	[InlineData(10f, 10f, 10f)] // a row already at the whole health (a snapshot row CUO wrote without breaking the block): any positive amount is lethal
	[InlineData(10f, 25f, 10f)]
	public void LethalDamage_IsTheRemainderOfTheGamesOwnRow(float health, float currentDamage, float expected) =>
		Assert.Equal(expected, RemoteBreakPresentation.LethalDamage(health, currentDamage));

	/// <summary>A negative damage would heal the row it is added to; the floor keeps "the block is gone" a fact this roll cannot miss.</summary>
	[Fact]
	public void LethalDamage_IsNeverNegative() =>
		Assert.True(RemoteBreakPresentation.LethalDamage(health: 1f, currentDamage: 4f) > 0f);
}
