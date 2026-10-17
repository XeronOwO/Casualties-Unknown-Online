using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The item-data family's PATH reads and list operations through the real console: one value
/// by path, the refusal that names what the compound carries, and the applied append/prepend/
/// insert cases the verb grammar promises. Split from
/// <see cref="CommandConsoleItemDataTests"/> at the class-size gate.
/// </summary>
[Trait("Category", "Integration")]
public class CommandConsoleItemDataPathTests
{
	private const ulong Backpack = 4101;

	[Fact]
	public void Get_WithAPath_ReadsThatValue()
	{
		var (host, _, local) = CreateSceneSession();
		Hold(host, local, Item(Backpack, "backpack", slot: 1, withState: true));

		var answer = Run(host, $"/data get id {Backpack} Liquids[0].Amount");

		Assert.Contains("#4101 backpack: Liquids[0].Amount = 250f", answer);
	}

	[Fact]
	public void Get_WithAMissingPath_NamesTheKeysItCarries()
	{
		var (host, _, local) = CreateSceneSession();
		Hold(host, local, Item(Backpack, "backpack", slot: 1, withState: true));

		var answer = Run(host, $"/data get id {Backpack} Components.Nope.charge");

		Assert.Contains("has no 'Nope'", answer);
		Assert.Contains("it carries: BatteryItem", answer);
	}

	[Theory]
	[InlineData("Liquids append {LiquidId:'blood',Amount:50f}")]
	[InlineData("Liquids prepend {LiquidId:'blood',Amount:50f}")]
	[InlineData("Liquids insert 0 {LiquidId:'blood',Amount:50f}")]
	public void ListOperations_ApplyAndSayWhatChanged(string tail)
	{
		var (host, _, local) = CreateSceneSession();
		Hold(host, local, Item(Backpack, "backpack", slot: 1, withState: true));

		var answer = Run(host, $"/data modify id {Backpack} {tail}");

		Assert.Contains("inserted", answer);
		Assert.Equal(2, local.Held(Backpack)!.Liquids.Count);
		Assert.Contains(local.Held(Backpack)!.Liquids, liquid => liquid.LiquidId == "blood");
	}


	private static ItemKernelAuthority Kernel(TestNode node) =>
		node.Services.GetRequiredService<ItemKernelAuthority>();

	private static (TestNode Host, TestNode Guest, FakeLocalItemAccess Local) CreateSceneSession()
	{
		var local = new FakeLocalItemAccess();
		var (host, guest) = CreateSession(services => services.Replace(ServiceDescriptor.Singleton<ILocalItemAccess>(local)));
		return (host, guest, local);
	}

	private static void Hold(TestNode host, FakeLocalItemAccess local, CharacterItemMsg item)
	{
		local.Hold(item);
		Assert.True(Kernel(host).TrySpawnCarried(HostId, item.InstanceId, item.ItemId, item, out _, out _));
	}

	private static CharacterItemMsg Item(
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

		return item;
	}

	private static string Run(TestNode node, string line)
	{
		var console = node.Services.GetRequiredService<ICommandControl>();
		Assert.True(console.TryExecute(line), $"'{line}' was not accepted by the console");
		return console.Lines[console.Lines.Count - 1].Text;
	}
}
