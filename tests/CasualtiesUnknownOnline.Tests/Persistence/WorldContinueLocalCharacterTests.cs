using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// What a continue hands the LOCAL player: the archive's own character for it, and
/// the facts that may travel with it. Split from <see cref="WorldSaveContinueTests"/>
/// because the subject is the local character's apply contract, not the checkpoint.
///
/// The Runtime binds every stored key a present peer claims; the key the LOCAL player
/// claims is different in kind, because the only body it can ever be applied to is
/// this process's own and that body does not exist at the click (the scene loads
/// afterwards). It therefore travels to the adapter with the outcome — the live
/// character-table slot it is also bound into cannot be told apart from the host's
/// current 1 Hz snapshot.
///
/// The position rule is the sharp one. A <c>layer-end</c> cut names the layer being
/// ENTERED, and the restore REGENERATES that layer from the baseline, so a body
/// position captured while the host was still standing in the layer being left does
/// not describe the world the restore builds. The native save carries no position at
/// all, and the game places the body itself
/// (<c>WorldGeneration.WorldPlacePlayer</c>, <c>WorldGeneration.cs:1886-1921</c>).
/// A mid-run cut names the layer its bodies stood in, so there the position IS the
/// one to restore.
/// </summary>
public class WorldContinueLocalCharacterTests
{
	private const ulong HostId = 1001UL;
	private const ulong GuestId = 2002UL;

	[Fact]
	public void LayerEndContinue_DoesNotHandTheReplacedLayersPositionToTheLocalCharacter()
	{
		using var fixture = WorldSaveFixture.Create("continue-position-layerend");
		CutAtLayerEndWithPosition(fixture, x: 40f, y: -12f);

		using var restarted = fixture.Restart("continue-position-layerend-restart");
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		// The contract is what the CONTINUE hands the local player, not what the store happens to
		// hold — the two are the same instance here, and both are pinned.
		var local = Assert.IsType<CharacterDataMsg>(outcome.LocalCharacter);
		Assert.Null(local.Position);
		Assert.Same(local, restarted.Characters.GetHostCharacterData());
	}

	[Fact]
	public void MidRunContinue_KeepsTheLocalCharactersPosition()
	{
		using var fixture = WorldSaveFixture.Create("continue-position-midrun");
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, WorldSaveCaptureTests.Run(layerIndex: 2), out _, out _));

		// A mid-run cut carries the live capture; the layer it names is the layer the
		// body was standing in, so the position describes the regenerated world.
		var captured = WorldSaveCaptureTests.Character(100, "bag");
		captured.Position = new NetVector2Msg(40f, -12f);
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.MenuReturn, out var refusal), refusal);
		var report = Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(captured, frame: 0));
		Assert.True(report.Captured, report.Summary);
		Assert.True(fixture.Repository.Repository.SetLastOpenedWorld(fixture.WorldId));

		using var restarted = fixture.Restart("continue-position-midrun-restart");
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		var local = Assert.IsType<CharacterDataMsg>(outcome.LocalCharacter);
		Assert.NotNull(local.Position);
		Assert.Equal(40f, local.Position.X);
		Assert.Equal(-12f, local.Position.Y);
		Assert.Same(local, restarted.Characters.GetHostCharacterData());
	}

	[Fact]
	public void Continue_HandsTheCallerTheCharacterTheLocalPlayerClaims()
	{
		using var fixture = WorldSaveFixture.Create("continue-local-character");
		fixture.Session.AddMember(GuestId, "Guest");
		CutAtLayerEnd(fixture, hostInstanceId: 100, guestInstanceId: 200);

		using var restarted = fixture.Restart("continue-local-character-restart");
		restarted.Session.AddMember(GuestId, "Guest");
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		// The outcome carries the LOCAL player's character — the one body this
		// process can put it on — not the guest's.
		var local = Assert.IsType<CharacterDataMsg>(outcome.LocalCharacter);
		Assert.Equal(100UL, Assert.Single(local.Items).InstanceId);

		// The guest's own character still lands in the table its reconnect path
		// sends from, exactly as before.
		var guest = Assert.IsType<CharacterDataMsg>(restarted.Characters.GetSavedCharacter(GuestId));
		Assert.Equal(200UL, Assert.Single(guest.Items).InstanceId);
	}

	[Fact]
	public void Continue_WithoutALocalClaim_HandsNoLocalCharacter()
	{
		// A world written over IP-direct is a different key space (§2): continued
		// over Steam, the stored key is claimed by nobody, so the local player must
		// join as a NEW character (decision 162) — the outcome must not invent one.
		using var fixture = WorldSaveFixture.Create("continue-local-character-ipdirect", ipDirect: true, displayName: "Host Name");
		CutAtLayerEnd(fixture, hostInstanceId: 100, guestInstanceId: null);

		using var restarted = fixture.Restart("continue-local-character-ipdirect-restart", displayName: "Host Name");
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		Assert.Null(outcome.LocalCharacter);
		Assert.Null(restarted.Characters.GetHostCharacterData());
	}

	// ---- helpers ----

	/// <summary>
	/// A layer-end cut whose host character carries a position captured in the layer
	/// being LEFT: a layer-end cut passes no live capture, so the seeded snapshot is
	/// the host's own latest one, and the layer advance is what names the next layer.
	/// </summary>
	private static void CutAtLayerEndWithPosition(WorldSaveFixture fixture, float x, float y)
	{
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, WorldSaveCaptureTests.Run(layerIndex: 0), out _, out _));

		var character = WorldSaveCaptureTests.Character(100, "bag");
		character.Position = new NetVector2Msg(x, y);
		fixture.Characters.SaveHostCharacterData(character);

		Assert.True(fixture.Kernel.TryAdvanceLayer(HostId, WorldSaveCaptureTests.Run(layerIndex: 1), out _, out _));
		Assert.True(fixture.Repository.Repository.SetLastOpenedWorld(fixture.WorldId));
	}

	/// <summary>One layer-end cut carrying a host character and, when asked, a present member's own.</summary>
	private static void CutAtLayerEnd(WorldSaveFixture fixture, ulong hostInstanceId, ulong? guestInstanceId)
	{
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, WorldSaveCaptureTests.Run(layerIndex: 0), out _, out _));
		fixture.Characters.SaveHostCharacterData(WorldSaveCaptureTests.Character(hostInstanceId, "bag"));
		if (guestInstanceId is { } guestId)
		{
			fixture.Characters.SaveCharacterData(GuestId, WorldSaveCaptureTests.Character(guestId, "rope"));
		}

		Assert.True(fixture.Kernel.TryAdvanceLayer(HostId, WorldSaveCaptureTests.Run(layerIndex: 1), out _, out _));
		Assert.True(fixture.Repository.Repository.SetLastOpenedWorld(fixture.WorldId));
	}
}
