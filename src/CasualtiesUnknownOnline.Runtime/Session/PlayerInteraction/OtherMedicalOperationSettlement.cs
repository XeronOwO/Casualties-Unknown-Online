using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The settlement of a Stage-3 operation: the one place a session ends. It drains the
/// defib time the terminal has not measured yet (<see cref="PrepareTerminal"/>), removes
/// the session from the registry, releases its claims, applies the effect that SETTLES
/// the unit, and broadcasts the one terminal a client renders; when the unit resolves
/// once it also answers and stops every other operation still open on the same
/// (target, limb, kind). The session service keeps the registry, the start/update
/// entries and the client surface, and hands this class the session table — so the
/// terminal rules grow here instead of inside the 580-line service.
/// </summary>
internal sealed class OtherMedicalOperationSettlement(
	ISessionControl session,
	PacketSender sender,
	MedicalOperationClaims claims,
	ITimeSource time,
	OtherMedicalOperationApplier applier,
	OtherMedicalRemovalApplier removal,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly MedicalOperationClaims _claims = claims;
	private readonly ITimeSource _time = time;
	private readonly OtherMedicalOperationApplier _applier = applier;
	private readonly OtherMedicalRemovalApplier _removal = removal;
	private readonly ILogger _log = log;

	/// <summary>Raised once per settled session; the service forwards it to its own subscribers and the wire.</summary>
	public event Action<MedicalOperationEndCommittedMsg>? EndCommitted;

	/// <summary>The drain that must run before a terminal is measured: a manual defib's time is credited first.</summary>
	public void PrepareTerminal(OtherMedicalOperationSession session)
	{
		if (session.Kind == MedicalOperationKind.ManualDefib)
		{
			_applier.DrainManualDefibTime(session, _time.NowMs);
		}
	}

	/// <summary>Ends the session exactly once: claim release, the settling effect, the terminal broadcast, then the sibling stop.</summary>
	public void Terminate(
		Dictionary<ulong, OtherMedicalOperationSession> sessions,
		OtherMedicalOperationSession session,
		MedicalOperationTerminalReason reason,
		bool dislocateSucceeded = false)
	{
		if (!sessions.Remove(session.OperationId))
		{
			return;
		}

		ReleaseSession(session);
		var settled = CompleteUnitOnTerminal(session, reason, dislocateSucceeded);
		var end = _applier.BuildTerminal(session, reason);
		_log.LogInformation("[MedicalOps3] operation {OperationId} terminal {Reason}: progress {Progress:F3}.",
			session.OperationId, reason, session.Progress);
		EndCommitted?.Invoke(end);
		_sender.SendToAll(
			_session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId).Select(m => m.SteamId),
			NetMsg.MedicalOperationEndCommitted,
			end);

		if (settled && MedicalOperationUnitRules.ResolvesOnce(session.Kind))
		{
			StopOtherOperatorsOnTheUnit(sessions, session);
		}
	}

	/// <summary>
	/// The effect that SETTLES the unit, in one place: the two removal kinds remove
	/// only on a completed end (a cancel or a timeout leaves the splint on the limb),
	/// while an amputation or a relocation finishes at its terminal whatever the
	/// reason, because its progress was already committed. The result is what makes
	/// the unit's single outcome visible to the sweep.
	/// </summary>
	private bool CompleteUnitOnTerminal(OtherMedicalOperationSession session, MedicalOperationTerminalReason reason, bool dislocateSucceeded) =>
		session.Kind switch
		{
			MedicalOperationKind.SplintRemoval or MedicalOperationKind.TourniquetRemoval
				when reason == MedicalOperationTerminalReason.Completed => _removal.Remove(session),
			MedicalOperationKind.Amputation => session.Progress >= 1f && _applier.CompleteAmputation(session),
			MedicalOperationKind.Dislocation => _applier.CompleteDislocation(session, dislocateSucceeded),
			_ => false,
		};

	/// <summary>
	/// A unit that resolves once has now been settled by this operation, so every
	/// OTHER operation still open on the same (target, limb, kind) is answered and
	/// stopped: its operator gets the precise "already handled" terminal carrying the
	/// authoritative state this completion produced, and its minigame ends there
	/// instead of working a limb that is already treated.
	/// </summary>
	private void StopOtherOperatorsOnTheUnit(
		Dictionary<ulong, OtherMedicalOperationSession> sessions,
		OtherMedicalOperationSession winner)
	{
		var reason = MedicalOperationUnitRules.HandledReason(winner.Kind);
		var others = sessions.Values
			.Where(s => s.OperationId != winner.OperationId
				&& s.Target == winner.Target
				&& s.LimbIndex == winner.LimbIndex
				&& s.Kind == winner.Kind)
			.ToList();
		foreach (var other in others)
		{
			_log.LogInformation(
				"[MedicalOps3] stopping operation {OperationId} ({Kind}) on {Target} limb {Limb}: {Reason}",
				other.OperationId, other.Kind, other.Target, other.LimbIndex, reason);
			PrepareTerminal(other);
			Terminate(sessions, other, MedicalOperationTerminalReason.AlreadyHandled);
		}
	}

	/// <summary>Releases the operator and item claims a session held.</summary>
	public void ReleaseSession(OtherMedicalOperationSession session)
	{
		_claims.ReleaseOperator(session.Operator);
		if (session.ItemInstanceId != 0)
		{
			_claims.ReleaseItem(session.ItemInstanceId);
		}
	}
}
