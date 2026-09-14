using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// S3.4b at the archive: a character's native fields (<c>lastHappiness</c>,
/// <c>caloriesConsumed</c>, <c>WoundView.cInfo</c>) have to survive the cut →
/// <c>character.json</c> → continue round trip, and a snapshot that carries none of
/// them has to be NAMED in the restore report instead of silently continuing with
/// the game's defaults (§6.1).
///
/// The cut is taken at a layer end, as in
/// <see cref="WorldContinueLocalCharacterTests"/>, because that is the seam whose
/// character the restore hands back to this process's own body.
/// </summary>
public sealed class WorldCharacterNativeFieldTests
{
	private const ulong HostId = 1001UL;
	private const ulong GuestId = 2002UL;

	[Fact]
	public void LayerEndCutThenContinue_RoundTripsTheLocalCharactersNativeFields()
	{
		using var fixture = WorldSaveFixture.Create("native-fields-roundtrip");
		var character = WorldSaveCaptureTests.Character(100, "bag");
		character.NativeFields = new CharacterNativeFieldsMsg
		{
			LastHappiness = [0.25f, 0.5f, 0.75f],
			CaloriesConsumed = 4100,
			CharacterInfo = [171, 24, 8123, 3],
		};
		CutAtLayerEnd(fixture, character);

		using var restarted = fixture.Restart("native-fields-roundtrip-restart");
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		var local = Assert.IsType<CharacterDataMsg>(outcome.LocalCharacter);
		var fields = Assert.IsType<CharacterNativeFieldsMsg>(local.NativeFields);
		Assert.Equal([0.25f, 0.5f, 0.75f], fields.LastHappiness);
		Assert.Equal(4100, fields.CaloriesConsumed);
		Assert.Equal([171, 24, 8123, 3], fields.CharacterInfo);
		Assert.DoesNotContain("native character fields", outcome.Summary, StringComparison.Ordinal);
	}

	[Fact]
	public void Continue_WhenAStoredCharacterCarriesNoNativeFields_NamesTheGapInTheRestoreReport()
	{
		// The old-sender case: a 1 Hz report from a build that predates S3.4b (or a
		// scene that could not be read) stores a character without the fields. The
		// restore has to say so — the alternative is a continued character that
		// quietly shows 0 cm / 0 y / #0 (PlayerCamera.cs:726-729 skips the game's own
		// roll when a run is continued).
		using var fixture = WorldSaveFixture.Create("native-fields-named");
		fixture.Session.AddMember(GuestId, "Guest");
		var guest = WorldSaveCaptureTests.Character(200, "rope");
		guest.NativeFields = new CharacterNativeFieldsMsg { LastHappiness = [0.5f], CaloriesConsumed = 9, CharacterInfo = [170, 20, 7, 1] };
		CutAtLayerEnd(fixture, WorldSaveCaptureTests.Character(100, "bag"), guest);

		using var restarted = fixture.Restart("native-fields-named-restart");
		restarted.Session.AddMember(GuestId, "Guest");
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		// The guest's file still has its values...
		var storedGuest = Assert.IsType<CharacterDataMsg>(restarted.Characters.GetSavedCharacter(GuestId));
		Assert.NotNull(storedGuest.NativeFields);

		// ...and the host's own (which carried none) is named, by key.
		Assert.Contains("carries no native character fields", outcome.Summary, StringComparison.Ordinal);
		Assert.Contains("steam-1001", outcome.Summary, StringComparison.Ordinal);
		Assert.Contains("lastHappiness, caloriesConsumed, WoundView.cInfo", outcome.Summary, StringComparison.Ordinal);
	}

	/// <summary>
	/// A layer-end cut whose host character is the given one (the host's own latest
	/// snapshot is what the layer-end seam carries — it passes no live capture) with
	/// the named member's own character beside it, then the world is pointed at for
	/// the continue.
	/// </summary>
	private static void CutAtLayerEnd(WorldSaveFixture fixture, CharacterDataMsg hostCharacter, CharacterDataMsg? guestCharacter = null)
	{
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, WorldSaveCaptureTests.Run(layerIndex: 0), out _, out _));
		fixture.Characters.SaveHostCharacterData(hostCharacter);
		if (guestCharacter is { } guest)
		{
			fixture.Characters.SaveCharacterData(GuestId, guest);
		}

		Assert.True(fixture.Kernel.TryAdvanceLayer(HostId, WorldSaveCaptureTests.Run(layerIndex: 1), out _, out _));
		Assert.True(fixture.Repository.Repository.SetLastOpenedWorld(fixture.WorldId));
	}

	[Fact]
	public void Continue_DoesNotBlameACharacterNoPresentPeerClaims()
	{
		// A stored file nobody claims is not restored at all (decision 162: that player
		// joins as a new character), so naming its fields as damage would describe a
		// degradation the player never gets — and would bury the real damage in noise.
		using var fixture = WorldSaveFixture.Create("native-fields-unclaimed");
		CutAtLayerEnd(fixture, WorldSaveCaptureTests.Character(100, "bag"));

		// The peer the file belongs to is NOT a member of the restarted session: its
		// key stays unclaimed while the host's own character (which has no native
		// fields either) is claimed and therefore named.
		using var restarted = fixture.Restart("native-fields-unclaimed-restart");
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		Assert.Contains("carries no native character fields", outcome.Summary, StringComparison.Ordinal);
		Assert.DoesNotContain($"steam-{GuestId}", outcome.Summary, StringComparison.Ordinal);
	}

	[Fact]
	public void Continue_NamesAStoredCharactersMalformedNativeFields()
	{
		// The snapshot HAS the sub-message but its history is not the game's window: the
		// report has to name that field instead of treating "not null" as "usable".
		using var fixture = WorldSaveFixture.Create("native-fields-malformed");
		var host = WorldSaveCaptureTests.Character(100, "bag");
		host.NativeFields = new CharacterNativeFieldsMsg
		{
			LastHappiness = [0.5f],
			CaloriesConsumed = 4100,
			CharacterInfo = [171, 24, 8123, 3],
		};
		CutAtLayerEnd(fixture, host);

		using var restarted = fixture.Restart("native-fields-malformed-restart");
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		Assert.Contains("cannot restore", outcome.Summary, StringComparison.Ordinal);
		Assert.Contains("the happiness history", outcome.Summary, StringComparison.Ordinal);
		Assert.Contains("steam-1001", outcome.Summary, StringComparison.Ordinal);
	}
}

