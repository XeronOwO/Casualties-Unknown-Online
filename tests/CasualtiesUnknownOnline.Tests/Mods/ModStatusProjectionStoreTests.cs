using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The phase-3 store-side projection seam: projection-kind declarations are
/// validated against body/limb scope, the store exposes typed snapshots to the
/// GameAdapter, and every status write/removal raises the refresh event.
/// </summary>
public class ModStatusProjectionStoreTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static ModStatusStore StoreOf(TestNode node) =>
		node.Services.GetRequiredService<ModService>().StatusStore;

	private static IModStatusRuntime StatusOf(TestNode node) =>
		((TestDataMod)node.Services.GetRequiredService<ModService>()
			.LoadedMods.Single(m => m is TestDataMod)).Context!.StatusRuntime;

	[Fact]
	public void BodyFormula_DeclareAndSet_ProducesBodySnapshot()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var status = StatusOf(host);
		var store = StoreOf(host);

		Assert.True(status.TryDeclare(
			"body.formula",
			ModStatusScope.Body,
			ModDataScope.Shared,
			projectionKind: ModStatusProjectionKind.BodyFormula));
		Assert.True(status.TrySetBodyStatus(
			"body.formula",
			HostId,
			new ModBodyFormulaProjection { JumpSpeed = 4f, Immunity = 8f }.ToPayload()));

		var snapshots = store.GetProjectionSnapshots(HostId);
		var snapshot = Assert.Single(snapshots);
		Assert.Equal("body.formula", snapshot.StatusId);
		Assert.Equal(ModStatusScope.Body, snapshot.Scope);
		Assert.Equal(ModStatusProjectionKind.BodyFormula, snapshot.ProjectionKind);
		Assert.Equal(HostId, snapshot.PlayerSteamId);
		Assert.Equal(-1, snapshot.LimbSlot);

		var projection = ModBodyFormulaProjection.FromPayload(snapshot.Value);
		Assert.NotNull(projection);
		Assert.Equal(4f, projection!.JumpSpeed);
		Assert.Equal(8f, projection.Immunity);
	}

	[Fact]
	public void LimbPhysiology_DeclareAndSet_ProducesLimbSnapshotWithSlot()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var status = StatusOf(host);
		var store = StoreOf(host);

		Assert.True(status.TryDeclare(
			"limb.proj",
			ModStatusScope.Limb,
			ModDataScope.LocalOnly,
			projectionKind: ModStatusProjectionKind.LimbPhysiology));
		Assert.True(status.TrySetLimbStatus(
			"limb.proj",
			HostId,
			2,
			new ModLimbProjection { BleedAmount = 1.5f, SkinHealth = -3f }.ToPayload()));

		var snapshot = Assert.Single(store.GetProjectionSnapshots(HostId));
		Assert.Equal("limb.proj", snapshot.StatusId);
		Assert.Equal(ModStatusScope.Limb, snapshot.Scope);
		Assert.Equal(ModStatusProjectionKind.LimbPhysiology, snapshot.ProjectionKind);
		Assert.Equal(2, snapshot.LimbSlot);

		var projection = ModLimbProjection.FromPayload(snapshot.Value);
		Assert.NotNull(projection);
		Assert.Equal(1.5f, projection!.BleedAmount);
		Assert.Equal(-3f, projection.SkinHealth);
	}

	[Fact]
	public void ProjectionKind_MustMatchBodyLimbScope()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var status = StatusOf(host);

		Assert.False(status.TryDeclare(
			"wrong.body",
			ModStatusScope.Limb,
			ModDataScope.LocalOnly,
			projectionKind: ModStatusProjectionKind.BodyFormula));
		Assert.False(status.TryDeclare(
			"wrong.limb",
			ModStatusScope.Body,
			ModDataScope.LocalOnly,
			projectionKind: ModStatusProjectionKind.LimbPhysiology));
	}

	[Fact]
	public void StatusChanged_FiresOnSetAndRemoval_AndRemovalClearsSnapshot()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var status = StatusOf(host);
		var store = StoreOf(host);
		var changes = 0;
		store.StatusChanged += () => changes++;

		Assert.True(status.TryDeclare(
			"body.formula",
			ModStatusScope.Body,
			ModDataScope.LocalOnly,
			projectionKind: ModStatusProjectionKind.BodyFormula));
		Assert.Equal(0, changes);

		Assert.True(status.TrySetBodyStatus(
			"body.formula",
			HostId,
			new ModBodyFormulaProjection { MaxEncumbrance = 1f }.ToPayload()));
		Assert.Equal(1, changes);
		Assert.Single(store.GetProjectionSnapshots(HostId));

		Assert.True(status.TryRemoveBodyStatus("body.formula", HostId));
		Assert.Equal(2, changes);
		Assert.Empty(store.GetProjectionSnapshots(HostId));
	}

	[Fact]
	public void OpaqueStatuses_AreExcludedFromProjectionSnapshots()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var status = StatusOf(host);
		var store = StoreOf(host);

		Assert.True(status.TryDeclare("opaque", ModStatusScope.Body, ModDataScope.LocalOnly));
		Assert.True(status.TrySetBodyStatus("opaque", HostId, [1, 2, 3]));

		Assert.Empty(store.GetProjectionSnapshots(HostId));
	}

	[Fact]
	public void StatusPresences_IncludeOpaqueAndLimbValues_ForRequestedPlayer()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var status = StatusOf(host);
		var store = StoreOf(host);

		Assert.True(status.TryDeclare("opaque.body", ModStatusScope.Body, ModDataScope.LocalOnly));
		Assert.True(status.TrySetBodyStatus("opaque.body", HostId, [1, 2, 3]));

		Assert.True(status.TryDeclare("opaque.limb", ModStatusScope.Limb, ModDataScope.LocalOnly));
		Assert.True(status.TrySetLimbStatus("opaque.limb", HostId, 3, [4, 5]));

		var presences = store.GetStatusPresences(HostId);
		Assert.Equal(2, presences.Count);
		Assert.Contains(presences, p => p.StatusId == "opaque.body" && p.Scope == ModStatusScope.Body && p.LimbSlot == -1);
		Assert.Contains(presences, p => p.StatusId == "opaque.limb" && p.Scope == ModStatusScope.Limb && p.LimbSlot == 3);

		Assert.Empty(store.GetStatusPresences(GuestId));
	}

}
