using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The <c>/heal</c> cheat verb's HOST half through the real console, the real composition root and
/// the real wire handlers: the selector verdict, the body reset on the client that owns the body,
/// the answer that names what happened, and the refusals. The member half (the two-sided cheat
/// check and the request/answer path) is <see cref="CommandConsoleMemberHealResetTests"/>'s.
///
/// <para>
/// The body seam is the suite's own fixture (<see cref="FakeLocalBodyReset"/>): a test host has no
/// game scene, and the game's live <c>Body</c> — the fields, the limb GameObjects, the components —
/// is exactly what the adapter's implementation proves at acceptance. What these cases pin is the
/// control plane: whose body each target's reset runs on, the sentence the operator reads, and that
/// a refusal is answered instead of a "Sent" that hides a drop.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class CommandConsoleHealResetTests
{
	[Fact]
	public void Host_HealsItself_ResetsItsOwnBodyAndNamesTheRevive()
	{
		var (host, _, reset) = CreateHealSession();
		reset.Outcome = LocalBodyResetOutcome.AppliedNow(wasDead: true, restoredLimbs: 2, hollowCleared: true);

		var answer = Run(host, "/heal @s");

		Assert.Equal("Reset 1001: full heal applied (revived from death; 2 severed limb(s) restored; hollow cleared).", answer);
		Assert.Equal(1, reset.Resets);
	}

	[Fact]
	public void Host_HealsAMember_InstructsThatClientsOwnBody()
	{
		var (host, guest, reset) = CreateHealSession();
		SeedMember(host, conscious: true);

		var answer = Run(host, "/heal @p");

		Assert.Equal("Reset 2001: sent to that player's own client, which applies the full reset and re-reports the state.", answer);
		Assert.Equal(1, reset.Resets);
		Assert.Contains(guest.Services.GetRequiredService<ICommandControl>().Lines,
			line => line.Text.Contains("1001 fully reset your state."));
	}

	[Fact]
	public void Host_HealsADeadMember_NamesTheReviveInTheAnswer()
	{
		var (host, _, _) = CreateHealSession();
		var dead = Snapshot(GuestId, conscious: false);
		dead.Health!.Alive = false;
		Characters(host).SaveCharacterData(GuestId, dead);

		var answer = Run(host, "/heal @p");

		Assert.Contains("it was dead, so it revives", answer);
	}

	[Fact]
	public void Host_HealsWhileTheMemberSwitchIsClosed_StillWorks()
	{
		var (host, _, reset) = CreateHealSession(memberCheatSwitch: false);
		SeedMember(host, conscious: true);

		var answer = Run(host, "/heal @p");

		Assert.StartsWith("Reset 2001: sent", answer);
		Assert.Equal(1, reset.Resets);
	}

	/// <summary>
	/// The target list the console's selector expands is never empty here; the direct surface is
	/// what pins the guard a hand-built request also reaches, and the sentence names the target.
	/// </summary>
	[Fact]
	public void Host_HealsATargetOutsideTheWorld_IsRefusedByName()
	{
		var (host, _, reset) = CreateHealSession();

		var answer = host.Services.GetRequiredService<IHealResetControl>().Reset([9999]);

		Assert.Equal("Refused for 9999: not in this world (left, or no snapshot yet).", answer);
		Assert.Equal(0, reset.Resets);
	}

	[Fact]
	public void Host_HealsNoTarget_IsRefusedByName()
	{
		var (host, _, reset) = CreateHealSession();

		var answer = host.Services.GetRequiredService<IHealResetControl>().Reset([]);

		Assert.Equal("Refused: the request names no player.", answer);
		Assert.Equal(0, reset.Resets);
	}

	/// <summary>
	/// The dedupe and the aggregate sentence: a request that names the host and a member resets the
	/// host locally and instructs the member, and each target keeps its own clause. A repeated id is
	/// one reset, not two.
	/// </summary>
	[Fact]
	public void Host_HealsItsOwnBodyAndAMember_AnswersEachTargetOnce()
	{
		var (host, _, reset) = CreateHealSession();
		SeedMember(host, conscious: true);

		var answer = host.Services.GetRequiredService<IHealResetControl>().Reset([HostId, GuestId, GuestId]);

		Assert.Contains("Reset 1001: full heal applied.", answer);
		Assert.Contains("Reset 2001: sent to that player's own client", answer);
		Assert.Equal(2, reset.Resets);
	}

	[Fact]
	public void Host_Heals_WhenTheTargetsOwnClientCannotApply_StillSendsAndTheTargetReadsTheRefusal()
	{
		var (host, guest, reset) = CreateHealSession();
		SeedMember(host, conscious: true);
		reset.Outcome = LocalBodyResetOutcome.Refused("this client has no local body yet.");

		var answer = Run(host, "/heal @p");

		Assert.StartsWith("Reset 2001: sent", answer);
		Assert.Contains(guest.Services.GetRequiredService<ICommandControl>().Lines,
			line => line.Text.Contains("could not be applied here: this client has no local body yet."));
	}

	/// <summary>The old wiring's silence is the regression this case guards: a self target is reset, never a no-op behind a "Sent" line.</summary>
	[Fact]
	public void Host_HealSelf_IsNotTheOldItemRequestWire()
	{
		var (host, guest, reset) = CreateHealSession();
		SeedMember(host, conscious: true);
		var requests = new List<NetMsg>();
		host.Transport.MessageReceived += (_, frame) => requests.Add((NetMsg)frame[0]);

		var answer = Run(host, "/heal @s");

		Assert.DoesNotContain("Sent heal request", answer);
		Assert.DoesNotContain(NetMsg.PlayerHealRequest, requests);
		Assert.Equal(1, reset.Resets);
		Assert.DoesNotContain(guest.Services.GetRequiredService<ICommandControl>().Lines,
			line => line.Text.Contains("reset"));
	}

	[Fact]
	public void Host_HealsAnUnknownSelector_AddsNoMatchLine()
	{
		var (host, _, _) = CreateHealSession();

		var answer = Run(host, "/heal @z");

		Assert.Equal("No players match selector '@z'.", answer);
	}

	[Fact]
	public void Host_HealsWithoutASelector_ShowsUsage()
	{
		var (host, _, _) = CreateHealSession();

		var answer = Run(host, "/heal");

		Assert.Equal("Usage: /heal <selector>", answer);
	}

	/// <summary>Every host case runs over the production root, the shared body fixture and the host-rules monitor the switch case varies.</summary>
	private static (TestNode Host, TestNode Guest, FakeLocalBodyReset Reset) CreateHealSession(
		bool memberCheatSwitch = false, bool announceRoster = true)
	{
		var reset = new FakeLocalBodyReset();
		var rules = new MutableOptionsMonitor<HostRulesOptions>(
			new HostRulesOptions { AllowMemberCheatVerbs = memberCheatSwitch });
		var (host, guest) = CreateSession(
			services =>
			{
				services.Replace(ServiceDescriptor.Singleton<ILocalBodyReset>(reset));
				services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(rules));
			},
			swallowHostToGuest: announceRoster ? null : NetMsg.CheatPermissions);
		// Production builds the console at startup; a test must too, before a wire instruction can
		// arrive, or the target's own notice has no subscriber to print it.
		_ = guest.Services.GetRequiredService<ICommandControl>();
		SeedHostEntities(host, GuestId, 5f);
		return (host, guest, reset);
	}

	private static void SeedMember(TestNode host, bool conscious)
	{
		Characters(host).SaveHostCharacterData(Snapshot(HostId, conscious: true));
		Characters(host).SaveCharacterData(GuestId, Snapshot(GuestId, conscious));
	}

	private static ICharacterDataControl Characters(TestNode node) =>
		node.Services.GetRequiredService<ICharacterDataControl>();

	/// <summary>Runs one console line and answers with the line it printed.</summary>
	private static string Run(TestNode node, string line)
	{
		var console = node.Services.GetRequiredService<ICommandControl>();
		Assert.True(console.TryExecute(line), $"'{line}' was not accepted by the console");
		return console.Lines[console.Lines.Count - 1].Text;
	}
}
