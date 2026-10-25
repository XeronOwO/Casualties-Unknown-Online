using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
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
/// game scene, and the game's live <c>Body</c> —the fields, the limb GameObjects, the components —
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

		var answer = Run(host, "/heal @p[distance=1..]");

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

		var answer = Run(host, "/heal @p[distance=1..]");

		Assert.Contains("it was dead, so it revives", answer);
	}

	[Fact]
	public void Host_HealsWhileTheMemberSwitchIsClosed_StillWorks()
	{
		var (host, _, reset) = CreateHealSession(memberCheatSwitch: false);
		SeedMember(host, conscious: true);

		var answer = Run(host, "/heal @p[distance=1..]");

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

		var answer = Run(host, "/heal @p[distance=1..]");

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

	/// <summary>The host's own reset needs no authority event: its own snapshot is written authoritatively already.</summary>
	[Fact]
	public void Host_SelfReset_AsksTheBodyWithNoInstructionId()
	{
		var (host, _, reset) = CreateHealSession();

		Run(host, "/heal @s");

		Assert.Null(reset.LastInstructionId);
	}

	/// <summary>
	/// The bug of batch <c>20261018-x</c>: the reset's blood volume/bleeds cannot ride the ordinary
	/// character report (the store preserves the host's three continuous values), so the target's
	/// client sends them as the applied event and the host stores them through the authority path.
	/// The member's host-model blood starts negative exactly as the batch found it.
	/// </summary>
	[Fact]
	public void Host_HealsAMember_StoresTheAppliedResultThroughTheAuthorityPath()
	{
		var (host, guest, reset) = CreateHealSession();
		SeedMember(host, conscious: true);
		var before = Characters(host).GetSavedCharacter(GuestId)!;
		before.Health!.BloodVolume = -99.7f;
		before.Health.TotalBleedSpeed = 3.2f;
		before.Limbs.Add(new CharacterLimbMsg { Index = 0, BleedAmount = 0.7f });
		var revisionBefore = before.LocalRevision;

		Assert.StartsWith("Reset 2001: sent", Run(host, "/heal @p[distance=1..]"));
		Assert.NotNull(reset.LastInstructionId);
		SendApplied(guest, host, reset.LastInstructionId!.Value);

		var saved = Characters(host).GetSavedCharacter(GuestId)!;
		Assert.Equal(100f, saved.Health!.BloodVolume);
		Assert.Equal(0f, saved.Health.TotalBleedSpeed);
		Assert.All(saved.Limbs, limb => Assert.Equal(0f, limb.BleedAmount));
		Assert.True(saved.LocalRevision > revisionBefore, "the event's in-place merge must bump the clone's revision");
	}

	/// <summary>
	/// The plain report that follows a reset is unchanged: it still may not state the three owned
	/// values, so a report built before the reset (or one hostile to it) cannot lower the healed
	/// snapshot the event just wrote.
	/// </summary>
	[Fact]
	public void AnAppliedReset_IsNotRevertedByThePlainReportThatFollows()
	{
		var (host, guest, reset) = CreateHealSession();
		SeedMember(host, conscious: true);
		Characters(host).GetSavedCharacter(GuestId)!.Health!.BloodVolume = -99.7f;

		Run(host, "/heal @p[distance=1..]");
		SendApplied(guest, host, reset.LastInstructionId!.Value);
		SendPlainReport(guest, host, bloodVolume: -99.7f);

		Assert.Equal(100f, Characters(host).GetSavedCharacter(GuestId)!.Health!.BloodVolume);
	}

	[Fact]
	public void A_NonFiniteAppliedResult_IsRefusedWholeByNameAndTheSnapshotStays()
	{
		var storeLog = new RecordingLogger<CharacterDataStore>();
		var (host, guest, reset) = CreateHealSession(storeLog: storeLog);
		SeedMember(host, conscious: true);
		Characters(host).GetSavedCharacter(GuestId)!.Health!.BloodVolume = 42f;
		Run(host, "/heal @p[distance=1..]");

		var state = HealState();
		state.Health!.BloodVolume = float.NaN;
		SendApplied(guest, host, reset.LastInstructionId!.Value, state);

		Assert.Equal(42f, Characters(host).GetSavedCharacter(GuestId)!.Health!.BloodVolume);
		Assert.True(storeLog.HasError("health.BloodVolume"), "the refusal names the field");
		Assert.True(storeLog.HasError("previous snapshot stays"), "the refusal says the previous snapshot stands");
	}

	/// <summary>
	/// The authority anchor: a result writes only while the host has an instruction outstanding for
	/// that member. Without this, any member could heal itself on demand without the cheat switch or
	/// roster ever seeing it.
	/// </summary>
	[Fact]
	public void AnAppliedResultWithNoOutstandingInstruction_WritesNothing()
	{
		var serviceLog = new RecordingLogger<PlayerHealResetService>();
		var (host, guest, _) = CreateHealSession(serviceLog: serviceLog);
		SeedMember(host, conscious: true);
		Characters(host).GetSavedCharacter(GuestId)!.Health!.BloodVolume = 42f;

		SendApplied(guest, host, instructionId: 7);

		Assert.Equal(42f, Characters(host).GetSavedCharacter(GuestId)!.Health!.BloodVolume);
		Assert.Contains(serviceLog.Entries, entry => entry.Message.Contains("no outstanding instruction", StringComparison.Ordinal));
	}

	[Fact]
	public void AnAppliedResultNamingAnotherOwner_IsDropped()
	{
		var serviceLog = new RecordingLogger<PlayerHealResetService>();
		var (host, guest, reset) = CreateHealSession(serviceLog: serviceLog);
		SeedMember(host, conscious: true);
		Characters(host).GetSavedCharacter(GuestId)!.Health!.BloodVolume = 42f;
		Run(host, "/heal @p[distance=1..]");

		SendApplied(guest, host, reset.LastInstructionId!.Value, owner: HostId);

		Assert.Equal(42f, Characters(host).GetSavedCharacter(GuestId)!.Health!.BloodVolume);
		Assert.Contains(serviceLog.Entries, entry => entry.Message.Contains("claiming owner", StringComparison.Ordinal));
	}

	/// <summary>A result answering an older instruction (two heals landed before it arrived) is not the outstanding one and writes nothing.</summary>
	[Fact]
	public void AnAppliedResultForASupersededInstruction_WritesNothing()
	{
		var serviceLog = new RecordingLogger<PlayerHealResetService>();
		var (host, guest, reset) = CreateHealSession(serviceLog: serviceLog);
		SeedMember(host, conscious: true);
		Characters(host).GetSavedCharacter(GuestId)!.Health!.BloodVolume = 42f;

		Run(host, "/heal @p[distance=1..]");
		var first = reset.LastInstructionId!.Value;
		Run(host, "/heal @p[distance=1..]");
		var second = reset.LastInstructionId!.Value;
		Assert.NotEqual(first, second);

		SendApplied(guest, host, first);
		Assert.Equal(42f, Characters(host).GetSavedCharacter(GuestId)!.Health!.BloodVolume);

		SendApplied(guest, host, second);
		Assert.Equal(100f, Characters(host).GetSavedCharacter(GuestId)!.Health!.BloodVolume);
	}

	[Fact]
	public void AnAppliedResultPastTheInstructionLivenessBound_IsRefused()
	{
		var (host, guest, reset) = CreateHealSession();
		SeedMember(host, conscious: true);
		Characters(host).GetSavedCharacter(GuestId)!.Health!.BloodVolume = 42f;
		Run(host, "/heal @p[distance=1..]");

		host.Clock.Advance(60_000);
		SendApplied(guest, host, reset.LastInstructionId!.Value);

		Assert.Equal(42f, Characters(host).GetSavedCharacter(GuestId)!.Health!.BloodVolume);
	}

	/// <summary>
	/// The three values the host owns are bounded by the reset TABLE the host itself ordered, not by
	/// the member's word: a result claiming any other blood volume, total bleeding speed or limb bleed
	/// is refused whole by name. A refused result does not resolve the instruction (the target's next
	/// retry could still carry a usable one), so the healthy result is accepted afterwards.
	/// </summary>
	[Fact]
	public void AnAppliedResultClaimingValuesTheResetTableDoesNotWrite_IsRefusedWhole()
	{
		var storeLog = new RecordingLogger<CharacterDataStore>();
		var (host, guest, reset) = CreateHealSession(storeLog: storeLog);
		SeedMember(host, conscious: true);
		Characters(host).GetSavedCharacter(GuestId)!.Health!.BloodVolume = 42f;
		Run(host, "/heal @p[distance=1..]");
		var instructionId = reset.LastInstructionId!.Value;

		var tooMuchBlood = HealState();
		tooMuchBlood.Health!.BloodVolume = 200f;
		SendApplied(guest, host, instructionId, tooMuchBlood);
		Assert.Equal(42f, Characters(host).GetSavedCharacter(GuestId)!.Health!.BloodVolume);

		var bleeding = HealState();
		bleeding.Health!.TotalBleedSpeed = 2f;
		SendApplied(guest, host, instructionId, bleeding);
		Assert.Equal(42f, Characters(host).GetSavedCharacter(GuestId)!.Health!.BloodVolume);

		var limbBleed = HealState();
		limbBleed.Limbs[0].BleedAmount = 0.5f;
		SendApplied(guest, host, instructionId, limbBleed);
		Assert.Equal(42f, Characters(host).GetSavedCharacter(GuestId)!.Health!.BloodVolume);

		SendApplied(guest, host, instructionId);
		Assert.Equal(100f, Characters(host).GetSavedCharacter(GuestId)!.Health!.BloodVolume);
		Assert.True(storeLog.HasError("health.BloodVolume"), "the blood mismatch is named");
		Assert.True(storeLog.HasError("health.TotalBleedSpeed"), "the total-bleed mismatch is named");
		Assert.True(storeLog.HasError("limb 0.BleedAmount"), "the limb-bleed mismatch is named");
	}

	/// <summary>
	/// The host answers for a locally suppressed instruction instead of claiming it was sent, and
	/// records no outstanding instruction — so a later result cannot be accepted against a send that
	/// never happened.
	/// </summary>
	[Fact]
	public void AnInstructionTheTransportRefuses_IsAnsweredAndLeavesNoPendingReset()
	{
		var serviceLog = new RecordingLogger<PlayerHealResetService>();
		var (host, guest, reset) = CreateHealSession(serviceLog: serviceLog);
		SeedMember(host, conscious: true);
		Characters(host).GetSavedCharacter(GuestId)!.Health!.BloodVolume = 42f;
		host.Transport.Network.SetFaults(HostId, GuestId, new LinkFaults { Down = true });

		var answer = Run(host, "/heal @p[distance=1..]");

		host.Transport.Network.ClearFaults(HostId, GuestId);
		Assert.Contains("transport did not accept the reset instruction", answer);
		Assert.Equal(0, reset.Resets);
		Assert.Equal(0, host.Services.GetRequiredService<PlayerHealResetService>().PendingInstructionCount);
		SendApplied(guest, host, instructionId: 1);
		Assert.Equal(42f, Characters(host).GetSavedCharacter(GuestId)!.Health!.BloodVolume);
		Assert.Contains(serviceLog.Entries, entry => entry.Message.Contains("no outstanding instruction", StringComparison.Ordinal));
	}

	/// <summary>Every host case runs over the production root, the shared body fixture and the host-rules monitor the switch case varies.</summary>
	private static (TestNode Host, TestNode Guest, FakeLocalBodyReset Reset) CreateHealSession(
		bool memberCheatSwitch = false, bool announceRoster = true,
		RecordingLogger<CharacterDataStore>? storeLog = null,
		RecordingLogger<PlayerHealResetService>? serviceLog = null)
	{
		var reset = new FakeLocalBodyReset();
		var rules = new MutableOptionsMonitor<HostRulesOptions>(
			new HostRulesOptions { AllowMemberCheatVerbs = memberCheatSwitch });
		var (host, guest) = CreateSession(
			services =>
			{
				services.Replace(ServiceDescriptor.Singleton<ILocalBodyReset>(reset));
				services.Replace(ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(rules));
				if (storeLog is not null)
				{
					services.Replace(ServiceDescriptor.Singleton<ILogger<CharacterDataStore>>(storeLog));
				}

				if (serviceLog is not null)
				{
					services.Replace(ServiceDescriptor.Singleton<ILogger<PlayerHealResetService>>(serviceLog));
				}
			},
			swallowHostToGuest: announceRoster ? null : NetMsg.CheatPermissions);
		// Production builds the console at startup; a test must too, before a wire instruction can
		// arrive, or the target's own notice has no subscriber to print it.
		_ = guest.Services.GetRequiredService<ICommandControl>();
		SeedHostEntities(host, GuestId, 5f);
		return (host, guest, reset);
	}

	/// <summary>The applied event the target's adapter sends after a succeeded version of the reset: full healthy body/limb state, stamped by its owner.</summary>
	private static HealResetAppliedMsg HealState() => new()
	{
		Health = new CharacterHealthMsg
		{
			Alive = true,
			Conscious = true,
			BrainHealth = 100f,
			BloodVolume = 100f,
			TotalBleedSpeed = 0f,
			Consciousness = 100f,
		},
		Limbs =
		[
			new CharacterLimbMsg { Index = 0, SkinHealth = 100f, MuscleHealth = 100f },
			new CharacterLimbMsg { Index = 1, SkinHealth = 100f, MuscleHealth = 100f },
			new CharacterLimbMsg { Index = 2, SkinHealth = 100f, MuscleHealth = 100f },
		],
	};

	private static void SendApplied(TestNode guest, TestNode host, ulong instructionId, HealResetAppliedMsg? state = null, ulong? owner = null)
	{
		state ??= HealState();
		state.OwnerSteamId = owner ?? GuestId;
		state.InstructionId = instructionId;
		guest.Services.GetRequiredService<PacketSender>().Send(HostId, NetMsg.HealResetApplied, state);
		host.Update();
	}

	private static void SendPlainReport(TestNode guest, TestNode host, float bloodVolume)
	{
		var report = SnapshotWithLimbs(GuestId, conscious: true);
		report.Health!.BloodVolume = bloodVolume;
		guest.Services.GetRequiredService<PacketSender>().Send(HostId, NetMsg.CharacterData, report);
		host.Update();
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
