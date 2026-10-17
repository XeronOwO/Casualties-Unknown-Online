using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
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
/// because the member's route has its own answers and because one class may
/// carry at most <c>TestClassSizeGateTests.MaxCasesPerClass</c> cases).
///
/// <para>
/// The host announces its rules to every member, so this class pins the three
/// postures a member's console can be in. The announced rule is OPEN: the write is
/// applied locally and reported, and the host's own kernel records it. The announced
/// rule is CLOSED: the write is refused HERE, before the value is written into the
/// local object — the operator used to watch it appear and go back, which is the
/// flicker this branch removes. Nothing was announced: the member applies, reports and
/// is settled by the host's own answer, exactly as it did before the rules were
/// replicated — the local check is an interception, and with no announcement there is
/// nothing to intercept on.
/// </para>
///
/// <para>
/// A fourth fact rides beside them: the refusal must not reach the adapter's
/// pickup/block-drop rollback, which would drop an item the member is holding.
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
	/// The ROSTER ruling through the same console, and the case that tells the two apart
	/// locally: with the switch OPEN and no grant the answer is the roster's sentence —
	/// not the switch's — and nothing is applied or reported. Every other case in this
	/// class is granted, which is why this one asks for <c>grant: false</c>.
	/// </summary>
	[Fact]
	public void Set_OnAMembersConsole_WhenTheSwitchIsOpenButTheMemberIsNotGranted_IsRefusedByTheRoster()
	{
		var (host, guest, local) = CreateSceneSession(ruleOpen: true, grant: false);
		Hold(host, local, Item(Backpack, "backpack", slot: 1, condition: 0.5f));

		var answer = Run(guest, $"/data set id {Backpack} condition 0.25");
		Pump(host, guest);

		Assert.Contains("Refused locally", answer);
		Assert.Contains("cheat roster", answer);
		Assert.DoesNotContain("cheat switch", answer);
		Assert.Equal(0.5f, local.Held(Backpack)!.Condition);
		Assert.Empty(RefusalLines(guest));
	}

	/// <summary>
	/// The refusal half, and the behaviour the rule announcement exists for: with the
	/// host's rule closed AND announced, the write is refused on the member's own client
	/// before anything is applied — the local object still holds the old value, nothing
	/// was reported, and the host never had to refuse anything. Before the announcement
	/// this same command applied the value, reported it, and put it back when the
	/// verdict came: the operator watched it appear and go back.
	/// </summary>
	[Fact]
	public void Set_OnAMembersConsole_WhenTheAnnouncedRuleIsClosed_IsRefusedBeforeAnythingIsApplied()
	{
		var (host, guest, local) = CreateSceneSession(ruleOpen: false);
		Hold(host, local, Item(Backpack, "backpack", slot: 1, condition: 0.5f));

		var answer = Run(guest, $"/data set id {Backpack} condition 0.25");
		Pump(host, guest);

		Assert.Contains("Refused locally", answer);
		Assert.Contains("AllowMemberCheatVerbs", answer);
		Assert.Equal(0.5f, local.Held(Backpack)!.Condition);
		Assert.Equal(0.5f, Kernel(host).FindItem(Backpack)!.Value.Data.Condition);
		Assert.Empty(RefusalLines(guest));
	}

	/// <summary>
	/// The same command under an ANNOUNCED open rule: the local check is not a second
	/// gate — it refuses only what the announced rule forbids, so a write the host allows
	/// still lands, and it lands with the host's own kernel recording it.
	/// </summary>
	[Fact]
	public void Set_OnAMembersConsole_WhenTheAnnouncedRuleIsOpen_IsAppliedAndReported()
	{
		var (host, guest, local) = CreateSceneSession(ruleOpen: true);
		Hold(host, local, Item(Backpack, "backpack", slot: 1, condition: 0.5f));

		var answer = Run(guest, $"/data set id {Backpack} condition 0.25");
		Pump(host, guest);

		Assert.Contains("condition 0.5 -> 0.25", answer);
		Assert.Equal(0.25f, local.Held(Backpack)!.Condition);
		Assert.Equal(0.25f, Kernel(host).FindItem(Backpack)!.Value.Data.Condition);
		Assert.Empty(RefusalLines(guest));
	}

	/// <summary>
	/// A member that never received the set — a swallowed announcement, or a build
	/// without the message — keeps the behaviour it had before the rules were replicated:
	/// the value is applied and reported, the host refuses it by name, and this client
	/// puts the recorded state back, so the two sides still agree. The local check is
	/// skipped rather than guessed, because a member's own config under the same name is
	/// not the host's rule.
	/// </summary>
	[Fact]
	public void Set_OnAMembersConsole_WhenNothingWasAnnounced_IsSettledByTheHostsOwnRefusal()
	{
		var (host, guest, local) = CreateSceneSession(ruleOpen: false, announce: false);
		Hold(host, local, Item(Backpack, "backpack", slot: 1, condition: 0.5f));

		var answer = Run(guest, $"/data set id {Backpack} condition 0.25");
		Pump(host, guest);

		Assert.Contains("condition 0.5 -> 0.25", answer);
		Assert.Equal(0.5f, Kernel(host).FindItem(Backpack)!.Value.Data.Condition);
		Assert.Equal(0.5f, local.Held(Backpack)!.Condition);
		var refusal = Assert.Single(RefusalLines(guest));
		Assert.Contains("this host's cheat switch is closed to members", refusal);
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

	/// <summary>
	/// Every console test runs against a scene of its own. TWO host rulings are in play
	/// and each case says which one it exercises: <paramref name="ruleOpen"/> is the
	/// cheat SWITCH (announced with the host rules), and <paramref name="grant"/> is the
	/// host's cheat ROSTER (announced separately). <paramref name="announce"/> false is
	/// the member that never received the rules — the frame is SWALLOWED on the
	/// host→member link, the lazy peer session's own failure — so the case exercises the
	/// real absence rather than a silenced sender; the precondition is asserted here so
	/// no case can silently run under the other one.
	/// </summary>
	private static (TestNode Host, TestNode Guest, FakeLocalItemAccess Local) CreateSceneSession(bool ruleOpen, bool announce = true, bool grant = true)
	{
		var local = new FakeLocalItemAccess();
		var rules = new MutableOptionsMonitor<HostRulesOptions>(
			new HostRulesOptions { AllowMemberCheatVerbs = ruleOpen });
		var (host, guest) = CreateSession(
			services =>
			{
				services.Replace(ServiceDescriptor.Singleton<ILocalItemAccess>(local));
				services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(rules));
			},
			swallowHostToGuest: announce ? null : NetMsg.HostRules);

		var announced = guest.Services.GetRequiredService<AnnouncedHostRules>().Current;
		Assert.Equal(announce, announced is not null);

		// The roster is the OTHER ruling. A case that exercises the switch is granted, so
		// the switch is the only closed ground; the one case about the roster itself asks
		// for no grant. The roster is a separate message, so swallowing the RULES
		// announcement cannot swallow it.
		if (grant)
		{
			GrantAndAnnounceCheatVerbs(host, guest);
		}

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
