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
/// The console's SNBT completion: the schema walk itself (keys, component types, fields,
/// booleans, liquid ids) and the command wiring that picks the schema — a definition's prefab
/// for <c>/give</c>, the recorded item's own capture for <c>/data merge</c>. The schema comes
/// from the adapter port, so what a suggestion offers is what the capture really carries.
/// </summary>
public class ItemStateCompletionTests
{
	private const string Q = "\"";

	private const ulong Battery = 4101;

	[Fact]
	public void RootKeys_AreOfferedWithTheirColon()
	{
		var schema = FakeItemState.DefinitionSchema(FakeItemState.WaterBottle)!;

		Assert.True(SnbtCompletion.TryFragments("{", schema.Root, out var prefix, out var fragments));

		Assert.Equal("{", prefix);
		Assert.Contains(fragments, fragment => fragment.Text == "Condition:");
		Assert.Contains(fragments, fragment => fragment.Text == "Favourited:");
		Assert.Contains(fragments, fragment => fragment.Text == "Liquids:");
		Assert.Contains(fragments, fragment => fragment.Text == "Components:");
	}

	[Fact]
	public void AKeyAlreadyWritten_IsNotOfferedAgain()
	{
		var schema = FakeItemState.DefinitionSchema(FakeItemState.WaterBottle)!;

		Assert.True(SnbtCompletion.TryFragments("{Condition:1,", schema.Root, out var prefix, out var fragments));

		Assert.Equal("{Condition:1,", prefix);
		Assert.DoesNotContain(fragments, fragment => fragment.Text == "Condition:");
		Assert.Contains(fragments, fragment => fragment.Text == "Favourited:");
	}

	[Fact]
	public void ABooleanValue_OffersTrueAndFalse()
	{
		var schema = FakeItemState.DefinitionSchema(FakeItemState.WaterBottle)!;

		Assert.True(SnbtCompletion.TryFragments("{Favourited:", schema.Root, out _, out var fragments));

		Assert.Contains(fragments, fragment => fragment.Text == "true");
		Assert.Contains(fragments, fragment => fragment.Text == "false");
	}

	[Fact]
	public void ComponentTypes_AndTheirFields_AreOfferedFromTheItem()
	{
		var schema = FakeItemState.DefinitionSchema(FakeItemState.LargeBattery)!;

		Assert.True(SnbtCompletion.TryFragments("{Components:{", schema.Root, out var typesPrefix, out var types));
		Assert.Equal("{Components:{", typesPrefix);
		Assert.Contains(types, fragment => fragment.Text == "BatteryItem:");

		Assert.True(SnbtCompletion.TryFragments("{Components:{BatteryItem:{", schema.Root, out var fieldsPrefix, out var fields));
		Assert.Equal("{Components:{BatteryItem:{", fieldsPrefix);
		Assert.Contains(fields, fragment => fragment.Text == "charge:");
		Assert.Contains(fields, fragment => fragment.Text == "on:");

		Assert.True(SnbtCompletion.TryFragments("{Components:{BatteryItem:{cha", schema.Root, out var partialPrefix, out var partial));
		Assert.Equal("{Components:{BatteryItem:{", partialPrefix);
		Assert.Single(partial);
		Assert.Equal("charge:", partial[0].Text);
	}

	[Fact]
	public void LiquidStackKeys_AndTheLiquidIds_AreOffered()
	{
		var schema = FakeItemState.DefinitionSchema(FakeItemState.WaterBottle)!;

		Assert.True(SnbtCompletion.TryFragments("{Liquids:[{", schema.Root, out _, out var keys));
		Assert.Contains(keys, fragment => fragment.Text == "LiquidId:");
		Assert.Contains(keys, fragment => fragment.Text == "Amount:");

		Assert.True(SnbtCompletion.TryFragments("{Liquids:[{LiquidId:'wa", schema.Root, out var idPrefix, out var ids));
		Assert.Equal("{Liquids:[{LiquidId:", idPrefix);
		Assert.Single(ids);
		Assert.Equal(Q + "water" + Q, ids[0].Text);

		Assert.True(SnbtCompletion.TryFragments("{Liquids:[{LiquidId:'water',", schema.Root, out _, out var remaining));
		Assert.DoesNotContain(remaining, fragment => fragment.Text == "LiquidId:");
		Assert.Contains(remaining, fragment => fragment.Text == "Amount:");
	}

	[Fact]
	public void ANumberPosition_OffersNothing()
	{
		var schema = FakeItemState.DefinitionSchema(FakeItemState.WaterBottle)!;

		Assert.True(SnbtCompletion.TryFragments("{Condition:", schema.Root, out _, out var fragments));
		Assert.Empty(fragments);
	}

	[Fact]
	public void GiveCompletion_AsksTheDefinitionsSchema()
	{
		var (host, _) = CreateSession();
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var suggestions = completion.Suggest("/give @p waterbottle{Condition:0.5,Liq");

		var suggestion = Assert.Single(suggestions, candidate => candidate.Text == "waterbottle{Condition:0.5,Liquids:");
		Assert.True(suggestion.Verbatim, "a state suggestion is inserted verbatim, not re-quoted");
	}

