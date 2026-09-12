using System;
using System.IO;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
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
