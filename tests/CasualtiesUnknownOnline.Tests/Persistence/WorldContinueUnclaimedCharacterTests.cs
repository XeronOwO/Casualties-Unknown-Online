using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// A continue binds only the stored characters a PRESENT peer claims (decision 162: an absent
/// member joins as a NEW character). The kernel rows the absent character carried must not enter
/// the restored table — nothing in this session will ever answer them, and every later custody
/// read and save inherits them (2026-11-08, batch <c>20261108-a</c>: four
/// <c>RowWithoutObject:Carried</c> rows for the member's pre-cut ids).
/// </summary>
public class WorldContinueUnclaimedCharacterTests
{
	private const ulong HostId = 1001UL;
	private const ulong GuestId = 2002UL;
	private const ulong HostItemId = 100UL;
	private const ulong GuestItemId = 200UL;

	[Fact]
	public void Continue_DropsTheCarriedRowsOfACharacterNoPresentPeerClaims()
	{
		using var fixture = WorldSaveFixture.Create("continue-unclaimed-rows");
		fixture.Session.AddMember(GuestId, "Guest");
		CutAtLayerEndWithCarriedRows(fixture);

		// The guest is NOT a member of the restarted session: the key it wrote is unclaimed.
		using var restarted = fixture.Restart("continue-unclaimed-rows-restart");
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		var rows = restarted.Kernel.QueryItems();
		Assert.False(rows.ContainsKey(GuestItemId), "the unclaimed character's carried row must not enter the restored table");
		Assert.True(rows.ContainsKey(HostItemId), "the claimed character's carried row must survive the restore");
	}

	private static void CutAtLayerEndWithCarriedRows(WorldSaveFixture fixture)
	{
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, WorldSaveCaptureTests.Run(layerIndex: 0), out _, out _));

		var host = WorldSaveCaptureTests.Character(HostItemId, "bag");
		var guest = WorldSaveCaptureTests.Character(GuestItemId, "rope");
		fixture.Characters.SaveHostCharacterData(host);
		fixture.Characters.SaveCharacterData(GuestId, guest);

		Assert.True(fixture.Kernel.TrySpawnCarried(HostId, HostItemId, "bag", host.Items[0], out _, out var hostRejection), $"{hostRejection}");
		Assert.True(fixture.Kernel.TrySpawnCarried(GuestId, GuestItemId, "rope", guest.Items[0], out _, out var guestRejection), $"{guestRejection}");

		Assert.True(fixture.Kernel.TryAdvanceLayer(HostId, WorldSaveCaptureTests.Run(layerIndex: 1), out _, out _));
		Assert.True(fixture.Repository.Repository.SetLastOpenedWorld(fixture.WorldId));
	}
}