	[Fact]
	public void SetCompletion_WalksTheRootExactlyAsMergeDoes()
	{
		// /data set writes the whole state, so its value completes against the root: before this the verb was missing from the state
		// source, which left one of the three write verbs typed blind.
		var (host, _) = CreateSession();
		var battery = new CharacterItemMsg
		{
			InstanceId = Battery,
			ItemId = FakeItemState.LargeBattery,
			SlotIndex = 1,
			Condition = 1f,
			Components =
			[
				new ComponentStateMsg
				{
					TypeName = FakeItemState.BatteryItem,
					Fields =
					[
						new ComponentFieldMsg { Name = FakeItemState.Charge, Kind = 1, FloatValue = 0.75f },
						new ComponentFieldMsg { Name = FakeItemState.On, Kind = 3, BoolValue = true },
					],
				},
			],
		};
		var kernel = host.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.True(kernel.TrySpawnCarried(HostId, battery.InstanceId, battery.ItemId, battery, out _, out _));

		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();
		var suggestions = completion.Suggest($"/data set id {Battery} {{Components:{{BatteryItem:{{cha");

		Assert.Contains(suggestions, candidate => candidate.Text == "{Components:{BatteryItem:{charge:");
	}

	[Fact]
	public void DataCompletion_AsksTheRecordedItemsSchema()
	{
		var (host, _) = CreateSession();
		var battery = new CharacterItemMsg
		{
			InstanceId = Battery,
			ItemId = FakeItemState.LargeBattery,
			SlotIndex = 1,
			Condition = 1f,
			Components =
			[
				new ComponentStateMsg
				{
					TypeName = FakeItemState.BatteryItem,
					Fields =
					[
						new ComponentFieldMsg { Name = FakeItemState.Charge, Kind = 1, FloatValue = 0.75f },
						new ComponentFieldMsg { Name = FakeItemState.On, Kind = 3, BoolValue = true },
					],
				},
			],
		};
		var kernel = host.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.True(kernel.TrySpawnCarried(HostId, battery.InstanceId, battery.ItemId, battery, out _, out _));

		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();
		var suggestions = completion.Suggest($"/data merge id {Battery} {{Components:{{BatteryItem:{{cha");

		var suggestion = Assert.Single(suggestions, candidate => candidate.Text == "{Components:{BatteryItem:{charge:");
		Assert.True(suggestion.Verbatim);
	}

	[Fact]
	public void ModifyCompletion_WalksThePathsShapeNotTheRoot()
	{
		var (host, _) = CreateSession();
		var kernel = host.Services.GetRequiredService<ItemKernelAuthority>();
		var bottle = new CharacterItemMsg
		{
			InstanceId = Battery,
			ItemId = "waterbottle",
			SlotIndex = 1,
			Condition = 0.5f,
			Liquids = [new LiquidStackMsg { LiquidId = "water", Amount = 250f }],
		};
		Assert.True(kernel.TrySpawnCarried(HostId, bottle.InstanceId, bottle.ItemId, bottle, out _, out _));

		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();
		var suggestions = completion.Suggest($"/data modify id {Battery} Liquids append {{");

		Assert.Contains(suggestions, candidate => candidate.Text.EndsWith("{LiquidId:", StringComparison.Ordinal));
		Assert.DoesNotContain(suggestions, candidate => candidate.Text.EndsWith("{Condition:", StringComparison.Ordinal));
	}

	[Fact]
	public void GiveCompletion_OnAnUnknownDefinition_OffersNothing()
	{
		var (host, _) = CreateSession();
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		Assert.Empty(completion.Suggest("/give @p notanitem{Condition:"));
	}

	[Fact]
	public void GiveCompletion_AfterAResolvedId_OpensTheStateBlock()
	{
		// The user's 2026-10-30 report: with the caret right after the id nothing completed at all. The correction (2026-10-31) is that the block is OPENED, not
		// pasted: one candidate inserts the brace, and the next keystroke completes the keys inside it.
		var (host, _) = CreateSession();
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var onTheId = Assert.Single(completion.Suggest("/give @p waterbottle"));
		Assert.True(onTheId.Verbatim, "opening a state is one command-line token");
		Assert.Equal("waterbottle{", onTheId.Text);

		// The typed id form is kept: a player who wrote the namespace must not have it rewritten by accepting the brace.
		var namespaced = Assert.Single(completion.Suggest("/give @p cu:waterbottle"));
		Assert.Equal("cu:waterbottle{", namespaced.Text);

		var afterTheId = completion.Suggest("/give @p waterbottle ");
		var opened = Assert.Single(afterTheId, candidate => candidate.Text == "{");
		Assert.True(opened.Verbatim, "the brace is inserted as typed");
		Assert.Contains(afterTheId, candidate => candidate.Text == "slot");
	}

	[Fact]
	public void GiveCompletion_InsideTheOpenedBlock_OffersTheNextKeys()
	{
		// The token-by-token grammar the user asked for: after the brace, the keys of THIS item's schema — not a template with values filled in.
		var (host, _) = CreateSession();
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var suggestions = completion.Suggest("/give @p waterbottle{");

		Assert.Contains(suggestions, candidate => candidate.Text.EndsWith("{Condition:", StringComparison.Ordinal));
		Assert.Contains(suggestions, candidate => candidate.Text.EndsWith("{Liquids:", StringComparison.Ordinal));
		Assert.DoesNotContain(suggestions, candidate => candidate.Text.Contains(" false", StringComparison.Ordinal));
	}
}

