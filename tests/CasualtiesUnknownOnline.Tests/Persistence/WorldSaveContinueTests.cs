using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.Fluids;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The continue side of S2 (§6, decision 165): the native Continue entry has to
/// resolve to "restore the selected CUO world" — the kernel checkpoint and the
/// stored characters — and it must refuse rather than fall back to regenerating
/// a layer the snapshot does not name. Restoring twice is idempotent and the
/// item facts keep a stable fingerprint.
/// </summary>
public class WorldSaveContinueTests
{
	private const ulong HostId = 1001UL;

	[Fact]
	public void ContinueWorldId_UsesTheLastOpenedPointerThenTheNewest()
	{
		using var fixture = WorldSaveFixture.Create("continue-target");

		// A world with no snapshot is not a continue target at all.
		Assert.Null(fixture.Service.ContinueWorldId);

		// The first world that actually carries a snapshot becomes the target.
		SaveLayerEnd(fixture, withCharacter: false);
		var first = fixture.WorldId;
		Assert.Equal(first, fixture.Service.ContinueWorldId);

		// The pointer wins over the newest world; a pointer naming a folder that no
		// longer holds a snapshot falls through to the newest one that does.
		Assert.True(fixture.Repository.Repository.SetLastOpenedWorld(first));
		Assert.Equal(first, fixture.Service.ContinueWorldId);
		Assert.True(fixture.Repository.Repository.CreateWorld("Second").Success);
		Assert.Equal(first, fixture.Service.ContinueWorldId);
	}

	[Fact]
	public void TryContinue_RestoresTheKernelCheckpoint()
	{
		using var fixture = WorldSaveFixture.Create("continue-kernel");
		SaveLayerEnd(fixture, withCharacter: true);
		Assert.True(fixture.Repository.Repository.SetLastOpenedWorld(fixture.WorldId));

		using var restarted = fixture.Restart("continue-kernel-restart");
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		Assert.True(outcome.Started);
		Assert.Equal(fixture.WorldId, outcome.WorldId);
		Assert.True(outcome.Salvage.IsClean, outcome.Summary);
		var run = restarted.Kernel.QueryRun();
		Assert.NotNull(run);
		Assert.Equal(1, run!.LayerIndex);
		Assert.Equal(1, Assert.Single(restarted.Kernel.QueryItems()).Value.Revision > 0 ? 1 : 0);
	}

	[Fact]
	public void TryContinue_ReleasesTheHalvesThePreviousAttemptLeftArmed()
	{
		// A restore that never reached its seam leaves its arms behind, and BOTH the
		// world-entry seam and this restore's expectation act on PRESENCE while a
		// contribution is attributed by the arm's STAMP. Kept, the dead attempt's arm
		// would be written into this layer and counted as a half this restore owes whose
		// identity can never match — the account would await a contribution the audit
		// refuses, forever. A mid-run cut is the case where nothing else releases it
		// (a layer-end cut cancels the world-entity half on its own).
		using var fixture = WorldSaveFixture.Create("continue-supersede");
		SaveMidRun(fixture);
		Assert.True(fixture.Repository.Repository.SetLastOpenedWorld(fixture.WorldId));

		var entities = new FakeRestoredWorldEntitySource { Armed = true, Sequence = 7, Facts = EntityFacts() };
		var native = new FakeNativeWorldFacts();
		native.SeedKeypad(5f, 6f, "1234");
		native.ApplyKeypadCodes(native.Keypads);
		Assert.True(native.HasPendingRestore);
		var audit = new WorldRestoreAudit();
		using var restarted = WorldSaveFixture.Create(
			"continue-supersede-restart", repository: fixture.Repository, nativeWorldFacts: native, worldEntities: entities, audit: audit);

		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		// The dead attempt's arms are released, with the reason named...
		Assert.False(entities.Armed);
		Assert.Contains("superseded", Assert.Single(entities.Cancels), StringComparison.Ordinal);
		Assert.Contains("cancel-pending", native.Calls);
		// ...including the adapter's native handover, which carries no stamp of its own:
		// the dead attempt's keypad code must not ride this restore's world-fact half.
		Assert.False(native.HasPendingRestore);

		// ...and it is NOT one of the halves the new restore owes: the writers armed for
		// THIS attempt are the world-fact half alone (the fixture composes no item control).
		Assert.Equal(1, audit.ExpectedContributions);
		Assert.True(audit.AwaitingLiveWrite);

		// The world the new attempt replaced is still the one its own halves belong to.
		audit.LiveWriteFinished(restarted.Kernel.RestoreSequence, complete: true, refused: [], summary: "the world facts landed");
		Assert.False(audit.AwaitingLiveWrite);
		Assert.Equal(fixture.WorldId, audit.Last!.WorldId);
	}

