using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The console's <c>/gamerule</c> verb (Minecraft's <c>gamerule &lt;rule name&gt; [&lt;value&gt;]</c>
/// shape): the query, the set (through the editor seam, keyed by the rule id and taking the canonical
/// value), and every refusal — an unknown rule, a value the kind refuses, the numeric range, a surplus
/// argument, and the host-only gate. The value grammar itself is
/// <see cref="HostRuleValueTextTests"/>'s subject; here the verb's own answers are read.
/// </summary>
[Trait("Category", "Integration")]
public class CommandConsoleCommandTests
{
	[Fact]
	public void GameRule_Query_AnswersTheValueInForce()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/gamerule remote_backpack_access"));

		Assert.Contains(console.Lines, line => line.Text.Contains("Game rule remote_backpack_access is currently set to: allow."));
	}

	[Fact]
	public void GameRule_Set_AppliesThroughTheEditorById()
	{
		var editor = new StubHostRulesEditor();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: services => services.Replace(ServiceDescriptor.Singleton<IHostRulesEditor>(editor)));
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/gamerule remote_backpack_access unconscious_or_dead"));

		Assert.Contains((HostRuleId.RemoteBackpackAccess, "unconscious_or_dead"), editor.Applied);
		Assert.Contains(console.Lines, line => line.Text.Contains("Game rule remote_backpack_access is now set to: unconscious_or_dead."));
	}

	[Fact]
	public void GameRule_SetNumber_WritesTheCanonicalValue()
	{
		var editor = new StubHostRulesEditor();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: services => services.Replace(ServiceDescriptor.Singleton<IHostRulesEditor>(editor)));
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/gamerule piggyback_weight_multiplier 0.50"));

		Assert.Contains((HostRuleId.PiggybackWeightMultiplier, "0.5"), editor.Applied);
	}

	[Fact]
	public void GameRule_InvalidValue_NamesTheChoicesAndWritesNothing()
	{
		var editor = new StubHostRulesEditor();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: services => services.Replace(ServiceDescriptor.Singleton<IHostRulesEditor>(editor)));
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/gamerule remote_backpack_access maybe"));

		Assert.Contains(console.Lines, line => line.Text.Contains("'maybe' is not one of allow, unconscious_or_dead, deny."));
		Assert.Empty(editor.Applied);
	}

	[Fact]
	public void GameRule_NumberOutOfRange_NamesTheRangeAndWritesNothing()
	{
		var editor = new StubHostRulesEditor();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: services => services.Replace(ServiceDescriptor.Singleton<IHostRulesEditor>(editor)));
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/gamerule piggyback_weight_multiplier 5"));

		Assert.Contains(console.Lines, line => line.Text.Contains("'5' is not a number between 0 and 3."));
		Assert.Empty(editor.Applied);
	}

	[Fact]
	public void GameRule_UnknownRule_ListsTheKnownNames()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/gamerule nope"));

		Assert.Contains(console.Lines, line => line.Text.Contains("Unknown game rule 'nope'."));
		Assert.Contains(console.Lines, line => line.Text.Contains("remote_backpack_access") && line.Text.Contains("piggyback_weight_multiplier"));
	}

	[Fact]
	public void GameRule_WithoutARuleName_ShowsUsage()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/gamerule"));

		Assert.Contains(console.Lines, line => line.Text.Contains("Usage: /gamerule <rule name> [<value>]"));
	}

	[Fact]
	public void GameRule_SurplusArgument_ShowsUsageAndWritesNothing()
	{
		var editor = new StubHostRulesEditor();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: services => services.Replace(ServiceDescriptor.Singleton<IHostRulesEditor>(editor)));
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/gamerule pvp_enabled true junk"));

		Assert.Contains(console.Lines, line => line.Text.Contains("Usage: /gamerule <rule name> [<value>]"));
		Assert.Empty(editor.Applied);
	}

	[Fact]
	public void GameRule_WhenTheEditorRefuses_AnswersWithItsReason()
	{
		var editor = new StubHostRulesEditor { Refusal = "the configuration could not be saved" };
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: services => services.Replace(ServiceDescriptor.Singleton<IHostRulesEditor>(editor)));
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/gamerule pvp_enabled true"));

		Assert.Contains(console.Lines, line => line.Text.Contains("Game rule pvp_enabled was not changed: the configuration could not be saved"));
	}

	[Fact]
	public void GameRule_OnAMember_IsRefusedHostOnly()
	{
		var editor = new StubHostRulesEditor();
		var (_, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: services => services.Replace(ServiceDescriptor.Singleton<IHostRulesEditor>(editor)));
		var console = guest.Services.GetRequiredService<ICommandControl>();

		Assert.False(console.TryExecute("/gamerule pvp_enabled true"));

		Assert.Contains(console.Lines, line => line.Kind == ConsoleLineKind.Error && line.Text.Contains("host-only"));
		Assert.Empty(editor.Applied);
	}
}
