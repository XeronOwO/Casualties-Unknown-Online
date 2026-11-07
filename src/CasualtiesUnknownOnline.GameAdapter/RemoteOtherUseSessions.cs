using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Patches;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The one remote Stage-3 use session: which kind is running, what the operator still owes the host,
/// and what happens when the engine, the host or the operator ends it. It is the bookkeeping half of
/// the Stage-3 adapter; <see cref="RemoteOtherMedicalOperationHandler"/> starts a kind's native
/// minigame and hands the session here.
/// </summary>
internal static class RemoteOtherUseSessions
{
	private static RemoteOtherUseSession? _active;
	private static Action<ulong, MedicalOperationUpdateAction, float, float, float, bool>? _updateSender;
	private static Action<ulong, float>? _endRequestSender;
	private static Action<ulong>? _cancelRequestSender;
	private static ILogger? _log;
	private static readonly HashSet<ulong> EndedOperationIds = [];

	internal static void Initialize(IMedicalOperationControl operations, ILogger log)
	{
		_updateSender = (id, action, v1, v2, v3, flag) =>
			operations.SendOtherUpdate(id, action, v1, v2, v3, flag);
		_endRequestSender = (id, total) => operations.SendOtherEndRequest(id, total);
		_cancelRequestSender = id => operations.SendCancelRequest(id);
		_log = log;
	}

	internal static bool IsActive => _active != null;

	internal static void Arm(RemoteOtherUseSession session) => _active = session;

	internal static void CompleteActiveUse()
	{
		var session = _active;
		if (session == null)
		{
			return;
		}

		if (session.OperationId != 0)
		{
			if (session.Kind == MedicalOperationKind.Dislocation
				&& session.Minigame is DislocationMinigame dislocation)
			{
				var limb = Traverse.Create(dislocation).Field("limb").GetValue<Limb>();
				session.DislocationSucceeded = limb != null && limb.dislocationTimer < 3f;
			}

			var total = session.Kind switch
			{
				MedicalOperationKind.Amputation => session.Progress,
				MedicalOperationKind.Bandage => session.Progress,
				MedicalOperationKind.Dislocation => session.DislocationSucceeded ? 1f : 0f,
				_ => 0f,
			};
			RemoteOtherMedicalMinigamePatch.Reset();
			EndedOperationIds.Add(session.OperationId);
			_active = null;
			_endRequestSender?.Invoke(session.OperationId, total);
			return;
		}

		session.EngineEnded = true;
	}

	internal static bool CancelActiveUse()
	{
		var session = _active;
		if (session == null)
		{
			return false;
		}

		if (session.OperationId != 0)
		{
			EndedOperationIds.Add(session.OperationId);
			_active = null;
			_cancelRequestSender?.Invoke(session.OperationId);
			RemoteOtherMedicalItemRestore.Restore(session);
		}
		else
		{
			// Ack is still pending: keep the session so the later ack can send
			// the cancel; the host must never hold an orphaned reservation.
			session.CancelledBeforeAck = true;
			session.EngineEnded = true;
			RemoteOtherMedicalItemRestore.Restore(session);
		}

		RemoteOtherMedicalMinigamePatch.Reset();
		if (session.Minigame != null
			&& MinigameBase.main != null // Unity object — ==
			&& ReferenceEquals(MinigameBase.main.currentMinigame, session.Minigame))
		{
			MinigameBase.main.EndMinigame();
		}

		return true;
	}

	internal static void OnHostTerminal(ulong operationId)
	{
		var session = _active;
		if (session == null)
		{
			EndedOperationIds.Remove(operationId);
			return;
		}

		if (session.OperationId != 0)
		{
			if (session.OperationId != operationId)
			{
				return;
			}

			EndedOperationIds.Remove(operationId);
		}
		else if (EndedOperationIds.Contains(operationId))
		{
			// A terminal for a previous operation, while a new session still waits for its StartAck: it must not kill the pending one.
			EndedOperationIds.Remove(operationId);
			return;
		}

		_active = null;
		RemoteOtherMedicalItemRestore.Restore(session);
		RemoteOtherMedicalMinigamePatch.Reset();
		if (session.Minigame != null
			&& MinigameBase.main != null // Unity object — ==
			&& ReferenceEquals(MinigameBase.main.currentMinigame, session.Minigame))
		{
			MinigameBase.main.EndMinigame();
		}
	}

	internal static bool IsActiveAmputation(AmputationMinigame minigame) =>
		_active is { Kind: MedicalOperationKind.Amputation } && ReferenceEquals(_active.Minigame, minigame);