	private static RestoredWorldEntityFacts EntityFacts() => new(
		[
			new EntityEventMsg
			{
				Kind = EntityEventKind.BearTrapClamped,
				Extra = 3,
				Position = new NetVector2Msg(1.5f, 2.5f),
			},
		],
		[new NetVector2Msg(3.5f, 4.5f)],
		[new BuildingEntityHealthEntryMsg { X = 5.5f, Y = 6.5f, Health = 12f }]);

	[Fact]
	public void TryContinue_OpensTheAccountForTheSameRestoreItStampedTheFactTablesWith()
	{
		// The identity the restore account attributes by: the click restores the kernel,
		// every arm it creates is stamped with THAT restore's sequence, and the account
		// is opened for the same value. A drift here would make the restore's own halves
		// unattributable — they would be ignored as stragglers and the restore would
		// never report.
		using var fixture = WorldSaveFixture.Create("continue-audit-identity");
		SaveLayerEnd(fixture, withCharacter: false);
		Assert.True(fixture.Repository.Repository.SetLastOpenedWorld(fixture.WorldId));

		var audit = new WorldRestoreAudit();
		var reports = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reports.Add;
		using var restarted = WorldSaveFixture.Create("continue-audit-identity-restart", repository: fixture.Repository, audit: audit);

		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		// The attempt this continue applied: the world-fact tables carry it...
		Assert.Equal(restarted.Kernel.RestoreSequence, restarted.WorldFacts.AppliedRestoreSequence);

		// ...and a half carrying it is THIS restore's half, not a straggler.
		audit.LiveWriteFinished(restarted.Kernel.RestoreSequence, complete: true, refused: [], summary: "the world facts landed");

		var report = Assert.Single(reports);
		Assert.True(report.Complete);
		Assert.Equal(fixture.WorldId, report.WorldId);
	}

	[Fact]
	public void TryContinue_Twice_KeepsTheSameFingerprintAndFacts()
	{
		using var fixture = WorldSaveFixture.Create("continue-idempotent");
		SaveLayerEnd(fixture, withCharacter: false);
		var expected = WorldArchiveRoundTrip.ItemFingerprint(fixture.Kernel.CreateCheckpoint());

		using var first = fixture.Restart("continue-idempotent-1");
		using var second = fixture.Restart("continue-idempotent-2");
		Assert.True(first.Service.TryContinue(out _));
		Assert.True(second.Service.TryContinue(out _));

		// Restoring an already-restored snapshot converges on the same world: same
		// identity, location and revision per item, and the same revision counter.
		Assert.Equal(expected, WorldArchiveRoundTrip.ItemFingerprint(first.Kernel.CreateCheckpoint()));
		Assert.Equal(expected, WorldArchiveRoundTrip.ItemFingerprint(second.Kernel.CreateCheckpoint()));
		Assert.Equal(first.Kernel.CurrentGlobalRevision, second.Kernel.CurrentGlobalRevision);
		Assert.Equal(first.Kernel.QueryRun()!.RunId, second.Kernel.QueryRun()!.RunId);
	}

