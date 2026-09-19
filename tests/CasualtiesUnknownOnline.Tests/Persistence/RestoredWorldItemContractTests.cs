using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The Runtime half of the host-side restore rebuild. A restored cut's world
/// items are the truth for the layer the host regenerates, so the restore arms
/// an expectation (publish suppressed, the set readable) that exactly one of two
/// things ends: the generation's reconcile (<see cref="ItemService.CompleteRestoredWorldItems"/>)
/// or a cancellation that names the loss (<see cref="ItemService.CancelRestoredWorldItems"/>).
///
/// The live half — whether the game's objects actually landed at the restored
/// spots — is the adapter's (<c>GeneratedItemAuthority</c> + <c>GeneratedItemReconcile</c>)
/// and needs a running game.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RestoredWorldItemContractTests
{
	private const ulong HostId = 1001;

	[Fact]
	public void ARestoredCut_ArmsTheReconcileAndPublishesTheRestoredSet()
	{
		using var w = ItemSimWorld.Create();
		var kernel = Kernel(w.Host);
		var items = w.Host.Services.GetRequiredService<ItemService>();

		RestoreOneShell(kernel, 100, 5f, 5f);

		Assert.True(items.RestoredWorldItemsPending); // the cut is authoritative until the generation reconciles it
		Assert.Equal(100UL, Assert.Single(items.ReadRestoredWorldItems()).ItemId);
		Assert.True(w.HostTable(100)); // the world table IS the restored set — guests snapshot from it
	}

	[Fact]
	public void ACompletedReconcile_ReportsTheItemHalfAndClearsTheExpectation()
	{
		using var w = ItemSimWorld.Create();
		var kernel = Kernel(w.Host);
		var items = w.Host.Services.GetRequiredService<ItemService>();
		var audit = w.Host.Services.GetRequiredService<WorldRestoreAudit>();
		var reported = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reported.Add;
		// The production order: the click restores the kernel (which arms the item set
		// with that attempt's sequence) and THEN opens the restore's account for the
		// same attempt.
		RestoreOneShell(kernel, 100, 5f, 5f);
		audit.BeginRestore("w-1", kernel.RestoreSequence, [WorldRestoreHalf.WorldFacts, WorldRestoreHalf.WorldItems]);
		audit.LiveWriteFinished(WorldRestoreHalf.WorldFacts, kernel.RestoreSequence, complete: true, refused: [], summary: "the world facts landed");

		items.CompleteRestoredWorldItems(applied: 1, refused: []);

		Assert.False(items.RestoredWorldItemsPending);
		var report = Assert.Single(reported); // the item half completed the restore
		Assert.True(report.Complete);
		Assert.Equal("w-1", report.WorldId);
		Assert.Contains("restored item set", report.Summary, StringComparison.Ordinal);
	}

	[Fact]
	public void ARefusedReconcile_ReportsTheItemHalfAsIncomplete()
	{
		using var w = ItemSimWorld.Create();
		var kernel = Kernel(w.Host);
		var items = w.Host.Services.GetRequiredService<ItemService>();
		var audit = w.Host.Services.GetRequiredService<WorldRestoreAudit>();
		var reported = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reported.Add;
		RestoreOneShell(kernel, 100, 5f, 5f);
		audit.BeginRestore("w-2", kernel.RestoreSequence, [WorldRestoreHalf.WorldFacts, WorldRestoreHalf.WorldItems]);
		audit.LiveWriteFinished(WorldRestoreHalf.WorldFacts, kernel.RestoreSequence, complete: true, refused: [], summary: "the world facts landed");

		items.CompleteRestoredWorldItems(applied: 0, refused: ["item #100 (shell) at (5.0,5.0)"]);

		var report = Assert.Single(reported);
		Assert.False(report.Complete);
		Assert.Equal("item #100 (shell) at (5.0,5.0)", Assert.Single(report.Refused));
	}

	[Fact]
	public void ACancelledReconcile_ReportsTheLossInsteadOfWaitingForever()
	{
		using var w = ItemSimWorld.Create();
		var kernel = Kernel(w.Host);
		var items = w.Host.Services.GetRequiredService<ItemService>();
		var audit = w.Host.Services.GetRequiredService<WorldRestoreAudit>();
		var reported = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reported.Add;
		RestoreOneShell(kernel, 100, 5f, 5f);
		audit.BeginRestore("w-3", kernel.RestoreSequence, [WorldRestoreHalf.WorldFacts, WorldRestoreHalf.WorldItems]);
		audit.LiveWriteFinished(WorldRestoreHalf.WorldFacts, kernel.RestoreSequence, complete: true, refused: [], summary: "the world facts landed");

		items.CancelRestoredWorldItems("the session ended before the generation reconcile ran");

		Assert.False(items.RestoredWorldItemsPending);
		var report = Assert.Single(reported);
		Assert.False(report.Complete);
		Assert.Contains("generation reconcile", Assert.Single(report.Refused), StringComparison.Ordinal);
	}

	[Fact]
	public void ACancelWithoutARestore_IsANoOp()
	{
		using var w = ItemSimWorld.Create();
		var items = w.Host.Services.GetRequiredService<ItemService>();
		var audit = w.Host.Services.GetRequiredService<WorldRestoreAudit>();
		var reports = 0;
		audit.Reported += _ => reports++;

		items.CancelRestoredWorldItems("nothing was armed");

		Assert.False(items.RestoredWorldItemsPending);
		Assert.Equal(0, reports); // no restore in flight — nothing to report and nothing to wait for
	}

	[Fact]
	public void TheRestoreGeneration_KeepsTheRestoredWorldTable()
	{
		// The generation a restore drives is NOT a new layer: the restored set IS that
		// layer's world table. Clearing it here would leave the host with an empty table
		// for the whole layer, and the suppressed publish would never rebuild it — so
		// every host consumer (snapshots, crafting lookups, the position stream) would
		// see no world items at all.
		using var w = ItemSimWorld.Create();
		var kernel = Kernel(w.Host);
		var items = w.Host.Services.GetRequiredService<ItemService>();
		RestoreOneShell(kernel, 100, 5f, 5f);

		items.ResetItems(); // the generation boundary (GameAdapterBridge.OnWorldGenerate)

		Assert.True(items.RestoredWorldItemsPending);
		Assert.True(w.HostTable(100));
	}

	[Fact]
	public void ANormalGeneration_StillClearsTheWorldTable()
	{
		using var w = ItemSimWorld.Create();
		var items = w.Host.Services.GetRequiredService<ItemService>();
		items.PublishGeneratedItems(
		[
			new WorldItem(200, new CharacterItemMsg { ItemId = "shell" }, new NetVector2(5f, 5f), NetVector2.Zero, 0, 0f, false),
		]);
		Assert.True(w.HostTable(200));

		items.ResetItems();

		Assert.False(w.HostTable(200)); // the layer is new and its table starts empty
	}

	[Fact]
	public void ASessionEnd_EndsTheItemExpectationThroughTheServicesOwnSubscription()
	{
		// The service's OWN SessionEnded subscription ends the expectation when a session
		// tears down — not a call from the restore path, and not something the save layer
		// does for it — so this pins the WIRING rather than the release method.
		using var w = ItemSimWorld.Create();
		var kernel = Kernel(w.Host);
		var items = w.Host.Services.GetRequiredService<ItemService>();
		var audit = w.Host.Services.GetRequiredService<WorldRestoreAudit>();
		var reported = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reported.Add;
		RestoreOneShell(kernel, 100, 5f, 5f);
		audit.BeginRestore("w-4", kernel.RestoreSequence, [WorldRestoreHalf.WorldFacts, WorldRestoreHalf.WorldItems]);
		audit.LiveWriteFinished(WorldRestoreHalf.WorldFacts, kernel.RestoreSequence, complete: true, refused: [], summary: "the world facts landed");

		w.Host.Session.EndSession();

		Assert.False(items.RestoredWorldItemsPending);
		var report = Assert.Single(reported);
		Assert.False(report.Complete);
		Assert.Contains("session ended", Assert.Single(report.Refused), StringComparison.Ordinal);
	}

	private static ItemKernelAuthority Kernel(TestNode node) =>
		node.Services.GetRequiredService<ItemKernelAuthority>();

	private static void RestoreOneShell(ItemKernelAuthority kernel, ulong id, float x, float y)
	{
		Assert.True(kernel.TrySpawn(
			HostId,
			new ItemIdentity(id, "shell"),
			ItemLocation.World(x, y),
			new CharacterItemMsg { ItemId = "shell" },
			out _,
			out _));
		Assert.True(kernel.Restore(kernel.CreateCheckpoint()).Success);
	}
}
