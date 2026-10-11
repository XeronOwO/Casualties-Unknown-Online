using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The local favourite marks at the archive: they are the marking player's own,
/// they ride that player's character record, and the record is what the world
/// archive stores per world — so a cut → <c>characters/&lt;playerKey&gt;.json</c> →
/// continue round trip has to bring them back exactly (user ruling 2026-10-11,
/// decision 258). This is the persistence half of the ticket's requirement 2; the
/// wire half is <c>LocalFavouriteMarksTests</c> and the real-machine half is the
/// rejoin/continue acceptance rows.
///
/// The cut is taken at a layer end, as in
/// <see cref="WorldCharacterNativeFieldTests"/>, because that is the seam whose
/// character the restore hands back to this process's own body.
/// </summary>
public sealed class WorldCharacterFavouriteMarkTests
{
	private const ulong HostId = 1001UL;

	[Fact]
	public void LayerEndCutThenContinue_RoundTripsTheLocalCharactersFavouriteMarks()
	{
		using var fixture = WorldSaveFixture.Create("favourite-marks-roundtrip");
		var character = WorldSaveCaptureTests.Character(100, "bag");
		character.FavouriteMarks =
		[
			new CharacterFavouriteMarkMsg { OwnerSteamId = 2002UL, ItemInstanceId = 41, ItemId = "scrapmetal" },
			new CharacterFavouriteMarkMsg { OwnerSteamId = 2003UL, ItemInstanceId = 42, ItemId = "bandage" },
		];
		CutAtLayerEnd(fixture, character);

		using var restarted = fixture.Restart("favourite-marks-roundtrip-restart");
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		var local = Assert.IsType<CharacterDataMsg>(outcome.LocalCharacter);
		var marks = Assert.IsType<List<CharacterFavouriteMarkMsg>>(local.FavouriteMarks);
		Assert.Equal(2, marks.Count);
		Assert.Equal(2002UL, marks[0].OwnerSteamId);
		Assert.Equal(41UL, marks[0].ItemInstanceId);
		Assert.Equal("scrapmetal", marks[0].ItemId);
		Assert.Equal(2003UL, marks[1].OwnerSteamId);
		Assert.Equal(42UL, marks[1].ItemInstanceId);
		Assert.Equal("bandage", marks[1].ItemId);
	}

	[Fact]
	public void AStoredCharacterWithoutTheMarksField_ComesBackWithNone()
	{
		// An older record: the list decodes to null, which the seed reads as "this
		// character brought no marks" rather than as a set to be guessed.
		using var fixture = WorldSaveFixture.Create("favourite-marks-absent");
		CutAtLayerEnd(fixture, WorldSaveCaptureTests.Character(100, "bag"));

		using var restarted = fixture.Restart("favourite-marks-absent-restart");
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		var local = Assert.IsType<CharacterDataMsg>(outcome.LocalCharacter);
		Assert.Null(local.FavouriteMarks);
	}

	private static void CutAtLayerEnd(WorldSaveFixture fixture, CharacterDataMsg hostCharacter)
	{
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, WorldSaveCaptureTests.Run(layerIndex: 0), out _, out _));
		fixture.Characters.SaveHostCharacterData(hostCharacter);
		Assert.True(fixture.Kernel.TryAdvanceLayer(HostId, WorldSaveCaptureTests.Run(layerIndex: 1), out _, out _));
		Assert.True(fixture.Repository.Repository.SetLastOpenedWorld(fixture.WorldId));
	}
}