	[Fact]
	public void ContainerTree_AfterRestore_HasExactlyOneParentPerChild()
	{
		// A container tree is an IN-LAYER fact, so it rides a mid-run cut: the layer the
		// cut names is the one its bodies stand in, and the restored rows belong to it.
		using var fixture = WorldSaveFixture.Create("continue-container-tree");
		var parent = new CharacterItemMsg
		{
			InstanceId = 100,
			ItemId = "bag",
			Condition = 1f,
			Contents =
			[
				new CharacterItemMsg { InstanceId = 101, ItemId = "water", Condition = 0.5f },
				new CharacterItemMsg { InstanceId = 102, ItemId = "rope", Condition = 1f },
			],
		};
		Assert.True(fixture.Kernel.TrySpawn(HostId, new ItemIdentity(100, "bag"), ItemLocation.World(1, 2), parent, out _, out _));
		fixture.Kernel.SyncContainerContents(HostId, 100, parent, new ActorId(HostId));
		SaveMidRun(fixture);

		using var restarted = fixture.Restart("continue-container-tree-restart");
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		var items = restarted.Kernel.QueryItems().Values.OrderBy(item => item.Identity.InstanceId).ToList();
		Assert.Equal([100UL, 101UL, 102UL], items.Select(item => item.Identity.InstanceId));

		// Exactly one parent per child, and the parent is the container: no child
		// is re-materialized as a second world item (acceptance row 2).
		var children = items.Where(item => item.Identity.InstanceId != 100).ToList();
		Assert.Equal(2, children.Count);
		Assert.All(children, child => Assert.Equal(ItemLocationKind.Contained, child.Location.Kind));
		Assert.All(children, child => Assert.Equal(100UL, child.Location.ParentItemId));
		Assert.Single(children.Select(child => child.Location.ParentItemId).Distinct());
		Assert.Equal(0.5f, items.Single(item => item.Identity.InstanceId == 101).Data.Condition);
	}

	[Fact]
	public void RemovedEnemy_StaysTerminalAfterRestore()
	{
		using var fixture = WorldSaveFixture.Create("continue-terminal-enemy");
		var enemyId = new EntityId(1UL, 7U, 0);
		Assert.True(fixture.Kernel.TryUpsertEnemy(HostId, new EnemyState(enemyId, "spider", 4f, false, false), out _, out _));
		Assert.True(fixture.Kernel.TryRemoveEnemy(HostId, enemyId, out _, out _));
		SaveLayerEnd(fixture, withCharacter: false);

		using var restarted = fixture.Restart("continue-terminal-enemy-restart");
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		var enemies = restarted.Kernel.QueryEnemies()!;
		Assert.Empty(enemies.Enemies);
		Assert.Equal(enemyId, Assert.Single(enemies.Removed));

		// A terminal fact never resurrects: the kernel refuses the re-upsert and the
		// tombstone keeps the id out of the table.
		Assert.False(restarted.Kernel.TryUpsertEnemy(HostId, new EnemyState(enemyId, "spider", 4f, false, false), out _, out _));
		Assert.Empty(restarted.Kernel.QueryEnemies()!.Enemies);
		Assert.Equal(enemyId, Assert.Single(restarted.Kernel.QueryEnemies()!.Removed));
	}

	[Fact]
	public void ConsumedTrap_StaysConsumedAfterRestore()
	{
		// A consumed trap is an IN-LAYER fact (a position-keyed entity fact), so it rides
		// a mid-run cut: the layer the cut names is the one the trap stands in. A
		// layer-end cut carries none of them — the layer it names is regenerated.
		using var fixture = WorldSaveFixture.Create("continue-terminal-trap");
		var position = new EntityPosition(3, 4);
		Assert.True(fixture.Kernel.TryRecordTrapConsumed(HostId, position, 2, 0, 1234, out _, out _));
		SaveMidRun(fixture);

		using var restarted = fixture.Restart("continue-terminal-trap-restart");
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		// A trap that was consumed stays consumed: the fact survives the snapshot and
		// re-recording it cannot produce a second consumption of the same trap.
		Assert.Single(restarted.Kernel.QueryWorldEntities()!.Consumptions);
		Assert.True(restarted.Kernel.TryRecordTrapConsumed(HostId, position, 2, 0, 1234, out _, out _));
		Assert.Single(restarted.Kernel.QueryWorldEntities()!.Consumptions);
	}

	[Fact]
	public void TryContinue_WithoutAReadableRunBaseline_IsRefused()
	{
		using var fixture = WorldSaveFixture.Create("continue-damaged-run");
		SaveLayerEnd(fixture, withCharacter: false);
		var live = fixture.Repository.Workspace.LiveDirectory(fixture.WorldId);

		// The archive's live run baseline is replaced by a file the decoder cannot
		// use: the continue must refuse instead of starting a fresh layer.
		File.WriteAllText(Path.Combine(live, SaveArchiveFormat.RunFileName), "[{\"runId\":42,\"randomState\":\"\"}]\n");

		using var restarted = fixture.Restart("continue-damaged-run-restart");
		Assert.True(restarted.Service.HasRestorableWorld);
		Assert.False(restarted.Service.TryContinue(out var outcome));

		Assert.False(outcome.Started);
		Assert.Contains("run baseline", outcome.Summary, StringComparison.Ordinal);
		Assert.Null(restarted.Kernel.QueryRun());
	}

