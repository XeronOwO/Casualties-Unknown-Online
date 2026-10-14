using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The <c>/data get</c> verb through the real console and the real kernel: what a
/// target resolves to, what the answer names, and every refusal. The wording of
/// the rows themselves is pinned by <see cref="ItemDataRendererTests"/>, and the
/// grammar by <see cref="ItemDataQueryParserTests"/>; this class is the wiring
/// those two cannot prove.
/// </summary>
[Trait("Category", "Integration")]
public class CommandConsoleItemDataTests
{
	private const ulong Backpack = 4101;

	private const ulong Bandage = 4102;

	private const ulong GuestsBandage = 4201;

	[Fact]
	public void Get_WithoutATarget_ListsTheCallersOwnItemsInSlotOrder()
	{
		var (host, _) = CreateSession();
		SeedCarried(host, HostId, Bandage, "bandage", slot: 0);
		SeedCarried(host, HostId, Backpack, "backpack", slot: 1);

		var answer = Run(host, "/data get");

		Assert.Contains("Items recorded for 1001 (2):", answer);
		Assert.Contains("#4101 backpack [slot 1]", answer);
		Assert.True(
			answer.IndexOf("#4102", StringComparison.Ordinal) < answer.IndexOf("#4101", StringComparison.Ordinal),
			$"the listing follows slot order: {answer}");
	}

	[Fact]
	public void Get_ByInstanceId_PrintsEveryProperty()
	{
		var (host, _) = CreateSession();
		SeedCarried(host, HostId, Backpack, "backpack", slot: 1, condition: 0.8f, favourited: true, withState: true);

		var answer = Run(host, $"/data get id {Backpack}");

		Assert.Contains("#4101 backpack [carried by 1001, slot 1] revision", answer);
		Assert.Contains("condition 0.8", answer);
		Assert.Contains("favourited yes", answer);
		Assert.Contains("liquid water 250 ml", answer);
		Assert.Contains("component BatteryItem: float charge=0.75", answer);
	}

	[Fact]
	public void Get_BySlot_ReadsThatSlotOnly()
	{
		var (host, _) = CreateSession();
		SeedCarried(host, HostId, Bandage, "bandage", slot: 0);
		SeedCarried(host, HostId, Backpack, "backpack", slot: 1);

		var answer = Run(host, "/data get slot 1");

		Assert.Contains("#4101 backpack", answer);
		Assert.DoesNotContain("#4102", answer);
	}

	[Fact]
	public void Get_ForASelectedBody_ReadsThatBodysItems()
	{
		var (host, _) = CreateSession();
		SeedHostEntities(host, GuestId, 5f);
		SeedCarried(host, HostId, Backpack, "backpack", slot: 1);
		SeedCarried(host, GuestId, GuestsBandage, "bandage", slot: 2);

		var answer = Run(host, "/data get @a");

		Assert.Contains("Items recorded for 2001 (1):", answer);
		Assert.Contains("#4201 bandage [slot 2]", answer);
		Assert.DoesNotContain("#4101", answer);
	}

	[Fact]
	public void Get_ForASelectedBodySlot_ReadsThatBodysSlot()
	{
		var (host, _) = CreateSession();
		SeedHostEntities(host, GuestId, 5f);
		SeedCarried(host, GuestId, GuestsBandage, "bandage", slot: 2);

		var answer = Run(host, $"/data get @a slot 2");

		Assert.Contains("#4201 bandage [carried by 2001, slot 2]", answer);
	}

	[Fact]
	public void Get_NamesAContainerContentItRecords()
	{
		var (host, _) = CreateSession();
		SeedCarried(host, HostId, Backpack, "backpack", slot: 1);
		var parent = new CharacterItemMsg { InstanceId = Backpack, ItemId = "backpack", SlotIndex = 1 };
		parent.Contents.Add(new CharacterItemMsg { InstanceId = GuestsBandage, ItemId = "bandage", SlotIndex = 1 });
		Assert.True(Kernel(host).TrySyncContainerFacts(HostId, parent, out _, out _));

		var answer = Run(host, $"/data get id {Backpack}");

		Assert.Contains("contents (1): #4201 bandage", answer);
	}

