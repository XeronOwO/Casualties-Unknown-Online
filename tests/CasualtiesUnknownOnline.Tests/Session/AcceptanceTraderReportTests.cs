using System;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The acceptance-only <c>/traderreport</c> lever: the give bound is judged on the host, and proving the
/// refusal and the clamp needs a claim the game's own gesture cannot produce. The verb is a MEMBER's report
/// and it goes through the cheat family's gate — a lever that can move a trader's credit is not a player's
/// shortcut — so the cases here pin its three postures (open, closed switch, host role) and the one wire
/// fact: what it sends is exactly the give report the bound reads.
/// </summary>
[Trait("Category", "Integration")]
public class AcceptanceTraderReportTests
{
	private const ulong TraderItem = 7001;

	[Fact]
	public void OnAMember_WithTheCheatRulingsOpen_SendsOneGiveReport()
	{
		var (host, guest) = CreateLeverSession(switchOpen: true, grant: true);
		TraderActionMsg? sent = null;
		host.Services.GetRequiredService<IWorldControl>().TraderActionReceived += (_, msg) => sent = msg;

		var answer = Run(guest, $"/traderreport 12.5 45 {TraderItem} 60");
		Pump(host, guest);

		Assert.Contains($"Reported give of item #{TraderItem} worth 60", answer, StringComparison.Ordinal);
		Assert.True(sent is not null, "the report must reach the host");
		Assert.Equal(TraderActionKind.GiveItem, sent!.Action);
		Assert.Equal(TraderItem, sent.ItemInstanceId);
		Assert.Equal(60, sent.ItemValue);
		Assert.Equal(12.5f, sent.Position.X);
	}

	[Fact]
	public void OnTheHost_IsRefusedByRole()
	{
		var (host, _) = CreateLeverSession(switchOpen: true, grant: true);

		var answer = Run(host, $"/traderreport 0 0 {TraderItem} 60");

		Assert.Contains("MEMBER's report", answer, StringComparison.Ordinal);
	}

	[Fact]
	public void WithTheCheatSwitchClosed_IsRefusedLocally()
	{
		var (host, guest) = CreateLeverSession(switchOpen: false, grant: true);
		TraderActionMsg? sent = null;
		host.Services.GetRequiredService<IWorldControl>().TraderActionReceived += (_, msg) => sent = msg;

		var answer = Run(guest, $"/traderreport 0 0 {TraderItem} 60");
		Pump(host, guest);

		Assert.Contains("AllowMemberCheatVerbs", answer, StringComparison.Ordinal);
		Assert.True(sent is null, "a refused lever sends nothing");
	}

	[Fact]
	public void WithANonFinitePosition_IsRefused()
	{
		var (host, guest) = CreateLeverSession(switchOpen: true, grant: true);
		TraderActionMsg? sent = null;
		host.Services.GetRequiredService<IWorldControl>().TraderActionReceived += (_, msg) => sent = msg;

		var answer = Run(guest, $"/traderreport NaN 0 {TraderItem} 60");
		Pump(host, guest);

		Assert.Contains("finite", answer, StringComparison.Ordinal);
		Assert.True(sent is null, "a non-finite position is not sent");
	}

	private static (TestNode Host, TestNode Guest) CreateLeverSession(bool switchOpen, bool grant)
	{
		var rules = new MutableOptionsMonitor<HostRulesOptions>(
			new HostRulesOptions { AllowMemberCheatVerbs = switchOpen });
		var (host, guest) = CreateSession(services =>
			services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(rules)));
		if (grant)
		{
			GrantAndAnnounceCheatVerbs(host, guest);
		}

		return (host, guest);
	}

	/// <summary>The production frame pump: each node drains what arrived for it, in the order the plugin's own update loop runs.</summary>
	private static void Pump(TestNode host, TestNode guest)
	{
		for (var round = 0; round < 2; round++)
		{
			host.Update();
			guest.Update();
		}
	}

	private static string Run(TestNode node, string line)
	{
		var console = node.Services.GetRequiredService<ICommandControl>();
		Assert.True(console.TryExecute(line), $"'{line}' was not accepted by the console");
		return console.Lines[console.Lines.Count - 1].Text;
	}
}