	[Fact]
	public void TryContinue_AppliesTheStoredHostCharacter()
	{
		using var fixture = WorldSaveFixture.Create("continue-host-character");
		SaveLayerEnd(fixture, withCharacter: true);

		using var restarted = fixture.Restart("continue-host-character-restart");
		Assert.True(restarted.Service.TryContinue(out _));

		var character = restarted.Characters.GetHostCharacterData();
		Assert.NotNull(character);
		Assert.Equal(100UL, Assert.Single(character!.Items).InstanceId);
		Assert.Equal("steam-1001", Assert.Single(restarted.Service.PendingCharacters).PlayerKey);
	}

	[Fact]
	public void TryContinue_InIpDirectMode_ClaimsTheStoredCharacterByName()
	{
		using var fixture = WorldSaveFixture.Create("continue-ip-claim", ipDirect: true, displayName: "Host Name");
		SaveLayerEnd(fixture, withCharacter: true);

		// The same name in a different case/spacing still claims the character.
		using var restarted = fixture.Restart("continue-ip-claim-restart", ipDirect: true, displayName: " host  NAME ");
		Assert.True(restarted.Service.TryContinue(out _));

		Assert.NotNull(restarted.Characters.GetHostCharacterData());
	}

	[Fact]
	public void TryContinue_OverSteamMode_DoesNotClaimANameKey()
	{
		using var fixture = WorldSaveFixture.Create("continue-ip-to-steam", ipDirect: true, displayName: "Host Name");
		SaveLayerEnd(fixture, withCharacter: true);

		// A world written over IP-direct is a different key space: over Steam the
		// stored key is claimed by nobody, so the host joins as a NEW character
		// (decision 162) — even when the Steam persona spells exactly the same name,
		// which is why the live transport space is compared, not just the prefix.
		using var restarted = fixture.Restart("continue-ip-to-steam-restart", displayName: "Host Name");
		Assert.True(restarted.Service.TryContinue(out _));

		Assert.Null(restarted.Characters.GetHostCharacterData());
		Assert.Equal("name-host-name", Assert.Single(restarted.Service.PendingCharacters).PlayerKey);
	}

	[Fact]
	public void TryContinue_BindsAPresentMembersCharacterThroughTheExistingPath()
	{
		using var fixture = WorldSaveFixture.Create("continue-guest");
		fixture.Session.AddMember(2002UL, "Guest");
		SaveLayerEnd(fixture, withCharacter: true, guestCharacter: true);

		using var restarted = fixture.Restart("continue-guest-restart");
		restarted.Session.AddMember(2002UL, "Guest");
		Assert.True(restarted.Service.TryContinue(out _));

		Assert.NotNull(restarted.Characters.GetSavedCharacter(2002UL));
		Assert.NotNull(restarted.Characters.GetHostCharacterData());
	}

	[Fact]
	public void TryContinue_WithADamagedLiveManifest_FallsBackToTheBackup()
	{
		using var fixture = WorldSaveFixture.Create("continue-backup-fallback");
		SaveLayerEnd(fixture, withCharacter: false);
		var live = fixture.Repository.Workspace.LiveDirectory(fixture.WorldId);
		var manifestPath = Path.Combine(live, SaveArchiveFormat.ManifestFileName);
		File.WriteAllText(manifestPath, "{ not json");
		Assert.Equal("{ not json", File.ReadAllText(manifestPath));

		using var restarted = fixture.Restart("continue-backup-fallback-restart");
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		// The live manifest is the only hard gate: a damaged one opens the newest
		// readable backup and SAYS SO in the report (§6, decision 163).
		Assert.True(outcome.Started);
		Assert.True(outcome.Summary.Contains("ManifestUnreadable", StringComparison.Ordinal), $"summary was: {outcome.Summary} (world {restarted.WorldId}, fixture world {fixture.WorldId})");
		Assert.True(outcome.Salvage.Report.Entries.Count > 0, "a fallback is never silent (§6)");
		Assert.Contains(outcome.Salvage.Report.Entries, entry => entry.Reason == DamageReport.EntryReason.ManifestUnreadable);
		Assert.NotNull(restarted.Kernel.QueryRun());
	}

