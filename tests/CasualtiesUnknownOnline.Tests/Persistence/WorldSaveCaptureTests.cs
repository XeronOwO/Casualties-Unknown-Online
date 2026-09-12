using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
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
	public void TryBeginRun_CreatesAWorldAndPointsTheIndexAtIt()
	{
		using var fixture = WorldSaveFixture.Create("save-capture");

		Assert.True(fixture.Service.TryBeginRun());

		// The run's world is a NEW folder (the repository's own seed world predates
		// it), but nothing is continuable yet: the folder holds no snapshot, and the
		// picker pointer does not move until a cut exists — an aborted start must not
		// hide the previous world behind an empty folder, nor offer a Continue entry
		// that cannot open anything.
		var worldId = fixture.WorldId;
		Assert.True(SaveArchiveFormat.IsWorldId(worldId));
		Assert.True(Directory.Exists(fixture.Repository.Repository.PathOfWorld(worldId)));
		Assert.False(fixture.Service.HasRestorableWorld);
		Assert.Null(fixture.Service.ContinueWorldId);

		// The first committed cut makes it continuable.
		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 0), out _, out _));
		Assert.True(fixture.Kernel.TryAdvanceLayer(HostId, Run(layerIndex: 1), out _, out _));
		Assert.True(fixture.Service.HasRestorableWorld);
		Assert.Equal(worldId, fixture.Service.ContinueWorldId);
	}

	[Fact]
	public void LayerAdvance_WritesACutForTheLayerBeingEntered()
	{
		using var fixture = WorldSaveFixture.Create("save-capture");
		Assert.True(fixture.Service.TryBeginRun());
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
		Assert.True(fixture.Service.TryBeginRun());
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
		Assert.True(fixture.Service.TryBeginRun());
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
		Assert.True(fixture.Service.TryBeginRun());
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
		Assert.True(fixture.Service.TryBeginRun());
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

		Assert.False(fixture.Service.TryBeginRun());
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
			NullLoggerFactory.Instance,
			NullLogger<WorldSaveService>.Instance);

		Assert.False(service.IsEnabled);
		Assert.False(service.HasRestorableWorld);
		Assert.False(service.TryBeginRun());
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
		Assert.True(fixture.Service.TryBeginRun());
		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 0), out _, out _));
		fixture.Characters.SaveHostCharacterData(Character(100, "bag"));

		Assert.True(fixture.Kernel.TryAdvanceLayer(HostId, Run(layerIndex: 1), out _, out _));

		var characters = Path.Combine(fixture.Repository.Workspace.LiveDirectory(fixture.WorldId), SaveArchiveFormat.CharactersFolderName);
		Assert.Equal(2, Directory.GetFiles(characters).Length);
		Assert.True(File.Exists(Path.Combine(characters, "steam-1001.json")));
		Assert.True(File.Exists(Path.Combine(characters, "steam-2002.json")));
	}

	// ---- fixture ----

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
