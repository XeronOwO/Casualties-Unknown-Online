using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Permissions;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The console completes only what the caller may run: the command word is filtered by the same authority
/// the handler enforces (the host-only declaration, or the cheat family's switch + roster), <c>/help</c>
/// uses the same filter and answers a hidden command with the refusal it would meet, and the two commands
/// with their own vocabulary complete it — <c>/gamerule</c>'s rule names then that rule's values, and
/// <c>/data</c>'s verbs with the write half gated.
///
/// <para>
/// The rows are judged from the deployed client's own suggestion source, so the tests read
/// <see cref="ICommandCompletionSource.Suggest"/> and the console's own output, never a re-implementation.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class CommandConsolePermissionCompletionTests
{
	[Fact]
	public void PlainMember_CommandMetadata_DoesNotNameHiddenCommands()
	{
		var (host, guest) = CreateSession(switchOpen: false, grant: false);

		var guestSpecs = guest.Services.GetRequiredService<ICommandCompletionSource>().Commands;
		var hostSpecs = host.Services.GetRequiredService<ICommandCompletionSource>().Commands;

		Assert.DoesNotContain(guestSpecs, spec => spec.Name is "give" or "gamerule" or "kick" or "op" or "traderreport");
		Assert.Contains(guestSpecs, spec => spec.Name == "data");
		Assert.Contains(hostSpecs, spec => spec.Name == "gamerule");
		Assert.Contains(hostSpecs, spec => spec.Name == "kick");
	}

	[Fact]
	public void PlainMember_IsOfferedNoAuthorityCommand()
	{
		var (_, guest) = CreateSession(switchOpen: false, grant: false);
		var completion = guest.Services.GetRequiredService<ICommandCompletionSource>();

		Assert.Empty(completion.Suggest("/ga"));
		Assert.Empty(completion.Suggest("/k"));
		Assert.Empty(completion.Suggest("/gi"));
		Assert.Empty(completion.Suggest("/op"));
		Assert.Empty(completion.Suggest("/trader"));

		// ...and the ordinary commands are still offered, so the filter is not hiding everything.
		Assert.Contains(completion.Suggest("/pl"), s => s.Text == "players");
		Assert.Contains(completion.Suggest("/da"), s => s.Text == "data");
	}

	[Fact]
	public void Host_IsOfferedTheAuthorityCommands()
	{
		var (host, _) = CreateSession(switchOpen: false, grant: false);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		Assert.Contains(completion.Suggest("/ga"), s => s.Text == "gamerule");
		Assert.Contains(completion.Suggest("/k"), s => s.Text == "kick");
		Assert.Contains(completion.Suggest("/gi"), s => s.Text == "give");
	}

	[Fact]
	public void GrantedMember_IsOfferedTheCheatVerbsButNotTheHostOnlyOnes()
	{
		var (_, guest) = CreateSession(switchOpen: true, grant: true);
		var completion = guest.Services.GetRequiredService<ICommandCompletionSource>();

		Assert.Contains(completion.Suggest("/gi"), s => s.Text == "give");
		Assert.Contains(completion.Suggest("/ga"), s => s.Text == "gamerule");
		Assert.Empty(completion.Suggest("/k"));
		Assert.Empty(completion.Suggest("/op"));
	}

	[Fact]
	public void GrantedMember_WhoseSwitchCloses_LosesTheCheatCompletion()
	{
		var (host, guest, rules) = CreateSessionWithRules(switchOpen: true, grant: true);
		Assert.Contains(guest.Services.GetRequiredService<ICommandCompletionSource>().Suggest("/gi"), s => s.Text == "give");

		rules.Set(new HostRulesOptions { AllowMemberCheatVerbs = false });
		host.Update();

		Assert.Empty(guest.Services.GetRequiredService<ICommandCompletionSource>().Suggest("/gi"));
	}

	[Fact]
	public void GrantedMember_OffTheAnnouncedRoster_LosesTheCheatCompletion()
	{
		// The roster ground, not the switch: the switch is open, the announced list simply does not name it.
		var (_, guest) = CreateSession(switchOpen: true, grant: false);

		Assert.Empty(guest.Services.GetRequiredService<ICommandCompletionSource>().Suggest("/gi"));
	}

	[Fact]
	public void PlainMember_HelpOmitsTheAuthorityCommands()
	{
		var (_, guest) = CreateSession(switchOpen: false, grant: false);

		var text = Run(guest, "/help");

		Assert.DoesNotContain("/give —", text);
		Assert.DoesNotContain("/gamerule —", text);
		Assert.DoesNotContain("/kick —", text);
		Assert.DoesNotContain("/op —", text);
		Assert.DoesNotContain("/traderreport —", text);
		Assert.Contains("/data —", text);
		Assert.Contains("/players —", text);
	}

	[Fact]
	public void PlainMember_HelpCompletion_DoesNotOfferHiddenCommandNames()
	{
		var (_, guest) = CreateSession(switchOpen: false, grant: false);
		var suggestions = guest.Services.GetRequiredService<ICommandCompletionSource>().Suggest("/help ");

		Assert.DoesNotContain(suggestions, s => s.Text is "give" or "gamerule" or "kick" or "op" or "traderreport");
		Assert.Contains(suggestions, s => s.Text == "players");
		Assert.Contains(suggestions, s => s.Text == "data");
	}

	[Fact]
	public void PlainMember_HelpForAHiddenCheatVerb_AnswersTheGateRefusal()
	{
		var (_, guest) = CreateSession(switchOpen: false, grant: false);

		var answer = Run(guest, "/help gamerule");

		Assert.DoesNotContain("Unknown command", answer);
		Assert.Contains("cheat switch", answer);
	}

	[Fact]
	public void PlainMember_HelpForAHostOnlyVerb_AnswersTheHostOnlyRefusal()
	{
		var (host, guest) = CreateSession(switchOpen: false, grant: false);
		Assert.True(host.Session.Role == SessionRole.Host);

		var answer = Run(guest, "/help kick");

		Assert.DoesNotContain("Unknown command", answer);
		Assert.Contains("host-only", answer);
	}

	[Fact]
	public void Host_GameRuleCompletion_OffersTheRuleNamesWithTheirValues()
	{
		var (host, _) = CreateSession(switchOpen: false, grant: false);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var all = completion.Suggest("/gamerule ");
		var narrowed = completion.Suggest("/gamerule remote");

		Assert.Equal([.. HostRuleCensus.Rows.Select(row => row.Name)], [.. all.Select(s => s.Text)]);
		Assert.All(HostRuleCensus.Rows, row => Assert.Contains(all, s => s.Text == row.Name));
		var remote = Assert.Single(narrowed);
		Assert.Equal("remote_backpack_access", remote.Text);
		Assert.Equal(CommandSuggestionHint.HostRuleCurrent, remote.Hint);
	}

	[Fact]
	public void Host_GameRuleCompletion_OffersExactlyThatRulesAcceptedValues()
	{
		var (host, _) = CreateSession(switchOpen: false, grant: false);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var access = completion.Suggest("/gamerule remote_backpack_access ");
		var parity = completion.Suggest("/gamerule native_binding_parity ");
		var weight = completion.Suggest("/gamerule piggyback_weight_multiplier ");

		Assert.Equal(["allow", "unconscious_or_dead", "deny"], [.. access.Select(s => s.Text)]);
		Assert.Equal(["allow", "warn", "require"], [.. parity.Select(s => s.Text)]);
		Assert.Empty(weight);

		// Every rule's own list, not only the two three-way ones: a boolean rule offers true/false and a
		// numeric rule offers nothing (its refusal names the range).
		foreach (var row in HostRuleCensus.Rows)
		{
			var offered = completion.Suggest($"/gamerule {row.Name} ");
			Assert.Equal([.. HostRuleValueText.Choices(row)], [.. offered.Select(s => s.Text)]);
		}
	}

	[Fact]
	public void EveryOfferedGameRuleValue_IsAcceptedByTheSameParserTheHandlerRuns()
	{
		var (host, _) = CreateSession(switchOpen: false, grant: false);
		var completion = host.Services.GetRequiredService<ICommandCompletionSource>();
		var checkedValues = 0;

		foreach (var row in HostRuleCensus.Rows)
		{
			foreach (var suggestion in completion.Suggest($"/gamerule {row.Name} "))
			{
				checkedValues++;
				Assert.True(
					HostRuleValueText.TryParse(row, suggestion.Text, out _, out var error),
					$"/gamerule {row.Name} {suggestion.Text} was offered but refused: {error}");
			}
		}

		// The two three-way sets are the candidate lists this row is about; a vacuous pass would prove nothing.
		Assert.True(checkedValues >= 6, $"only {checkedValues} value suggestions were checked");
	}

	[Fact]
	public void DataCompletion_OffersTheReadVerbToEveryone_AndTheWriteVerbsOnlyToTheAuthorised()
	{
		var (host, guest) = CreateSession(switchOpen: true, grant: false);
		var guestCompletion = guest.Services.GetRequiredService<ICommandCompletionSource>();
		var hostCompletion = host.Services.GetRequiredService<ICommandCompletionSource>();

		var plain = guestCompletion.Suggest("/data ");
		Assert.Equal(["get"], [.. plain.Select(s => s.Text)]);

		Grant(host, guest);
		var granted = guestCompletion.Suggest("/data ");
		Assert.Contains(granted, s => s.Text == "get");
		Assert.Contains(granted, s => s.Text == "merge");
		Assert.Contains(granted, s => s.Text == "modify");
		Assert.Contains(granted, s => s.Text == "remove");

		var hostWords = hostCompletion.Suggest("/data ");
		Assert.Contains(hostWords, s => s.Text == "merge");
	}

	[Fact]
	public void DataStateCompletion_IsWithheldFromACallerTheGateRefuses()
	{
		// The write verbs' SNBT vocabulary is the same gate: a caller the roster refuses gets no state
		// suggestions, while the host (and a granted member) does — the schema is the same either way, so an
		// empty answer can only come from the lock, not from a missing item.
		var (host, guest) = CreateSession(switchOpen: true, grant: false);
		var battery = new CharacterItemMsg
		{
			InstanceId = 4101,
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
		// The completion asks THIS client's item record, so the member's own kernel holds the same row: with
		// the schema present either way, an empty answer can only be the gate's lock.
		var guestKernel = guest.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.True(guestKernel.TrySpawnCarried(GuestId, battery.InstanceId, battery.ItemId, battery, out _, out _));
		const string Line = "/data merge id 4101 {Components:{BatteryItem:{cha";

		Assert.Empty(guest.Services.GetRequiredService<ICommandCompletionSource>().Suggest(Line));
		Assert.NotEmpty(host.Services.GetRequiredService<ICommandCompletionSource>().Suggest(Line));

		Grant(host, guest);
		Assert.NotEmpty(guest.Services.GetRequiredService<ICommandCompletionSource>().Suggest(Line));
	}

	[Fact]
	public void Suggestions_WriteNoLog()
	{
		var log = new RecordingLogger<CommandConsoleService>();
		var (host, guest) = CreateSession(
			switchOpen: false,
			grant: false,
			extraRegistrations: services => services.Replace(ServiceDescriptor.Singleton<ILogger<CommandConsoleService>>(log)));
		var completion = guest.Services.GetRequiredService<ICommandCompletionSource>();

		completion.Suggest("/g");
		completion.Suggest("/gamerule ");
		completion.Suggest("/help");
		completion.Suggest("/data ");

		Assert.DoesNotContain(log.Entries, entry => entry.Message.Contains("[Cheat]", StringComparison.Ordinal));
		Assert.DoesNotContain(log.Entries, entry => entry.Message.Contains("[Command]", StringComparison.Ordinal));
		_ = host;
	}

	[Fact]
	public void HiddenCommand_TypedDirectly_IsStillRefusedByName()
	{
		var (_, guest) = CreateSession(switchOpen: false, grant: false);

		var answer = Run(guest, "/gamerule remote_backpack_access deny");
		var kick = guest.Services.GetRequiredService<ICommandControl>().TryExecute("/kick 1");

		Assert.Contains("cheat switch", answer);
		Assert.False(kick);
	}

	private static (TestNode Host, TestNode Guest) CreateSession(
		bool switchOpen,
		bool grant,
		Action<IServiceCollection>? extraRegistrations = null)
	{
		var (host, guest, _) = CreateSessionWithRules(switchOpen, grant, extraRegistrations);
		return (host, guest);
	}

	private static (TestNode Host, TestNode Guest, MutableOptionsMonitor<HostRulesOptions> Rules) CreateSessionWithRules(
		bool switchOpen,
		bool grant,
		Action<IServiceCollection>? extraRegistrations = null)
	{
		var rules = new MutableOptionsMonitor<HostRulesOptions>(new HostRulesOptions { AllowMemberCheatVerbs = switchOpen });
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: services =>
			{
				services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(rules));
				extraRegistrations?.Invoke(services);
			});
		MarkInWorld(host);
		MarkInWorld(guest);
		if (grant)
		{
			Grant(host, guest);
		}

		return (host, guest, rules);
	}

	private static void Grant(TestNode host, TestNode guest)
	{
		var permissions = host.Services.GetRequiredService<CheatPermissions>();
		Assert.True(permissions.Grant(guest.SteamId, out var refusal), refusal);
		host.Services.GetRequiredService<ICheatPermissionAnnouncement>().AnnounceAll();
	}

	private static string Run(TestNode node, string line)
	{
		var console = node.Services.GetRequiredService<ICommandControl>();
		Assert.True(console.TryExecute(line), $"'{line}' was not accepted by the console");
		return console.Lines[console.Lines.Count - 1].Text;
	}
}