	internal static bool IsActiveDislocation(DislocationMinigame minigame) =>
		_active is { Kind: MedicalOperationKind.Dislocation } && ReferenceEquals(_active.Minigame, minigame);

	internal static bool IsActiveAed(AEDMinigame minigame) =>
		_active is { Kind: MedicalOperationKind.Aed } && ReferenceEquals(_active.Minigame, minigame);

	internal static bool IsActiveManualDefib() =>
		_active is { Kind: MedicalOperationKind.ManualDefib };

	internal static void ReportBandageWrap()
	{
		if (_active is not { Kind: MedicalOperationKind.Bandage } session)
		{
			return;
		}

		if (session.OperationId != 0)
		{
			// One wrap is one eighteenth of the treatment, and the LOCAL total must advance with it: the end request carries `session.Progress`, so a total that stayed 0 committed the operation as 0 even after eighteen wraps reached the host (measured 2026-11-01: `terminal TimedOut: progress 1.0`).
			session.Progress = Math.Min(1f, session.Progress + (1f / 18f));
			_updateSender?.Invoke(session.OperationId, MedicalOperationUpdateAction.Wrap, 1f, 0f, 0f, false);
		}
		else
		{
			session.PendingWraps++;
		}
	}

	internal static void ReportDislocationHit(bool wrench)
	{
		if (_active is not { Kind: MedicalOperationKind.Dislocation } session)
		{
			return;
		}

		if (session.OperationId != 0)
		{
			_updateSender?.Invoke(session.OperationId, MedicalOperationUpdateAction.Hit, 0f, 0f, 0f, wrench);
		}
		else
		{
			session.PendingHits++;
		}
	}

	internal static void ReportAmputationCut(float delta)
	{
		if (_active is not { Kind: MedicalOperationKind.Amputation } session)
		{
			return;
		}

		if (session.OperationId != 0)
		{
			session.Progress = Math.Min(1f, session.Progress + delta);
			_updateSender?.Invoke(session.OperationId, MedicalOperationUpdateAction.Cut, delta, 0f, 0f, false);
		}
		else
		{
			session.PendingCut = Math.Min(1f, session.PendingCut + delta);
		}
	}

	internal static void ReportAedStart() =>
		ReportAedStage(0f);

	internal static void ReportAedAnalyze() =>
		ReportAedStage(1f);

	internal static void ReportAedShock()
	{
		if (_active is not { Kind: MedicalOperationKind.Aed } session)
		{
			return;
		}

		if (session.OperationId != 0)
		{
			_updateSender?.Invoke(session.OperationId, MedicalOperationUpdateAction.Shock, 0f, 0f, 0f, false);
		}
		else
		{
			session.PendingAedShock = 1;
		}
	}

	internal static void ReportManualShock(float charge)
	{
		if (_active is not { Kind: MedicalOperationKind.ManualDefib } session)
		{
			return;
		}

		if (session.OperationId != 0)
		{
			_updateSender?.Invoke(session.OperationId, MedicalOperationUpdateAction.Shock, charge, 0f, 0f, false);
		}
		else
		{
			session.PendingManualShock = charge;
		}
	}

	private static void ReportAedStage(float stageCode)
	{
		if (_active is not { Kind: MedicalOperationKind.Aed } session)
		{
			return;
		}

		if (session.OperationId != 0)
		{
			_updateSender?.Invoke(session.OperationId, MedicalOperationUpdateAction.Stage, stageCode, 0f, 0f, false);
		}
		else if (stageCode <= 0.5f)
		{
			session.PendingAedStart = 1;
		}
		else
		{
			session.PendingAedAnalyze = 1;
		}
	}

