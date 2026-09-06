using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Patches;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Remote-medical Stage 3 adapter. It starts the native Bandage/Dislocation/
/// AED/ManualDefib/Amputation minigames on the remote display body, routes
/// semantic updates through the generic medical operation session, and handles
/// the one-shot splint/tourniquet removal sessions. All active state is
/// exclusive to one local minigame/one operation at a time.
/// </summary>
internal sealed class RemoteOtherMedicalOperationHandler
{
	private static RemoteOtherUseSession? _active;
	private static Action<ulong, MedicalOperationUpdateAction, float, float, float, bool>? _updateSender;
	private static Action<ulong, float>? _endRequestSender;
	private static Action<ulong>? _cancelRequestSender;
	private static readonly HashSet<ulong> EndedOperationIds = [];

	private readonly GameAdapterDomains _domains;

	internal RemoteOtherMedicalOperationHandler(GameAdapterDomains domains)
	{
		_domains = domains;
		_updateSender = (id, action, v1, v2, v3, flag) =>
			domains.PlayerInteraction.MedicalOperations.SendOtherUpdate(id, action, v1, v2, v3, flag);
		_endRequestSender = (id, total) => domains.PlayerInteraction.MedicalOperations.SendOtherEndRequest(id, total);
		_cancelRequestSender = id => domains.PlayerInteraction.MedicalOperations.SendCancelRequest(id);
		domains.PlayerInteraction.MedicalOperations.StartAckReceived += OnStartAckReceived;
	}

	internal bool TryStartRemoteBandageUse(Item item, int limbIndex, ulong target, ulong itemInstanceId)
	{
		var display = RemoteMedicalView.DisplayBody;
		if (_active != null
			|| display == null // Unity object — ==
			|| limbIndex < 0
			|| limbIndex >= display.limbs.Length
			|| display.limbs[limbIndex] == null // Unity object — ==
			|| display.limbs[limbIndex].dismembered
			|| !RemoteBandageMinigameCatalog.IsBandageItem(item.id)
			|| MinigameBase.main == null // Unity object — ==
			|| MinigameBase.main.currentMinigame != null)
		{
			return false;
		}

		RemoteOtherUseSession session = null!;
		session = new RemoteOtherUseSession(target, itemInstanceId, _domains.Session.LocalSteamId)
		{
			Kind = MedicalOperationKind.Bandage,
			LimbIndex = limbIndex,
			Item = item,
			HasItem = true,
			OriginalCondition = item.condition,
			Minigame = new BandageMinigame(
				_ => ReportBandageWrap(),
				Color.white,
				display.limbs[limbIndex]),
		};

		return StartMinigameSession(session, item);
	}

	internal bool TryStartRemoteDislocationUse(Limb limb, bool wrench, Item? item, ulong target, ulong itemInstanceId)
	{
		var display = RemoteMedicalView.DisplayBody;
		if (_active != null
			|| display == null // Unity object — ==
			|| limb == null // Unity object — ==
			|| limb.dismembered
			|| !limb.dislocated
			|| MinigameBase.main == null // Unity object — ==
			|| MinigameBase.main.currentMinigame != null)
		{
			return false;
		}

		var session = new RemoteOtherUseSession(target, itemInstanceId, _domains.Session.LocalSteamId)
		{
			Kind = MedicalOperationKind.Dislocation,
			LimbIndex = Array.IndexOf(display.limbs, limb),
			Item = item,
			HasItem = item != null,
			OriginalCondition = item != null ? item.condition : 0f,
			Minigame = new DislocationMinigame(limb, wrench),
			Wrench = wrench,
		};

		return StartMinigameSession(session, item);
	}

	internal bool TryStartRemoteAedUse(Item item, int limbIndex, ulong target, ulong itemInstanceId)
	{
		var display = RemoteMedicalView.DisplayBody;
		if (_active != null
			|| display == null // Unity object — ==
			|| limbIndex < 0
			|| limbIndex >= display.limbs.Length
			|| display.limbs[limbIndex] == null // Unity object — ==
			|| MinigameBase.main == null // Unity object — ==
			|| MinigameBase.main.currentMinigame != null)
		{
			return false;
		}

		var session = new RemoteOtherUseSession(target, itemInstanceId, _domains.Session.LocalSteamId)
		{
			Kind = MedicalOperationKind.Aed,
			LimbIndex = limbIndex,
			Item = item,
			HasItem = true,
			OriginalCondition = item.condition,
			Minigame = new AEDMinigame(display.limbs[limbIndex]),
		};

		return StartMinigameSession(session, item);
	}

