using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The item-data write on a MEMBER's console — the half the host's own rule owns,
/// through the real console, the real kernel protocol and the production
/// composition root (split from <see cref="CommandConsoleItemDataTests"/>, whose
/// cases pin what a write does where the client itself is the authority, both
/// because the member's route has its own two answers and because one class may
/// carry at most <c>TestClassSizeGateTests.MaxCasesPerClass</c> cases).
///
/// <para>
/// Three facts are pinned here, and the third is the reason the member route could
/// not land before it: the host's rule admits the write and the host's own kernel
/// records what the member reported; the rule's OFF posture refuses it by name; and
/// a refused write leaves NO divergence — the value the member had already applied
/// goes back to the session's recorded state, and the operator is told in the
/// console. A fourth case pins the trap that sits beside it: the refusal must not
/// reach the adapter's pickup/block-drop rollback, which would drop an item the
/// member is holding.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class CommandConsoleMemberItemDataTests
{
	private const ulong Backpack = 4101;

	/// <summary>
	/// The admitted half: the member's own report reaches the host, the host's rule
	/// admits it, and the row the host records is the value the member wrote. The
	/// answer says the verdict is still out — the write is applied and reported
	/// first, and the host judges it afterwards — so a member never reads it as a
	/// fact the host already accepted.
	/// </summary>
	[Fact]
	public void Set_OnAMembersConsole_IsReportedAndTheHostRecordsIt()
	{
		var (host, guest, local) = CreateSceneSession(ruleOpen: true);
		Hold(host, local, Item(Backpack, "backpack", slot: 1, condition: 0.5f));

		var answer = Run(guest, $"/data set id {Backpack} condition 0.25");
		Pump(host, guest);

		Assert.Contains("#4101 backpack: condition 0.5 -> 0.25", answer);
		Assert.Contains("reported to the host", answer);
		Assert.Equal(0.25f, local.Held(Backpack)!.Condition);
		Assert.Equal(0.25f, Kernel(host).FindItem(Backpack)!.Value.Data.Condition);
		Assert.Empty(RefusalLines(guest));
	}

	/// <summary>
	/// The refusal half, and the whole reason S1b could not land on its own: with the
	/// host's rule closed (its default), the write is applied locally and reported
	/// like any other, the host refuses it by name, and this client puts the recorded
	/// state back — so the two sides still agree afterwards. Both lines reach the
	/// console, and the order between them is the operator's reading: the verb's own
	/// line first, then the verdict a round trip later. This harness cannot show that
	/// order — `FakeNetwork` delivers with no delay, so the verdict is processed
	/// INSIDE the send call and its line lands before the handler's own — so the case
	/// asserts presence, and the order is stated as production behaviour rather than
	/// pinned here.
	/// </summary>
	[Fact]
	public void Set_OnAMembersConsole_WhenTheHostRuleIsClosed_IsRefusedAndTheValueGoesBack()
	{
		var (host, guest, local) = CreateSceneSession(ruleOpen: false);
		Hold(host, local, Item(Backpack, "backpack", slot: 1, condition: 0.5f));

		var answer = Run(guest, $"/data set id {Backpack} condition 0.25");
		Pump(host, guest);

		Assert.Contains("condition 0.5 -> 0.25", answer);
		Assert.Equal(0.5f, Kernel(host).FindItem(Backpack)!.Value.Data.Condition);
		Assert.Equal(0.5f, local.Held(Backpack)!.Condition);
		var refusal = Assert.Single(RefusalLines(guest));
		Assert.Contains("this host's rule does not open the item-data write verbs", refusal);
		Assert.Contains("#4101 backpack", refusal);
	}

	/// <summary>
	/// The trap beside the refusal: the item-kind mapping in <c>ItemService</c> must
	/// route a console write's refusal to the write family, never onto the pickup or
	/// block-drop reasons — this client HOLDS the item, and the pickup rollback would
	/// take it out of its hands for a report that has nothing to do with a pickup.
	/// </summary>
	[Fact]
	public void Set_OnAMembersConsole_WhenRefused_NeverBecameAPickupRollback()
	{
		var (host, guest, local) = CreateSceneSession(ruleOpen: false);
		var rejected = new List<(ulong ItemId, ItemRejectMsg.Reason Reason)>();
		guest.Services.GetRequiredService<IItemControl>().ItemRejected += (itemId, reason) => rejected.Add((itemId, reason));
		Hold(host, local, Item(Backpack, "backpack", slot: 1, condition: 0.5f));

		Run(guest, $"/data set id {Backpack} condition 0.25");
		Pump(host, guest);

		Assert.Empty(rejected);
		Assert.Equal(0.5f, local.Held(Backpack)!.Condition);
	}

	/// <summary>
	/// A console write names an item the session records, and the record is this
	/// host's own judgement — so an id this host never judged cannot be a console
	/// write at all. The creation-before-operation invariant answers it, and the
	/// answer settles the value the member applied: without that half a modified
	/// client's unrecorded write would stay in its own picture for good.
	/// </summary>
	[Fact]
	public void Set_OnAMembersConsole_ForAnIdTheHostNeverJudged_IsRefusedAndSettled()
	{
		var (host, guest, local) = CreateSceneSession(ruleOpen: true);
		local.Hold(Item(Backpack, "backpack", slot: 1, condition: 0.5f));

		// The record is a fact on BOTH sides, and this one exists only on the member's
		// own kernel: the console resolves it, the host has never judged it.
		SeedCarried(guest, GuestId, Item(Backpack, "backpack", slot: 1, condition: 0.5f));

		var answer = Run(guest, $"/data set id {Backpack} condition 0.25");
		Pump(host, guest);

		Assert.Contains("condition 0.5 -> 0.25", answer);
		Assert.Equal(0.5f, local.Held(Backpack)!.Condition);
		Assert.Contains("no judged item under that id", Assert.Single(RefusalLines(guest)));
	}

	/// <summary>Every console test runs against a scene of its own, and the host's own rule is what the case is about.</summary>
	private static (TestNode Host, TestNode Guest, FakeLocalItemAccess Local) CreateSceneSession(bool ruleOpen)
	{
		var local = new FakeLocalItemAccess();
		var rules = new MutableOptionsMonitor<HostRulesOptions>(
			new HostRulesOptions { AllowMemberItemDataWrite = ruleOpen });
		var (host, guest) = CreateSession(services =>
		{
			services.Replace(ServiceDescriptor.Singleton<ILocalItemAccess>(local));
			services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(rules));
		});
		return (host, guest, local);
	}

	/// <summary>
	/// The write's two halves: the record on the host's authority — which the member's
	/// kernel receives as the committed batch, exactly as a real session delivers it,
	/// so seeding the host IS seeding both sides — and the local object this client
	/// holds.
	/// </summary>
	private static void Hold(TestNode host, FakeLocalItemAccess local, CharacterItemMsg item)
	{
		local.Hold(item);
		SeedCarried(host, GuestId, item);
	}

	private static void SeedCarried(TestNode node, ulong owner, CharacterItemMsg item)
	{
		var seeded = Kernel(node).TrySpawnCarried(owner, item.InstanceId, item.ItemId, item, out _, out var rejection);
		Assert.True(seeded, $"seeding item #{item.InstanceId} for {owner} on node {node.SteamId} was refused: {rejection?.Reason} ({rejection?.Message})");
	}

	private static CharacterItemMsg Item(ulong instanceId, string definition, int slot, float condition) => new()
	{
		InstanceId = instanceId,
		ItemId = definition,
		SlotIndex = slot,
		Condition = condition,
	};

	/// <summary>The production frame pump: each node drains what arrived for it, in the order the plugin's own update loop runs. Twice, because the member's report and the host's answer each cross one link.</summary>
	private static void Pump(TestNode host, TestNode guest)
	{
		for (var round = 0; round < 2; round++)
		{
			host.Update();
			guest.Update();
		}
	}

	/// <summary>The refusal lines the console is showing — the answer that arrives after the verb's own line.</summary>
	private static IReadOnlyList<string> RefusalLines(TestNode node) =>
		[.. node.Services.GetRequiredService<ICommandControl>().Lines
			.Select(line => line.Text)
			.Where(text => text.StartsWith("The host refused the write to ", StringComparison.Ordinal))];

	/// <summary>Runs one console line and answers with the line it printed.</summary>
	private static string Run(TestNode node, string line)
	{
		var console = node.Services.GetRequiredService<ICommandControl>();
		Assert.True(console.TryExecute(line), $"'{line}' was not accepted by the console");
		return console.Lines[console.Lines.Count - 1].Text;
	}

	private static ItemKernelAuthority Kernel(TestNode node) =>
		node.Services.GetRequiredService<ItemKernelAuthority>();
}