	internal static void OnStartAckReceived(MedicalOperationStartAckMsg msg)
	{
		var session = _active;
		if (session == null) { return; }

		if (!msg.Accepted)
		{
			_log?.LogWarning("[MedicalView] remote {Kind} start rejected for {Target}: {Reason}.",
				msg.Kind, msg.TargetSteamId, msg.RejectReason);

			// Only the rejection of THIS session may clear it: the first release is refused for line of sight and the retry starts a new one, so a stale rejection would take the LIVE session down and its wraps would never leave (0.088 on one run, 0.000 on the next).
			if (msg.TargetSteamId != session.Target || msg.ItemInstanceId != session.ItemInstanceId || msg.Kind != session.Kind)
			{
				_log?.LogInformation("[MedicalView] ignored a stale {Kind} rejection for {Target} — it names a session this client is not running.", msg.Kind, msg.TargetSteamId);
				return;
			}

			_active = null;
			RemoteOtherMedicalItemRestore.Restore(session);
			RemoteOtherMedicalMinigamePatch.Reset();
			if (session.Minigame != null
				&& MinigameBase.main != null // Unity object — ==
				&& ReferenceEquals(MinigameBase.main.currentMinigame, session.Minigame))
			{
				MinigameBase.main.EndMinigame();
			}

			return;
		}

		if (session.Target != msg.TargetSteamId || session.ItemInstanceId != msg.ItemInstanceId)
		{
			return;
		}

		session.OperationId = msg.OperationId;
		FlushPending(session);
		if (session.CancelledBeforeAck)
		{
			EndedOperationIds.Add(msg.OperationId);
			_active = null;
			RemoteOtherMedicalMinigamePatch.Reset();
			_cancelRequestSender?.Invoke(msg.OperationId);
			RemoteOtherMedicalItemRestore.Restore(session);
			return;
		}

		if (session.EngineEnded)
		{
			if (session.Kind == MedicalOperationKind.Dislocation
				&& session.Minigame is DislocationMinigame dislocation)
			{
				var limb = Traverse.Create(dislocation).Field("limb").GetValue<Limb>();
				session.DislocationSucceeded = limb != null && limb.dislocationTimer < 3f;
			}

			EndedOperationIds.Add(msg.OperationId);
			_active = null;
			RemoteOtherMedicalMinigamePatch.Reset();
			var endTotal = session.Kind switch
			{
				MedicalOperationKind.Amputation => session.Progress,
				MedicalOperationKind.Dislocation => session.DislocationSucceeded ? 1f : 0f,
				_ => 0f,
			};
			_endRequestSender?.Invoke(msg.OperationId, endTotal);
			return;
		}

		if (session.Removal)
		{
			EndedOperationIds.Add(msg.OperationId);
			_active = null;
			RemoteOtherMedicalMinigamePatch.Reset();
			_endRequestSender?.Invoke(msg.OperationId, 0f);
			return;
		}

		if (session.Kind == MedicalOperationKind.Aed)
		{
			ReportAedStart();
		}
	}

	private static void FlushPending(RemoteOtherUseSession session)
	{
		if (session.OperationId == 0) { return; }

		while (session.PendingWraps > 0)
		{
			_updateSender?.Invoke(session.OperationId, MedicalOperationUpdateAction.Wrap, 1f, 0f, 0f, false);
			session.PendingWraps--;
		}

		while (session.PendingHits > 0)
		{
			_updateSender?.Invoke(session.OperationId, MedicalOperationUpdateAction.Hit, 0f, 0f, 0f, false);
			session.PendingHits--;
		}

		if (session.PendingCut > 0f)
		{
			var delta = session.PendingCut;
			session.Progress = Math.Min(1f, session.Progress + delta);
			_updateSender?.Invoke(session.OperationId, MedicalOperationUpdateAction.Cut, delta, 0f, 0f, false);
			session.PendingCut = 0f;
		}

		if (session.PendingAedStart > 0 && session.PendingAedAnalyze > 0)
		{
			_updateSender?.Invoke(session.OperationId, MedicalOperationUpdateAction.Stage, 0f, 0f, 0f, false);
			_updateSender?.Invoke(session.OperationId, MedicalOperationUpdateAction.Stage, 1f, 0f, 0f, false);
			session.PendingAedStart = 0;
			session.PendingAedAnalyze = 0;
		}
		else if (session.PendingAedStart > 0)
		{
			_updateSender?.Invoke(session.OperationId, MedicalOperationUpdateAction.Stage, 0f, 0f, 0f, false);
			session.PendingAedStart = 0;
		}
		else if (session.PendingAedAnalyze > 0)
		{
			_updateSender?.Invoke(session.OperationId, MedicalOperationUpdateAction.Stage, 1f, 0f, 0f, false);
			session.PendingAedAnalyze = 0;
		}

		if (session.PendingAedShock > 0)
		{
			_updateSender?.Invoke(session.OperationId, MedicalOperationUpdateAction.Shock, 0f, 0f, 0f, false);
			session.PendingAedShock = 0;
		}

		if (session.PendingManualShock > 0f)
		{
			_updateSender?.Invoke(session.OperationId, MedicalOperationUpdateAction.Shock, session.PendingManualShock, 0f, 0f, false);
			session.PendingManualShock = 0f;
		}
	}
}