	[Fact]
	public void TryContinue_WithoutAWorld_IsRefused()
	{
		using var fixture = WorldSaveFixture.Create("continue-empty");

		// The repository lists only folders that exist: with none, the entry has
		// nothing to open and says so instead of starting a fresh run.
		Directory.Delete(fixture.Repository.Repository.PathOfWorld(fixture.Repository.WorldId), recursive: true);

		Assert.False(fixture.Service.HasRestorableWorld);
		Assert.False(fixture.Service.TryContinue(out var outcome));
		Assert.Contains("no CUO world", outcome.Summary, StringComparison.Ordinal);
	}

	// ---- the layer-scoped kernel tables (enemies, fluids) ----

	[Fact]
	public void LayerEndRestore_LendsNoReplacedLayerEnemyRowToTheNewLayer()
	{
		// A live enemy row is a fact about ONE layer: the layout it stands in and the
		// id the host allocated for it belong to that layer. A layer-end cut names the
		// layer being ENTERED — regenerated from the run baseline with its own enemies
		// — so the rows describe the layer being LEFT. Restoring them puts a foreign
		// layer's fact into the kernel, where a late-joining guest's checkpoint carries
		// it and the guest's position pairing can hand it to a freshly generated enemy.
		// The tombstone is NOT in this set (a terminal fact must stay terminal).
		using var fixture = WorldSaveFixture.Create("continue-layer-end-enemy");
		var enemyId = new EntityId(1UL, 7U, 0);
		Assert.True(fixture.Kernel.TryUpsertEnemy(
			HostId, new EnemyState(enemyId, "spider", 4f, false, false), out _, out _));
		SaveMidRunAsLayerEnd(fixture, "continue-layer-end-enemy");

		using var restarted = fixture.Restart("continue-layer-end-enemy-restart");
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		Assert.Empty(restarted.Kernel.QueryEnemies()!.Enemies);
	}

	[Fact]
	public void LayerEndRestore_LendsNoReplacedLayerFluidRowToTheNewLayer()
	{
		// The fluid table is the same shape of fact: coarse per-chunk totals of ONE
		// layer's grid. The regenerated layer runs its own 5 s aggregation, so a
		// restored chunk total would be stale truth about a grid that no longer exists.
		using var fixture = WorldSaveFixture.Create("continue-layer-end-fluid");
		Assert.True(fixture.Kernel.TryUpdateFluidRegion(
			HostId, new FluidRegionState(1, 2, 7, 1, 50), out _, out _));
		SaveMidRunAsLayerEnd(fixture, "continue-layer-end-fluid");

		using var restarted = fixture.Restart("continue-layer-end-fluid-restart");
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		Assert.Empty(restarted.Kernel.QueryFluids()?.Regions ?? []);
	}

	[Fact]
	public void MidRunRestore_KeepsTheLayerScopedRowsTheCutNames()
	{
		// The other half of the rule above: a mid-run cut names the layer its bodies
		// stand in, and the restore REBUILDS that layer — so its enemy rows and its fluid
		// chunks are that layer's own facts and must come back. Only a cut that names the
		// layer being ENTERED drops them, which is what makes the drop a rule about the
		// cut kind rather than a blanket "never restore these tables".
		using var fixture = WorldSaveFixture.Create("continue-mid-run-layer-tables");
		var enemyId = new EntityId(1UL, 7U, 0);
		Assert.True(fixture.Kernel.TryUpsertEnemy(
			HostId, new EnemyState(enemyId, "spider", 4f, false, false), out _, out _));
		Assert.True(fixture.Kernel.TryUpdateFluidRegion(
			HostId, new FluidRegionState(1, 2, 7, 1, 50), out _, out _));
		SaveMidRun(fixture);

		using var restarted = fixture.Restart("continue-mid-run-layer-tables-restart");
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		Assert.Equal(enemyId, Assert.Single(restarted.Kernel.QueryEnemies()!.Enemies).EntityId);
		Assert.Equal(7, Assert.Single(restarted.Kernel.QueryFluids()!.Regions).TotalAmount);
	}

