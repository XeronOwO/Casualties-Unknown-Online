using System;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The descent follow's one-shot saved-character handback rule (independent review
/// major-3): the body that came through the host's regeneration never left, so the
/// handback that answers its InWorld is refused — exactly once — while a RESPAWN
/// handback (flagged by the host) and anything after the first refusal are applied
/// normally. The flag, not the presence of a position, names the revive: a handback can
/// arrive positionless for other reasons (an older snapshot, a later layer refusal), so
/// a position's presence is not the tell. The type is compile-excluded adapter code, so it is
/// driven reflectively.
/// </summary>
[Trait("Category", "Integration")]
[Collection(GameAssemblyCollection.Name)]
public class DescentBodyPreservationTests
{
	private static readonly Type Preservation = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Run.DescentBodyPreservation",
		throwOnError: true)!;

	private static object Create() => Activator.CreateInstance(Preservation)!;

	private static void Note(object preservation, int bodyId) =>
		Preservation.GetMethod("Note", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
			.Invoke(preservation, [bodyId]);

	private static void Clear(object preservation) =>
		Preservation.GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
			.Invoke(preservation, null);

	private static bool TryConsume(object preservation, int bodyId, CharacterDataMsg handback) =>
		(bool)Preservation.GetMethod("TryConsume", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
			.Invoke(preservation, [bodyId, handback])!;

	private static CharacterDataMsg WithPosition() =>
		new() { Position = new NetVector2Msg { X = 4f, Y = 5f } };

	private static CharacterDataMsg Respawn() =>
		new() { Position = null, Respawn = true };

	[Fact]
	public void TheDescentHandback_IsRefusedExactlyOnce()
	{
		var preservation = Create();
		Note(preservation, bodyId: 77);

		Assert.True(TryConsume(preservation, 77, WithPosition()));
		Assert.False(TryConsume(preservation, 77, WithPosition()), "the suppression is one-shot");
	}

	[Fact]
	public void TheDescentHandbackWithoutAPosition_IsStillTheDescents()
	{
		// A handback can arrive positionless for other reasons (an older snapshot, a receiver-side layer
		// refusal). It is still the handback this body must NOT take: the body came through the
		// regeneration with its items, and the snapshot's item set is the old one.
		var preservation = Create();
		Note(preservation, bodyId: 77);

		Assert.True(TryConsume(preservation, 77, new CharacterDataMsg { Position = null }));
	}

	[Fact]
	public void AHandbackForAnotherBody_IsNeverRefused()
	{
		var preservation = Create();
		Note(preservation, bodyId: 77);

		Assert.False(TryConsume(preservation, 78, WithPosition()));
	}

	[Fact]
	public void ARespawnHandback_IsNotConsumedAndTheDescentHandbackStillIs()
	{
		// The respawn names itself (the host's next-level revive): it is a handback this body DOES need,
		// so it must neither be dropped nor spend the descent's one shot.
		var preservation = Create();
		Note(preservation, bodyId: 77);

		Assert.False(TryConsume(preservation, 77, Respawn()));
		Assert.True(TryConsume(preservation, 77, WithPosition()));
	}

	[Fact]
	public void AfterTheFollowClears_NoHandbackIsRefused()
	{
		var preservation = Create();
		Note(preservation, bodyId: 77);
		Clear(preservation);

		Assert.False(TryConsume(preservation, 77, WithPosition()));
	}

	[Fact]
	public void WithNoFollow_NothingIsRefused()
	{
		var preservation = Create();

		Assert.False(TryConsume(preservation, 77, WithPosition()));
	}
}
