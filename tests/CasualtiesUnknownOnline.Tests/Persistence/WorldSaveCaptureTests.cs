using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The cut side of S2/S3: the host's run owns a world, the layer advance the
/// kernel commits writes one cut into it (the trigger is the kernel's own commit
/// event, so a cut can never run from a half-applied batch), the deliberate menu
/// return writes one at the frame-end seam (armed first, taken by the pump), and
/// a guest never writes at all.
/// </summary>
public class WorldSaveCaptureTests
{
	private const ulong HostId = 1001UL;
	private const ulong RunId = 42UL;

	[Fact]
	public void TheRunsFirstCut_MakesItsWorldTheContinueTarget_EvenWhenAnotherWorldIsChosen()
	{
		using var fixture = WorldSaveFixture.Create("save-capture");
		var repository = fixture.Repository.Repository;

		// A world the player played earlier — a REAL snapshot through the service's own cut
		// path — and the picker's current selection, so it IS the Continue target here.
		var olderWorld = PlayAnEarlierWorld(fixture);
		Assert.True(repository.SetLastOpenedWorld(olderWorld));
		Assert.Equal(olderWorld, repository.LastOpenedWorldId);
		Assert.Equal(olderWorld, fixture.Service.ContinueWorldId);

		// A new run: the folder is created, but it holds no snapshot yet, so the target
		// must NOT move — an aborted start cannot hide the previous world behind a
		// folder the Continue entry cannot open.
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		var worldId = fixture.WorldId;
		Assert.True(SaveArchiveFormat.IsWorldId(worldId));
		Assert.True(Directory.Exists(repository.PathOfWorld(worldId)));
		Assert.Equal(olderWorld, repository.LastOpenedWorldId);
		Assert.Equal(olderWorld, fixture.Service.ContinueWorldId);

		// The FIRST committed cut is the moment the world being played becomes the
		// target: after the player leaves, Continue must open the run they just left,
		// not whatever world the picker last selected.
		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 0), out _, out _));
		Assert.True(fixture.Kernel.TryAdvanceLayer(HostId, Run(layerIndex: 1), out _, out _));
		Assert.Equal(worldId, repository.LastOpenedWorldId);
		Assert.Equal(worldId, fixture.Service.ContinueWorldId);

		// It moves on the FIRST cut only: a picker choice made after it still stands,
		// and the run's next cut may not steal the selection back. The RAW pointer is the
		// assertion that bites: ContinueWorldId alone answers the same world either way,
		// because both worlds carry a snapshot.
		Assert.True(repository.SetLastOpenedWorld(olderWorld));
		Assert.True(fixture.Kernel.TryAdvanceLayer(HostId, Run(layerIndex: 2), out _, out _));
		Assert.Equal(olderWorld, repository.LastOpenedWorldId);
	}

	[Fact]
	public void ARefusedCut_DoesNotMoveTheContinueTarget()
	{
		using var fixture = WorldSaveFixture.Create("save-refused-cut");
		var repository = fixture.Repository.Repository;

		// The picker's current selection is an earlier world with a REAL snapshot.
		var olderWorld = PlayAnEarlierWorld(fixture);
		Assert.True(repository.SetLastOpenedWorld(olderWorld));

		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));

		// The kernel holds no run baseline, so the writer refuses: an attempt that wrote
		// no snapshot must leave the Continue target exactly where it was.
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.Command, out var refusal), refusal);
		var report = Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(null, frame: 0));
		Assert.Equal(WorldCutResult.Refused, report.Result);

		Assert.Equal(olderWorld, repository.LastOpenedWorldId);
		Assert.Equal(olderWorld, fixture.Service.ContinueWorldId);
	}

	[Fact]
	public void TryBeginRun_Tutorial_GetsNoArchive_AndReleasesThePreviousRun()
	{
		using var fixture = WorldSaveFixture.Create("save-capture");
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		var runWorldId = fixture.Service.CurrentWorldId;
		Assert.True(SaveArchiveFormat.IsWorldId(runWorldId));

		// The tutorial entry: the save layer refuses it AND releases the previous
		// run's identity, so a /save fired during the tutorial can never rewrite the
		// previous run's snapshot, and nothing here becomes a Continue target.
		Assert.False(fixture.Service.TryBeginRun(isTutorial: true));

		Assert.Equal(string.Empty, fixture.Service.CurrentWorldId);
		Assert.False(fixture.Service.TryRequestCut(WorldCutReason.Command, out var refusal));
		Assert.Contains("world", refusal, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void LayerAdvance_WritesACutForTheLayerBeingEntered()
	{
		using var fixture = WorldSaveFixture.Create("save-capture");
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 0), out _, out _));

		// The kernel's own commit is the trigger: the cut is taken AFTER the layer
		// advance committed, so it stores the baseline of the layer entered.
		Assert.True(fixture.Kernel.TryAdvanceLayer(HostId, Run(layerIndex: 1), out _, out _));

		var worldId = fixture.WorldId;
		var live = fixture.Repository.Workspace.LiveDirectory(worldId);
		Assert.Equal(1, LiveRunLayer(live));
		var backup = Assert.Single(Directory.GetFiles(fixture.Repository.Workspace.BackupsDirectory(worldId), "*.cuoz"));
		Assert.Contains("layer-end-", Path.GetFileName(backup), StringComparison.Ordinal);
	}

	[Fact]
	public void MenuReturnCut_WritesTheHostCharacterUnderTheSteamKey()
	{
		using var fixture = WorldSaveFixture.Create("save-capture");
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 2), out _, out _));

		var report = MenuReturnCut(fixture, Character(100, "bag"));

		// The menu return is a MID-RUN cut now: the world is fully alive at that
		// moment, so its in-layer facts ride the snapshot and the phase names the
		// seam that took it.
		Assert.True(report.Captured);
		Assert.Equal(WorldCutReason.MenuReturn, report.Reason);
		Assert.Contains(WorldSaveService.FrameEndCutPhase, LiveManifest(fixture).GetProperty("cutPhase").GetString()!, StringComparison.Ordinal);
		Assert.Equal("mid-run", LiveManifest(fixture).GetProperty("kind").GetString());

		var live = fixture.Repository.Workspace.LiveDirectory(fixture.WorldId);
		var characters = Path.Combine(live, SaveArchiveFormat.CharactersFolderName);
		var file = Assert.Single(Directory.GetFiles(characters));
		Assert.Equal("steam-1001.json", Path.GetFileName(file));
		Assert.Contains("bag", File.ReadAllText(file), StringComparison.Ordinal);
	}

	[Fact]
	public void MenuReturnCut_MenuReturnPhaseIsRecordedOnTheManifest()
	{
		using var fixture = WorldSaveFixture.Create("save-capture-phase");
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 0), out _, out _));

		MenuReturnCut(fixture, null);

		var manifest = LiveManifest(fixture);
		Assert.Equal("menu-return", manifest.GetProperty("saveReason").GetString());
		Assert.Equal(
			(long)fixture.Kernel.CreateCheckpoint().GlobalRevision,
			manifest.GetProperty("globalRevision").GetInt64());
	}

	[Fact]
	public void MenuReturnCut_InIpDirectMode_WritesTheNameKey()
	{
		using var fixture = WorldSaveFixture.Create("save-capture-ip", ipDirect: true, displayName: "Host Name");
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 0), out _, out _));

		MenuReturnCut(fixture, Character(100, "bag"));

		var characters = Path.Combine(fixture.Repository.Workspace.LiveDirectory(fixture.WorldId), SaveArchiveFormat.CharactersFolderName);
		Assert.Equal("name-host-name.json", Path.GetFileName(Assert.Single(Directory.GetFiles(characters))));
	}

	[Fact]
	public void Capture_WithoutARunOrAWorld_WritesNothing()
	{
		using var fixture = WorldSaveFixture.Create("save-capture");

		// No run started yet: the world exists but holds no baseline to restore into.
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.Command, out _));
		Assert.False(fixture.Service.TryCaptureArmedCut(Character(100, "bag"), frame: 0)!.Captured);
		Assert.Empty(CutFilesOf(fixture));

		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 0), out _, out _));
		using var noWorld = WorldSaveFixture.Create("save-capture-noworld");
		Assert.True(noWorld.Kernel.TryStartRun(HostId, Run(layerIndex: 0), out _, out _));
		Assert.False(noWorld.Service.TryRequestCut(WorldCutReason.Command, out var refusal));
		Assert.Contains("no CUO world", refusal!, StringComparison.Ordinal);
	}

	[Fact]
	public void GuestRole_NeverWrites()
	{
		using var fixture = WorldSaveFixture.Create("save-capture");
		fixture.Session.Role = SessionRole.Guest;

		Assert.False(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.False(fixture.Service.TryRequestCut(WorldCutReason.Command, out var refusal));
		Assert.Contains("guest", refusal!, StringComparison.Ordinal);
		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 0), out _, out _));
		Assert.True(fixture.Kernel.TryAdvanceLayer(HostId, Run(layerIndex: 1), out _, out _));

		// The guest's own gestures are refused AND the host-side commit that would
		// have written a cut for a host wrote nothing: no world carries a snapshot.
		Assert.Empty(CutFilesOf(fixture));
		Assert.False(Directory.Exists(fixture.Repository.Workspace.LiveDirectory(fixture.Repository.WorldId)));
	}

	/// <summary>Every committed cut (backup archive) of the fixture's world; empty when the folder was never written.</summary>
	private static IReadOnlyList<string> CutFilesOf(WorldSaveFixture fixture)
	{
		var directory = fixture.Repository.Workspace.BackupsDirectory(fixture.WorldId);
		return Directory.Exists(directory) ? Directory.GetFiles(directory, "*.cuoz") : [];
	}

	[Fact]
	public void DisabledRepository_ReportsNoWorldAndWritesNothing()
	{
		var characters = new FakeCharacterDataControl();
		var kernel = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
		using var service = new WorldSaveService(
			null,
			new FakeSessionControl(),
			characters,
			kernel,
			new FakeTransportIdentity(),
			new WorldSnapshotEncoder(NullLogger<WorldSnapshotEncoder>.Instance),
			new FakeWorldFactSource(),
			new ModContentStore(),
			NullLoggerFactory.Instance,
			NullLogger<WorldSaveService>.Instance);

		Assert.False(service.IsEnabled);
		Assert.False(service.HasRestorableWorld);
		Assert.False(service.TryBeginRun(isTutorial: false));
		Assert.False(service.TryRequestCut(WorldCutReason.Command, out var refusal));
		Assert.Contains("no CUO world repository", refusal!, StringComparison.Ordinal);
		Assert.False(service.HasArmedCut);
		Assert.Null(service.TryCaptureArmedCut(null, frame: 0));
		Assert.True(kernel.TryStartRun(HostId, Run(layerIndex: 0), out _, out _));
		Assert.False(service.TryContinue(out var outcome));
		Assert.False(outcome.Started);
		Assert.Contains("no world repository", outcome.Summary, StringComparison.Ordinal);
	}

	[Fact]
	public void LayerAdvance_CarriesEveryPresentMembersCharacter()
	{
		using var fixture = WorldSaveFixture.Create("save-capture");
		fixture.Session.AddMember(2002UL, "Guest");
		fixture.Characters.SaveCharacterData(2002UL, Character(200, "rope"));
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 0), out _, out _));
		fixture.Characters.SaveHostCharacterData(Character(100, "bag"));

		Assert.True(fixture.Kernel.TryAdvanceLayer(HostId, Run(layerIndex: 1), out _, out _));

		var characters = Path.Combine(fixture.Repository.Workspace.LiveDirectory(fixture.WorldId), SaveArchiveFormat.CharactersFolderName);
		Assert.Equal(2, Directory.GetFiles(characters).Length);
		Assert.True(File.Exists(Path.Combine(characters, "steam-1001.json")));
		Assert.True(File.Exists(Path.Combine(characters, "steam-2002.json")));
	}

	[Fact]
	public void Cut_RecordsTheContentSetThisProcessMaterialized()
	{
		// §3.2's `contentFingerprint` had no producer: every cut wrote the empty string
		// ("unknown"), so nothing could tell that two saves — or two peers — materialize
		// different content under one id and one mod version. The cut records what the mod
		// domain holds, which is what lets a load compare it.
		using var fixture = WorldSaveFixture.Create("save-capture-content");
		fixture.Content.Add("test.contentmod", "testns", new ModItemDefinition { Id = "kept.item" });
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 0), out _, out _));

		Assert.True(fixture.Kernel.TryAdvanceLayer(HostId, Run(layerIndex: 1), out _, out _));

		var recorded = LiveManifest(fixture).GetProperty("contentFingerprint").GetString();
		Assert.Equal(fixture.Content.Fingerprint, recorded);
		Assert.NotEmpty(recorded!);

		// The value follows the ENTRIES: a content set that changed is a different value,
		// which is the whole point of recording it.
		fixture.Content.Add("test.contentmod", "testns", new ModItemDefinition { Id = "added.later" });
		Assert.NotEqual(recorded, fixture.Content.Fingerprint);
	}

	// ---- fixture ----

	/// <summary>
	/// Play one earlier run into its own world through the service's own cut path, so the
	/// picker's selection names a world a Continue could genuinely open: a folder whose
	/// manifest exists but whose payload the decoder refuses is not a continuable world.
	/// </summary>
	internal static string PlayAnEarlierWorld(WorldSaveFixture fixture)
	{
		using var earlier = fixture.Restart("save-capture-earlier");
		Assert.True(earlier.Service.TryBeginRun(isTutorial: false));
		var olderWorld = earlier.WorldId;
		Assert.True(earlier.Kernel.TryStartRun(HostId, Run(layerIndex: 0), out _, out _));
		Assert.True(earlier.Kernel.TryAdvanceLayer(HostId, Run(layerIndex: 1), out _, out _));
		return olderWorld;
	}

	/// <summary>Take a cut the way the pump does: arm it, then take it at the frame-end seam.</summary>
	internal static WorldCutReport MenuReturnCut(WorldSaveFixture fixture, CharacterDataMsg? character, int frame = 0)
	{
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.MenuReturn, out var refusal), refusal);
		var report = fixture.Service.TryCaptureArmedCut(character, frame);
		return Assert.IsType<WorldCutReport>(report);
	}

	/// <summary>The live snapshot's manifest as JSON.</summary>
	internal static JsonElement LiveManifest(WorldSaveFixture fixture)
	{
		var path = Path.Combine(fixture.Repository.Workspace.LiveDirectory(fixture.WorldId), SaveArchiveFormat.ManifestFileName);
		using var document = JsonDocument.Parse(File.ReadAllBytes(path));
		return document.RootElement.Clone();
	}

	internal static RunState Run(int layerIndex) =>
		new(RunId, [1, 2, 3, 4], 0, 2, 10, false, [new RunSetting("speed", RunSettingKind.Float, FloatValue: 1.5f)], layerIndex);

	internal static CharacterDataMsg Character(ulong instanceId, string definitionId) =>
		new() { SlotCount = 5, Items = [new CharacterItemMsg { InstanceId = instanceId, ItemId = definitionId, Condition = 0.9f }] };

	internal static int LiveRunLayer(string liveDirectory)
	{
		var bytes = File.ReadAllBytes(Path.Combine(liveDirectory, SaveArchiveFormat.RunFileName));
		using var document = JsonDocument.Parse(bytes);

		// run.json carries typed rows: the kernel baseline and, when the cut could
		// capture them, the native run fields. The layer index lives on the baseline.
		return document.RootElement.EnumerateArray()
			.Single(row => row.GetProperty("kind").GetString() == SaveRunRow.RunKind)
			.GetProperty("run")
			.GetProperty("layerIndex")
			.GetInt32();
	}
}
