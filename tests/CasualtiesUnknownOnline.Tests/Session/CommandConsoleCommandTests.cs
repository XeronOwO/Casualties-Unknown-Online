using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The console's <c>/gamerule</c> verb on the HOST (Minecraft's <c>gamerule &lt;rule name&gt;
/// [&lt;value&gt;]</c> shape): the query, the set (through the editor seam, keyed by the rule id and
/// taking the canonical value), and the grammar's refusals — an unknown rule, a value the kind refuses,
/// the numeric range, a surplus argument, and the editor's own refusal. The gate and the member's half are
/// <see cref="CommandConsoleMemberGameRuleTests"/>'s subject; the value grammar itself is
/// <see cref="HostRuleValueTextTests"/>'s; here the host verb's own answers are read.
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
		var (host, editor, _) = HostWithRuleEntries();
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/gamerule remote_backpack_access unconscious_or_dead"));

		Assert.Contains((HostRuleId.RemoteBackpackAccess, "unconscious_or_dead"), editor.Applied);
		Assert.Contains(console.Lines, line => line.Text.Contains("Game rule remote_backpack_access is now set to: unconscious_or_dead."));
		Assert.Equal(RemoteBackpackAccess.UnconsciousOrDeadOnly, host.Services.GetRequiredService<IHostRules>().RemoteBackpackAccess);
	}

	[Fact]
	public void GameRule_SetNumber_WritesTheCanonicalValue()
	{
		var (host, editor, rules) = HostWithRuleEntries();
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/gamerule piggyback_weight_multiplier 0.50"));

		Assert.Contains((HostRuleId.PiggybackWeightMultiplier, "0.5"), editor.Applied);
		Assert.Equal(0.5f, rules.CurrentValue.PiggybackWeightMultiplier);
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
		var (host, _, _) = HostWithRuleEntries(refusal: "the configuration could not be saved");
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/gamerule pvp_enabled true"));

		Assert.Contains(console.Lines, line => line.Text.Contains("Game rule pvp_enabled was not changed: the configuration could not be saved"));
	}

	/// <summary>
	/// A host whose rule entries are faked at the seam the test project can reach: the editor writes the
	/// options monitor the rule surface is composed from, which is the edge a production <c>ConfigEntry</c>
	/// write fires. That is what makes the answer's read-back and the announcement edge real here.
	/// </summary>
	private static (TestNode Host, StubHostRulesEditor Editor, MutableOptionsMonitor<HostRulesOptions> Rules) HostWithRuleEntries(
		string? inForceAfterWrite = null,
		string? refusal = null)
	{
		var rules = new MutableOptionsMonitor<HostRulesOptions>(new HostRulesOptions());
		var respawn = new MutableOptionsMonitor<RespawnOptions>(new RespawnOptions());
		var editor = new StubHostRulesEditor(rules, respawn)
		{
			InForceAfterWrite = inForceAfterWrite,
			Refusal = refusal,
		};
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: services =>
			{
				services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(rules));
				services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<RespawnOptions>>(respawn));
				services.Replace(ServiceDescriptor.Singleton<IHostRulesEditor>(editor));
			});
		return (host, editor, rules);
	}
}
