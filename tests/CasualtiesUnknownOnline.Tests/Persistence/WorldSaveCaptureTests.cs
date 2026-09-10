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
/// The cut side of S2: the host's run owns a world, the layer advance the kernel
/// commits writes one cut into it (the trigger is the kernel's own commit event,
/// so a cut can never run from a half-applied batch), the deliberate menu return
/// writes one too, and a guest never writes at all.
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
	public void MenuReturn_WritesTheHostCharacterUnderTheSteamKey()
	{
		using var fixture = WorldSaveFixture.Create("save-capture");
		Assert.True(fixture.Service.TryBeginRun());
		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 2), out _, out _));

		Assert.True(fixture.Service.TryCaptureMenuReturnCut(Character(100, "bag")));

		var live = fixture.Repository.Workspace.LiveDirectory(fixture.WorldId);
		var characters = Path.Combine(live, SaveArchiveFormat.CharactersFolderName);
		var file = Assert.Single(Directory.GetFiles(characters));
		Assert.Equal("steam-1001.json", Path.GetFileName(file));
		Assert.Contains("bag", File.ReadAllText(file), StringComparison.Ordinal);
	}

	[Fact]
	public void MenuReturn_InIpDirectMode_WritesTheNameKey()
	{
		using var fixture = WorldSaveFixture.Create("save-capture-ip", ipDirect: true, displayName: "Host Name");
		Assert.True(fixture.Service.TryBeginRun());
		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 0), out _, out _));

		Assert.True(fixture.Service.TryCaptureMenuReturnCut(Character(100, "bag")));

		var characters = Path.Combine(fixture.Repository.Workspace.LiveDirectory(fixture.WorldId), SaveArchiveFormat.CharactersFolderName);
		Assert.Equal("name-host-name.json", Path.GetFileName(Assert.Single(Directory.GetFiles(characters))));
	}

	[Fact]
	public void Capture_WithoutARunOrAWorld_WritesNothing()
	{
		using var fixture = WorldSaveFixture.Create("save-capture");

		// No run started yet: the world exists but holds no baseline to restore into.
		Assert.True(fixture.Service.TryBeginRun());
		Assert.False(fixture.Service.TryCaptureMenuReturnCut(Character(100, "bag")));
		Assert.Empty(CutFilesOf(fixture));

		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 0), out _, out _));
		using var noWorld = WorldSaveFixture.Create("save-capture-noworld");
		Assert.True(noWorld.Kernel.TryStartRun(HostId, Run(layerIndex: 0), out _, out _));
		Assert.False(noWorld.Service.TryCaptureMenuReturnCut(null));
	}

	[Fact]
	public void GuestRole_NeverWrites()
	{
		using var fixture = WorldSaveFixture.Create("save-capture");
		fixture.Session.Role = SessionRole.Guest;

		Assert.False(fixture.Service.TryBeginRun());
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
			NullLoggerFactory.Instance,
			NullLogger<WorldSaveService>.Instance);

		Assert.False(service.IsEnabled);
		Assert.False(service.HasRestorableWorld);
		Assert.False(service.TryBeginRun());
		Assert.True(kernel.TryStartRun(HostId, Run(layerIndex: 0), out _, out _));
		Assert.False(service.TryCaptureMenuReturnCut(null));
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

	internal static RunState Run(int layerIndex) =>
		new(RunId, [1, 2, 3, 4], 0, 2, 10, false, [new RunSetting("speed", RunSettingKind.Float, FloatValue: 1.5f)], layerIndex);

	internal static CharacterDataMsg Character(ulong instanceId, string definitionId) =>
		new() { SlotCount = 5, Items = [new CharacterItemMsg { InstanceId = instanceId, ItemId = definitionId, Condition = 0.9f }] };

	internal static int LiveRunLayer(string liveDirectory)
	{
		var bytes = File.ReadAllBytes(Path.Combine(liveDirectory, SaveArchiveFormat.RunFileName));
		using var document = JsonDocument.Parse(bytes);
		return document.RootElement.EnumerateArray().Single().GetProperty("layerIndex").GetInt32();
	}
}