	[Fact]
	public void Get_WithAnUnknownInstanceId_IsRefusedByName()
	{
		var (host, _) = CreateSession();

		Assert.Contains("No item #999999 is recorded in this session.", Run(host, "/data get id 999999"));
	}

	[Fact]
	public void Get_WithAnEmptySlot_IsRefusedByName()
	{
		var (host, _) = CreateSession();
		SeedCarried(host, HostId, Backpack, "backpack", slot: 1);

		Assert.Contains("No item is carried by 1001 in slot 7.", Run(host, "/data get slot 7"));
	}

	[Fact]
	public void Get_ForASelectorNoBaseMatches_IsRefusedByName()
	{
		var (host, _) = CreateSession();

		Assert.Contains("No players match selector '@x'.", Run(host, "/data get @x"));
	}

	[Fact]
	public void Get_ForABodyThatCarriesNothing_IsRefusedByName()
	{
		var (host, _) = CreateSession();
		SeedHostEntities(host, GuestId, 5f);

		Assert.Contains("No item is recorded for 2001.", Run(host, "/data get @a"));
	}

	/// <summary>
	/// The caller's own body is the one empty answer that needs the reason: a
	/// carried item joins the record — and gets the id a target names — on the
	/// holding client's first report, which for the host is its first use, move or
	/// drop. Without that sentence an operator reads "you carry nothing" from an
	/// empty record.
	/// </summary>
	[Fact]
	public void Get_ForTheCallersOwnBodyWithNothingRecorded_SaysWhyItIsEmpty()
	{
		var (host, _) = CreateSession();

		var answer = Run(host, "/data get");

		Assert.Contains("No item is recorded for 1001 yet", answer);
		Assert.Contains("first report of it", answer);
	}

	[Theory]
	[InlineData("/data")]
	[InlineData("/data nonsense")]
	[InlineData("/data get 4101")]
	[InlineData("/data get slot -1")]
	public void Data_WithSomethingThatIsNotATarget_AnswersWithTheUsage(string line)
	{
		var (host, _) = CreateSession();

		var answer = Run(host, line);

		Assert.Contains("Usage:", answer);
		Assert.Contains("/data get", answer);
	}

	[Fact]
	public void Data_WithAWriteVerb_SaysWhichHalfLanded()
	{
		var (host, _) = CreateSession();

		var answer = Run(host, "/data set id 4101 condition 0.5");

		Assert.Contains("/data set is not implemented yet", answer);
	}

	[Fact]
	public void Help_ListsTheDataCommand()
	{
		var (host, _) = CreateSession();

		Assert.Contains("/data", Run(host, "/help"));
	}

	private static ItemKernelAuthority Kernel(TestNode node) =>
		node.Services.GetRequiredService<ItemKernelAuthority>();

	private static void SeedCarried(
		TestNode node,
		ulong owner,
		ulong instanceId,
		string definition,
		int slot,
		float condition = 1f,
		bool favourited = false,
		bool withState = false)
	{
		var item = new CharacterItemMsg
		{
			InstanceId = instanceId,
			ItemId = definition,
			SlotIndex = slot,
			Condition = condition,
			Favourited = favourited,
		};

		if (withState)
		{
			item.Liquids = [new LiquidStackMsg { LiquidId = "water", Amount = 250f }];
			item.Components =
			[
				new ComponentStateMsg
				{
					TypeName = "BatteryItem",
					Fields = [new ComponentFieldMsg { Name = "charge", Kind = 1, FloatValue = 0.75f }],
				},
			];
		}

		Assert.True(Kernel(node).TrySpawnCarried(owner, instanceId, definition, item, out _, out _));
	}

	/// <summary>Runs one console line and answers with the line it printed.</summary>
	private static string Run(TestNode node, string line)
	{
		var console = node.Services.GetRequiredService<ICommandControl>();
		Assert.True(console.TryExecute(line), $"'{line}' was not accepted by the console");
		return console.Lines[console.Lines.Count - 1].Text;
	}
}
