using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The saved position's layer rule (descent-spawn ticket): a snapshot's position belongs to the layer
/// that captured it, so a restore applies it only while this client is still in that layer. An unknown
/// stamp (no committed run baseline on either side) keeps the pre-stamp behaviour.
/// </summary>
public class CharacterRestorePositionPolicyTests
{
	private static WorldGenerationMsg Layer(int layer, ulong epoch = 7) => new() { RunEpoch = epoch, LayerIndex = layer };

	[Fact]
	public void APositionFromAnotherLayer_IsNotApplied() =>
		// The layer changed between capture and restore: the coordinates are now fresh terrain.
		Assert.True(CharacterRestorePositionPolicy.IsFromAnotherLayer(captured: Layer(2), current: Layer(3)));

	[Fact]
	public void APositionFromTheSameLayer_IsApplied() =>
		// A same-layer reconnect returns to where it left.
		Assert.False(CharacterRestorePositionPolicy.IsFromAnotherLayer(captured: Layer(3), current: Layer(3)));

	[Fact]
	public void TheRunEpoch_IsNotPartOfTheDecision() =>
		// A restore/continue names its own run; the position's meaning is the layer, not the epoch.
		Assert.False(CharacterRestorePositionPolicy.IsFromAnotherLayer(captured: Layer(3, epoch: 10), current: Layer(3, epoch: 11)));

	[Theory]
	[InlineData(true, false)]
	[InlineData(false, true)]
	[InlineData(false, false)]
	public void AnUnknownStamp_KeepsThePosition(bool capturedKnown, bool currentKnown)
	{
		var captured = capturedKnown ? Layer(2) : null;
		var current = currentKnown ? Layer(3) : null;

		Assert.False(CharacterRestorePositionPolicy.IsFromAnotherLayer(captured, current));
	}
}
