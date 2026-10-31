using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.Content;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Command console family: argument completion and hint surfaces (selector/json/resource-location kinds).
/// Split from the former single class so xUnit v2 (serial inside a class)
/// does not serialize this whole surface in one collection.
/// </summary>
[Trait("Category", "Integration")]
public class CommandConsoleCompletionTests
{
	[Fact]
	public void Suggest_ForAnItemIdArgument_OffersTheCanonicalFormTheVerbAccepts()
	{
		// User direction 2026-10-30: /give and /summon resolve BOTH forms (the bare id the game names and the canonical namespace:path), so the list offers the
		// canonical id — the form every other surface shows — and accepting it runs. The test host registers one content entry, which is enough to read the projection.
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId, extraRegistrations: services =>
			services.AddSingleton<IResourceLocationSource>(new StubResourceSource(
				new ResourceLocationEntry(ContentId.Parse("cu:widget"), ModContentKind.Item, "Widget"))));
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		Assert.Contains(completion.Suggest("/give @p wid"), s => s.Text == "cu:widget");
		Assert.DoesNotContain(completion.Suggest("/give @p wid"), s => s.Text == "widget");

		// The kind's own endpoint still speaks the canonical form, because it is not tied to a verb.
		var kinds = host.Services.GetRequiredService<ICommandArgumentSuggestions>();
		Assert.Contains(kinds.Suggest(CommandArgumentKind.ResourceLocation, "cu:wid"), s => s.Text == "cu:widget");
	}

	[Fact]
	public void Suggest_ForLaterArgumentsOfGiveAndSummon_OffersTheWordsTheirParsersAccept()
	{
		// Every position with a vocabulary, not only the first: the summon position word, the give slot keyword, and the state of a
		// summoned item (the same component schema a give carries, because a loose item entity IS that item).
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		Assert.Contains(completion.Suggest("/summon cu:aed "), s => s.Text == "cursor");
		Assert.Contains(completion.Suggest("/summon cu:aed "), s => s.Text == "player");
		Assert.Contains(completion.Suggest("/summon item cu:aed "), s => s.Text == "random");

		Assert.Contains(completion.Suggest("/give @p cu:aed "), s => s.Text == "slot");
	}

	[Fact]
	public void Suggest_ForGamemodeModeArgument_OffersBothModes()
	{
		// A closed word set: the verb's own accepted values are the candidates, so the argument is not typed blind.
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var all = completion.Suggest("/gamemode ");
		Assert.Contains(all, s => s.Text == "survivor");
		Assert.Contains(all, s => s.Text == "spectator");

		var narrowed = completion.Suggest("/gamemode spec");
		Assert.Contains(narrowed, s => s.Text == "spectator");
		Assert.DoesNotContain(narrowed, s => s.Text == "survivor");
	}

	[Fact]
	public void Suggest_AfterTheVerbWithAnEmptyPrefix_ShowsTheArgumentsChoices()
	{
		// The state a player is in the instant they type the space after the verb: the token under the caret is EMPTY, and the list
		// has to show the choices rather than nothing — the reported "I typed a command and saw no completion suggestion" is this exact
		// moment, because every argument source filtered by prefix and an empty prefix matched nothing.
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var selectors = completion.Suggest("/heal ");
		Assert.Contains(selectors, s => s.Text == "@a");
		Assert.Contains(selectors, s => s.Text == "@p");

		var targets = completion.Suggest("/give ");
		Assert.NotEmpty(targets);
	}

	[Fact]
	public void Suggest_ForAnEmptySelectorPrefix_ListsTheSelectorsInTheUiToo()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var suggestions = host.Services.GetRequiredService<ICommandArgumentSuggestions>();

		var matches = suggestions.Suggest(CommandArgumentKind.Selector, "");

		Assert.Contains(matches, s => s.Text == "@a");
		Assert.Contains(matches, s => s.Text == "@e");
	}

	[Fact]
	public void ArgumentSuggestions_SelectorKind_ReturnsSelectors()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var suggestions = host.Services.GetRequiredService<ICommandArgumentSuggestions>();

		var matches = suggestions.Suggest(CommandArgumentKind.Selector, "@a");

		Assert.Contains(matches, s => s.Text == "@a" && s.Hint == CommandSuggestionHint.SelectorAllPlayers);
	}

	[Fact]
	public void ArgumentSuggestions_JsonKind_ReturnsTemplates()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var suggestions = host.Services.GetRequiredService<ICommandArgumentSuggestions>();

		var matches = suggestions.Suggest(CommandArgumentKind.Json, "{\"k");

		Assert.Contains(matches, s => s.Text == "{\"key\": \"value\"}");
	}

	[Fact]
	public void ArgumentSuggestions_ResourceLocationKind_ReturnsCatalog()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var suggestions = host.Services.GetRequiredService<ICommandArgumentSuggestions>();

		var matches = suggestions.Suggest(CommandArgumentKind.ResourceLocation, "cu:");

		Assert.Contains(matches, s => s.Text == "cu:player" && s.Hint == CommandSuggestionHint.ResourceLocation);
	}

	[Fact]
	public void ArgumentSuggestions_ResourceLocationKind_CompletesModContentByBareIdAndNamespace()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var suggestions = host.Services.GetRequiredService<ICommandArgumentSuggestions>();

		var byBareId = suggestions.Suggest(CommandArgumentKind.ResourceLocation, "wooden");
		var byNamespace = suggestions.Suggest(CommandArgumentKind.ResourceLocation, "testcontent:wood");

		Assert.Contains(byBareId, s => s.Text == "testcontent:wooden.sword");
		Assert.Contains(byNamespace, s => s.Text == "testcontent:wooden.sword");
	}

	[Fact]
	public void Suggest_ForHealSelectorArgument_ReturnsSelectors()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var suggestions = completion.Suggest("/heal @");

		Assert.Contains(suggestions, s => s.Text == "@a");
		Assert.Contains(suggestions, s => s.Text == "@p");
	}

	[Fact]
	public void Suggest_ForHealSelectorBracket_ReturnsFilterKeys()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var suggestions = completion.Suggest("/heal @a[");

		Assert.Contains(suggestions, s => s.Text == "@a[type=");
		Assert.Contains(suggestions, s => s.Text == "@a[name=");
		Assert.Contains(suggestions, s => s.Text == "@a[sort=");
	}

	[Fact]
	public void GetHint_ForHeal_ShowsSelectorUsage()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var hint = completion.GetHint("/heal");

		Assert.Equal("console.hint.usage", hint!.Key);
		Assert.Contains("/heal <selector>", hint.Args[0] as string);
	}

	[Fact]
	public void GetHint_ForGameRule_ShowsRuleUsage()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var hint = completion.GetHint("/gamerule");

		Assert.Equal("console.hint.usage", hint!.Key);
		Assert.Contains("/gamerule <rule name> [<value>]", hint.Args[0] as string);
	}
}
