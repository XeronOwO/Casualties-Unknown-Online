using System;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The <c>/summon</c> HOST half through the real console, the real session and the production composition root:
/// the verb asks the scene seam for the position, the seam summons the object and reports it through the family's
/// own landed path (<see cref="FakeWorldSummon"/> documents which one), and the answer names what was created and
/// where. The <c>item</c> type word is Minecraft's shape — the type first, the definition its argument — and the
/// item entity it makes is the same fact and the same world row the give ladder's drop makes.
///
/// <para>
/// The permission cases and the member's request/answer path are
/// <see cref="CommandConsoleMemberWorldSummonTests"/>'s. The scene seam's own half — instantiation, the real
/// prefab tables — is the Game Adapter's and has no test host (`FakeWorldSummon` replaces exactly that); the
/// acceptance batch reads it live.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class CommandConsoleWorldSummonTests
{
	[Fact]
	public void Host_SummonsAnEntity_ReportsTheCreationAndAnswersThePosition()
	{
		var (host, summon) = CreateSummonSession();

		var answer = Run(host, "/summon beartrap player");

		Assert.Equal("Summoned beartrap at (1.0, 2.0).", answer);
		var creation = Assert.Single(Registry(host).Entries);
		Assert.Equal("beartrap", creation.Id);
		Assert.Equal(1f, creation.Position.X);
		Assert.Equal(2f, creation.Position.Y);
		Assert.Equal(WorldSummonAnchor.Player, summon.LastAnchor);
	}

	/// <summary>The pointer is the native default, so an invocation with no position is aimed where the player points.</summary>
	[Fact]
	public void Host_SummonsWithNoPosition_AtThePointer()
	{
		var (host, summon) = CreateSummonSession();

		var answer = Run(host, "/summon medcrate");

		Assert.Equal("Summoned medcrate at (10.0, 20.0).", answer);
		Assert.Equal(WorldSummonAnchor.Cursor, summon.LastAnchor);
	}

	[Fact]
	public void Host_SummonsAtALiteralPoint()
	{
		var (host, summon) = CreateSummonSession();

		var answer = Run(host, "/summon medcrate 12.5,-3");

		Assert.Equal("Summoned medcrate at (12.5, -3.0).", answer);
		Assert.Null(summon.LastAnchor); // a typed point is not a keyword
	}

	/// <summary>The item type word: the definition is the type's argument, and the result is a world item entity.</summary>
	[Fact]
	public void Host_SummonsAnItemEntity_WritesTheWorldRowAndAnswersItsInstanceId()
	{
		var (host, summon) = CreateSummonSession();

		var answer = Run(host, "/summon item bandage player");

		var item = Assert.Single(summon.CreatedItems);
		Assert.Equal($"Summoned bandage (#{item.InstanceId}) at (1.0, 2.0).", answer);
		Assert.True(WorldItems(host).IsWorldItemRegistered(item.InstanceId));
		var row = Kernel(host).FindItem(item.InstanceId);
		Assert.NotNull(row);
		Assert.Equal(ItemLocationKind.World, row!.Value.Location.Kind);
		Assert.Equal(1f, row.Value.Data.Condition);
	}

	/// <summary>The item's state is bound BEFORE the creation (the give's rule): the row and the answer name the state the object really carries.</summary>
	[Fact]
	public void Host_SummonsAnItemEntityWithState_AppliesItBeforeTheCreation()
	{
		var (host, summon) = CreateSummonSession();

		var answer = Run(host, "/summon item bandage player {Condition:0.25f}");

		var item = Assert.Single(summon.CreatedItems);
		Assert.Equal(0.25f, item.Condition);
		Assert.Contains($"Summoned bandage (#{item.InstanceId}) at (1.0, 2.0) with state {{Condition:0.25f,", answer);
		Assert.Equal(0.25f, Kernel(host).FindItem(item.InstanceId)!.Value.Data.Condition);
	}

	/// <summary>
	/// The user's 2026-10-18 requirement: a summoned item entity's SNBT is the give drop's — the SAME bound document
	/// for the same text, because both verbs hand it to the same creation port and the same world-item creation.
	/// The give to a full body answers through its drop rung; the summon answers with the same document.
	/// </summary>
	[Fact]
	public void ASummonedItemEntity_AndAGivenDrop_CarryTheSameStateDocument()
	{
		var (host, summon) = CreateSummonSession();
		Characters(host).SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true,
			Item(4100, "backpack", slot: 0), Item(4101, "bandage", slot: 1), Item(4102, "knife", slot: 2),
			Item(4103, "bandage", slot: 3), Item(4104, "bandage", slot: 4), Item(4105, "bandage", slot: 5)));

		var given = Run(host, "/give @p bandage{Condition:0.25f}");
		var summoned = Run(host, "/summon item bandage player {Condition:0.25f}");

		const string State = "with state {Condition:0.25f,";
		Assert.Contains(State, given);
		Assert.Contains(State, summoned);
		var givenDoc = given.Substring(given.IndexOf(State, StringComparison.Ordinal) + "with state ".Length);
		var summonedDoc = summoned.Substring(summoned.IndexOf(State, StringComparison.Ordinal) + "with state ".Length);
		Assert.Equal(givenDoc, summonedDoc);
		Assert.Single(summon.CreatedItems);
	}

	[Fact]
	public void Host_SummonsAnAnimal_IsAcknowledgedByKeyAndNotRecordedAsAMaterializableCreation()
	{
		var (host, _) = CreateSummonSession();

		var answer = Run(host, "/summon spider player");

		Assert.Equal("Summoned spider at (1.0, 2.0).", answer);
		Assert.Equal(0, Registry(host).Count);
		Assert.Equal(1, Registry(host).AnimalCount);
	}

	[Fact]
	public void Host_SummonsAPrefabTheBuildDoesNotKnow_IsRefusedByName()
	{
		var (host, summon) = CreateSummonSession();

		var answer = Run(host, "/summon notanprefab");

		Assert.Contains("Refused:", answer);
		Assert.Contains("no prefab 'notanprefab'", answer);
		Assert.Equal(0, Registry(host).Count);
		Assert.Empty(WorldItems(host).GetWorldItemsForDiagnostics());
		Assert.Equal(("notanprefab", WorldSummonKind.Entity, ""), Assert.Single(summon.Attempts));
	}

	/// <summary>
	/// The type word is enforced, not guessed: a bare item id is refused with the spelling that works, and
	/// <c>item</c> with an entity prefab is refused by name — before anything is created either way.
	/// </summary>
	[Fact]
	public void Host_SummonsAnItemDefinitionWithoutTheItemWord_IsRefusedWithTheSpelling()
	{
		var (host, summon) = CreateSummonSession();

		var answer = Run(host, "/summon bandage player");

		Assert.Contains("is an item definition, so it is summoned as a loose item entity: /summon item bandage", answer);
		Assert.Empty(summon.CreatedItems);
		Assert.Empty(WorldItems(host).GetWorldItemsForDiagnostics());
	}

	[Fact]
	public void Host_SummonsAnEntityPrefabWithTheItemWord_IsRefusedByName()
	{
		var (host, summon) = CreateSummonSession();

		var answer = Run(host, "/summon item beartrap player");

		Assert.Contains("'/summon item' names an item definition", answer);
		Assert.Empty(summon.CreatedItems);
		Assert.Equal(0, Registry(host).Count);
	}

	[Fact]
	public void Host_SummonsAnEntityWithAState_IsRefusedBeforeAnythingIsCreated()
	{
		var (host, _) = CreateSummonSession();

		var answer = Run(host, "/summon beartrap player {Condition:0.5f}");

		Assert.Contains("takes no state argument", answer);
		Assert.Equal(0, Registry(host).Count);
	}

	/// <summary>The scene's own refusal ("no world", "no body") is answered where the verb was typed, and nothing is created or sent.</summary>
	[Fact]
	public void Host_SummonWhenThePositionCannotBeResolved_IsRefusedByName()
	{
		var (host, summon) = CreateSummonSession();
		summon.AnchorRefusal = "this client is not in a world, so there is no layer to create anything in.";

		var answer = Run(host, "/summon item bandage player");

		Assert.Contains("Refused: this client is not in a world", answer);
		Assert.Empty(summon.Attempts);
		Assert.Empty(WorldItems(host).GetWorldItemsForDiagnostics());
	}

	/// <summary>The host's own standing is the online switch: neither the cheat switch nor a roster entry gates the host's own console.</summary>
	[Fact]
	public void Host_SummonsWhileTheMemberSwitchIsClosed()
	{
		var (host, _) = CreateSummonSession(memberCheatSwitch: false);

		var answer = Run(host, "/summon beartrap player");

		Assert.StartsWith("Summoned beartrap", answer);
		Assert.Equal(1, Registry(host).Count);
	}

	[Theory]
	[InlineData("/summon", "Usage:")]
	[InlineData("/summon item", "/summon item <itemId>")]
	[InlineData("/summon beartrap nowhere", "'nowhere' is not a position")]
	[InlineData("/summon bandage{Condition:0.5f}", "The state is its own argument")]
	[InlineData("/summon beartrap player {Condition:0.5f} extra", "Usage:")]
	public void MalformedSummon_IsRefusedWithItsOwnSentence(string line, string expected)
	{
		var (host, summon) = CreateSummonSession();

		var answer = Run(host, line);

		Assert.Contains(expected, answer);
		Assert.Empty(summon.Attempts);
		Assert.Equal(0, Registry(host).Count);
	}

	private static (TestNode Host, FakeWorldSummon Summon) CreateSummonSession(bool memberCheatSwitch = false)
	{
		var rules = new MutableOptionsMonitor<HostRulesOptions>(
			new HostRulesOptions { AllowMemberCheatVerbs = memberCheatSwitch });
		var (host, _) = CreateSession(services =>
		{
			services.Replace(ServiceDescriptor.Singleton<IWorldSummon>(p => new FakeWorldSummon(
				p.GetRequiredService<IItemControl>(),
				p.GetRequiredService<IWorldControl>(),
				p.GetRequiredService<ISessionControl>())));
			// The give verb's creation port is the fixture's own too: the equivalence case compares the state the
			// two verbs bind, and both must come from the same table and the same bind.
			services.Replace(ServiceDescriptor.Singleton<IItemGrantSource>(new FakeItemGrantSource()));
			services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(rules));
		});

		return (host, (FakeWorldSummon)host.Services.GetRequiredService<IWorldSummon>());
	}

	private static RuntimeEntityRegistry Registry(TestNode node) =>
		node.Services.GetRequiredService<RuntimeEntityRegistry>();

	private static ICharacterDataControl Characters(TestNode node) =>
		node.Services.GetRequiredService<ICharacterDataControl>();

	private static ItemService WorldItems(TestNode node) =>
		node.Services.GetRequiredService<ItemService>();

	private static ItemKernelAuthority Kernel(TestNode node) =>
		node.Services.GetRequiredService<ItemKernelAuthority>();

	/// <summary>Runs one console line and answers with the line it printed.</summary>
	private static string Run(TestNode node, string line)
	{
		var console = node.Services.GetRequiredService<ICommandControl>();
		Assert.True(console.TryExecute(line), $"'{line}' was not accepted by the console");
		return console.Lines[console.Lines.Count - 1].Text;
	}
}