	internal bool TryStartRemoteManualDefibUse(Item item, int limbIndex, ulong target, ulong itemInstanceId)
	{
		var display = RemoteMedicalView.DisplayBody;
		if (_active != null
			|| display == null // Unity object — ==
			|| limbIndex < 0
			|| limbIndex >= display.limbs.Length
			|| display.limbs[limbIndex] == null // Unity object — ==
			|| MinigameBase.main == null // Unity object — ==
			|| MinigameBase.main.currentMinigame != null)
		{
			return false;
		}

		var session = new RemoteOtherUseSession(target, itemInstanceId, _domains.Session.LocalSteamId)
		{
			Kind = MedicalOperationKind.ManualDefib,
			LimbIndex = limbIndex,
			Item = item,
			HasItem = true,
			OriginalCondition = item.condition,
			Minigame = new ManualDefibMinigame(display.limbs[limbIndex]),
		};

		return StartMinigameSession(session, item);
	}

	internal bool TryStartRemoteAmputationUse(Item item, int limbIndex, ulong target, ulong itemInstanceId)
	{
		var display = RemoteMedicalView.DisplayBody;
		if (_active != null
			|| display == null // Unity object — ==
			|| limbIndex < 0
			|| limbIndex >= display.limbs.Length
			|| display.limbs[limbIndex] == null // Unity object — ==
			|| display.limbs[limbIndex].dismembered
			|| !RemoteOtherMedicalCatalog.IsAmputationTool(item.id)
			|| MinigameBase.main == null // Unity object — ==
			|| MinigameBase.main.currentMinigame != null)
		{
			return false;
		}

		var session = new RemoteOtherUseSession(target, itemInstanceId, _domains.Session.LocalSteamId)
		{
			Kind = MedicalOperationKind.Amputation,
			LimbIndex = limbIndex,
			Item = item,
			HasItem = true,
			OriginalCondition = item.condition,
			Minigame = new AmputationMinigame(display.limbs[limbIndex]),
		};

		return StartMinigameSession(session, item);
	}

	internal bool TryStartRemoteRemoval(Limb limb, MedicalOperationKind kind)
	{
		if (_active != null
			|| RemoteMedicalView.DisplayBody is not { } display
			|| limb == null // Unity object — ==
			|| limb.dismembered)
		{
			return false;
		}

		var session = new RemoteOtherUseSession(RemoteMedicalView.TargetSteamId, 0, _domains.Session.LocalSteamId)
		{
			Kind = kind,
			LimbIndex = Array.IndexOf(display.limbs, limb),
			Removal = true,
		};

		_active = session;
		_domains.PlayerInteraction.MedicalOperations.SendOtherStartRequest(
			session.Target, 0, session.LimbIndex, session.Kind);
		_domains.Log.LogInformation("[MedicalView] started remote {Kind} removal for {Target} limb {Limb}.",
			kind, session.Target, session.LimbIndex);
		return true;
	}

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
			// A terminal for a previous operation arrived while a new session is
			// still waiting for its StartAck. It must not kill the new pending
			// session.
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

	private bool StartMinigameSession(RemoteOtherUseSession session, Item? item)
	{
		if (session.ItemInstanceId != 0)
		{
			RemoteOtherMedicalItemRestore.Clear(session.ItemInstanceId);
		}

		MinigameBase.main.StartMinigame(session.Minigame!, item);
		if (!ReferenceEquals(MinigameBase.main.currentMinigame, session.Minigame))
		{
			return false;
		}

		_active = session;
		RemoteOtherMedicalMinigamePatch.Reset();
		_domains.PlayerInteraction.MedicalOperations.SendOtherStartRequest(
			session.Target, session.ItemInstanceId, session.LimbIndex, session.Kind);
		_domains.Log.LogInformation("[MedicalView] started remote {Kind} minigame for {Target} limb {Limb} (item {ItemId}).",
			session.Kind, session.Target, session.LimbIndex, item?.id ?? "-");
		return true;
	}

	private void OnStartAckReceived(MedicalOperationStartAckMsg msg)
	{
		var session = _active;
		if (session == null)
		{
			return;
		}

		if (!msg.Accepted)
		{
			_domains.Log.LogWarning("[MedicalView] remote {Kind} start rejected for {Target}: {Reason}.",
				msg.Kind, msg.TargetSteamId, msg.RejectReason);
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
		if (session.OperationId == 0)
		{
			return;
		}

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
