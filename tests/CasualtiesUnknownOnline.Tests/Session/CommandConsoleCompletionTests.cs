using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
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
public class CommandConsoleCompletionTests
{
	[Fact]
	public void ArgumentSuggestions_SelectorKind_ReturnsSelectors()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var suggestions = host.Services.GetRequiredService<ICommandArgumentSuggestions>();

		var matches = suggestions.Suggest(CommandArgumentKind.Selector, "@a");

		Assert.Contains(matches, s => s.Text == "@a" && s.Description == "All players");
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

		Assert.Contains(matches, s => s.Text == "cu:player" && !string.IsNullOrWhiteSpace(s.Description));
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

		Assert.Contains("/heal <selector>", hint);
	}

	[Fact]
	public void Suggest_ForHostRulesJsonArgument_ReturnsTemplates()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var suggestions = completion.Suggest("/hostrules {");

		Assert.Contains(suggestions, s => s.Text == "{\"key\": \"value\"}");
	}

	[Fact]
	public void GetHint_ForHostRules_ShowsJsonUsage()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var hint = completion.GetHint("/hostrules");

		Assert.Contains("/hostrules <json>", hint);
	}
}
