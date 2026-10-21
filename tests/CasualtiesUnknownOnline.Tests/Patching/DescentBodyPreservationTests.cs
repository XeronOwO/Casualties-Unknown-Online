using System;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The descent follow's one-shot saved-character handback rule (independent review
/// major-3): the body that came through the host's regeneration never left, so the
/// handback that answers its InWorld is refused — exactly once — while a revive
/// handback (the respawn's null-position snapshot) and anything after the first
/// refusal are applied normally. The type is compile-excluded adapter code, so it is
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

	private static CharacterDataMsg Revive() =>
		new() { Position = null };

	[Fact]
	public void TheDescentHandback_IsRefusedExactlyOnce()
	{
		var preservation = Create();
		Note(preservation, bodyId: 77);

		Assert.True(TryConsume(preservation, 77, WithPosition()));
		Assert.False(TryConsume(preservation, 77, WithPosition()), "the suppression is one-shot");
	}

	[Fact]
	public void AHandbackForAnotherBody_IsNeverRefused()
	{
		var preservation = Create();
		Note(preservation, bodyId: 77);

		Assert.False(TryConsume(preservation, 78, WithPosition()));
	}

	[Fact]
	public void AReviveHandback_IsNotConsumedAndTheDescentHandbackStillIs()
	{
		// The respawn handback has no position on purpose (it lands at the current spawn): it is a
		// handback this body DOES need, so it must neither be dropped nor spend the descent's one shot.
		var preservation = Create();
		Note(preservation, bodyId: 77);

		Assert.False(TryConsume(preservation, 77, Revive()));
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