	// ---- helpers ----

	/// <summary>
	/// Cuts one layer-end snapshot of the fixture's kernel (with an optional host/guest
	/// character) and advances to layer 1. The seeded item is a CARRIED one: a layer-end
	/// cut names the layer being ENTERED, so a world-rooted record would describe the
	/// layer being replaced and the archive does not carry it (§3.4) — the record that
	/// crosses the boundary is the carried one.
	/// </summary>
	private static void SaveLayerEnd(WorldSaveFixture fixture, bool withCharacter, bool guestCharacter = false, bool seedItem = true)
	{
		Assert.True(fixture.Service.TryBeginRun());
		Assert.True(fixture.Kernel.TryStartRun(HostId, WorldSaveCaptureTests.Run(layerIndex: 0), out _, out _));
		if (seedItem)
		{
			Assert.True(fixture.Kernel.TrySpawn(
				HostId,
				new ItemIdentity(100, "bag"),
				ItemLocation.Carried(new ActorId(HostId)),
				new CharacterItemMsg { InstanceId = 100, ItemId = "bag", Condition = 1f },
				out _,
				out _));
		}

		if (withCharacter)
		{
			fixture.Characters.SaveHostCharacterData(WorldSaveCaptureTests.Character(100, "bag"));
		}

		if (guestCharacter)
		{
			fixture.Characters.SaveCharacterData(2002UL, WorldSaveCaptureTests.Character(200, "rope"));
		}

		Assert.True(fixture.Kernel.TryAdvanceLayer(HostId, WorldSaveCaptureTests.Run(layerIndex: 1), out _, out _));

		// The cut is written by the kernel's commit event; the world pointer makes
		// the continue deterministic.
		Assert.True(fixture.Repository.Repository.SetLastOpenedWorld(fixture.WorldId));
	}

	/// <summary>
	/// Cuts the fixture's kernel MID-RUN — so the archive carries the layer-scoped enemy
	/// and fluid rows — and then patches the manifest to name a LAYER-END cut. That is
	/// the shape a layer-end restore has to be robust against: an archive written by a
	/// build that still let those rows through, or by a hand-edited/white-box one. The
	/// encoder drops them for a layer-end cut it writes itself (pinned by
	/// <see cref="WorldSnapshotWorldFactsTests.Encode_LayerEndCut_DropsTheLiveEnemyAndFluidRowsButKeepsTheTombstone"/>),
	/// which is exactly why the restore half cannot be proven through the service's own
	/// cut path. The manifest is not covered by the payload digests, so the patch is the
	/// only edit the loader will not notice — and the restore's job is to be right anyway.
	/// </summary>
	private static void SaveMidRunAsLayerEnd(WorldSaveFixture fixture, string label)
	{
		_ = label;
		SaveMidRun(fixture);
		var manifestPath = Path.Combine(fixture.Repository.Workspace.LiveDirectory(fixture.WorldId), "manifest.json");
		var manifest = File.ReadAllText(manifestPath);
		Assert.Contains("\"mid-run\"", manifest, StringComparison.Ordinal);
		File.WriteAllText(manifestPath, manifest.Replace("\"mid-run\"", "\"layer-end\""));
	}

	/// <summary>
	/// Cuts one MID-RUN snapshot of the fixture's kernel and points the continue pointer
	/// at it. This is the kind that carries IN-LAYER facts: the layer it names is the one
	/// the bodies stand in, so its world items are restored into that same layer.
	/// </summary>
	private static void SaveMidRun(WorldSaveFixture fixture)
	{
		Assert.True(fixture.Service.TryBeginRun());
		Assert.True(fixture.Kernel.TryStartRun(HostId, WorldSaveCaptureTests.Run(layerIndex: 2), out _, out _));
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.MenuReturn, out var refusal), refusal);
		Assert.NotNull(fixture.Service.TryCaptureArmedCut(null, frame: 0));
		Assert.True(fixture.Repository.Repository.SetLastOpenedWorld(fixture.WorldId));
	}
}
