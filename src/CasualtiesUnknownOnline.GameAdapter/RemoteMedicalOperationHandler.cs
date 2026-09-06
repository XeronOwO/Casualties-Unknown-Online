using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Remote-medical WoundView gesture routing. It maps a local dragged medical
/// item released on a remote body-limb diagram to the host-authoritative
/// medical operation session and keeps the display-only remote body copy
/// untouched. Injectable/IV medicines are routed through the native syringe
/// minigame first: the acting player physically pumps the amount, the operator
/// sends incremental deltas while the minigame is running, and the host is the
/// only authority for the committed item/target state.
/// </summary>
internal sealed class RemoteMedicalOperationHandler
{
	private static RemoteSyringeUseSession? _activeSyringe;
	private static Action<ulong>? _cancelRequestSender;
	private static Action<ulong, float>? _endRequestSender;
	private static readonly HashSet<ulong> AuthoritativeItemIds = [];

	private readonly GameAdapterDomains _domains;

	internal RemoteMedicalOperationHandler(GameAdapterDomains domains)
	{
		_domains = domains;
		_cancelRequestSender = id => domains.PlayerInteraction.MedicalOperations.SendCancelRequest(id);
		_endRequestSender = (id, total) => domains.PlayerInteraction.MedicalOperations.SendEndRequest(id, total);
		domains.PlayerInteraction.MedicalOperations.StartAckReceived += OnStartAckReceived;
	}

	internal bool TryHandleLimbUse(Item dragItem, int limbIndex)
	{
		if (!RemoteMedicalView.IsOpen || dragItem == null) // Unity object — ==
		{
			return false;
		}

		// A remote-backpack display proxy belongs to another player's
		// inventory; in the medical view the acting player can only use their
		// own carried item on the remote subject.
		if (dragItem.GetComponent<RemoteCloneRender>() != null) // Unity object — ==
		{
			_domains.Log.LogWarning("[MedicalView] refused limb use: dragged item {ItemId} is a remote display proxy, not a local medical item.",
				dragItem.id);
			return false;
		}

		var target = RemoteMedicalView.TargetSteamId;
		if (target == 0)
		{
			_domains.Log.LogWarning("[MedicalView] refused limb use: remote medical focus has no target.");
			return false;
		}

		var instance = dragItem.GetComponent<ItemInstanceId>();
		if (instance == null || instance.Id == 0) // Unity object — ==
		{
			_domains.Log.LogWarning("[MedicalView] refused limb use: local item {ItemId} has no authoritative instance id.",
				dragItem.id);
			return false;
		}

		if (!LocalUseItemEligibility.IsMedicalLimbUseItem(dragItem))
		{
			_domains.Log.LogWarning("[MedicalView] refused limb use: {ItemId} is not a supported remote medical/limb-treatment item.",
				dragItem.id);
			return false;
		}

		if (RemoteHealProfiles.IsHealItem(dragItem.id))
		{
			_domains.PlayerInteraction.SendHealRequest(target, instance.Id, limbIndex);
			_domains.Log.LogInformation("[MedicalView] requested heal of {Target} limb {Limb} with {ItemId} (id {InstanceId}).",
				target, limbIndex, dragItem.id, instance.Id);
			return true;
		}

		if (RemoteMedicineCatalog.IsInjectableItem(dragItem.id))
		{
			return TryStartRemoteSyringeUse(dragItem, limbIndex, target, instance.Id);
		}

		_domains.PlayerInteraction.SendUseRequest(target, instance.Id, limbIndex);
		_domains.Log.LogInformation("[MedicalView] requested use of {Target} limb {Limb} with {ItemId} (id {InstanceId}).",
			target, limbIndex, dragItem.id, instance.Id);
		return true;
	}

	/// <summary>
	/// Called when the native minigame system ends the active remote syringe
	/// minigame. The exact physically delivered ml is sent as one EndRequest;
	/// the host reconciles it against all incremental updates and emits the
	/// single authoritative EndCommitted. An empty minigame still ends the
	/// session so reservations are released.
	/// </summary>
	internal static void CompleteActiveSyringeUse()
	{
		var session = _activeSyringe;
		if (session == null)
		{
			return;
		}

		if (session.OperationId != 0)
		{
			_activeSyringe = null;
			_endRequestSender?.Invoke(session.OperationId, session.InjectedMl);
			return;
		}

		// The start acknowledgement has not arrived yet. Keep the session alive
		// so the later ack can either send EndRequest (the minigame completed)
		// or CancelRequest (the view closed before the ack). Otherwise the host
		// would hold an orphaned reservation until timeout.
		session.EngineEnded = true;
		if (!RemoteMedicalView.IsOpen)
		{
			session.CancelledBeforeAck = true;
			RestoreIfNotAuthoritative(session);
		}
	}

	/// <summary>
	/// Cancel an in-flight remote syringe session without sending a request.
	/// Used when the remote medical focus closes before the minigame ends;
	/// returns true when a session was active so the caller can also end the
	/// native minigame. Committed progress is not rolled back — the host keeps
	/// the already-reported ml and releases the session on the cancel message.
	/// </summary>
	internal static bool CancelActiveSyringeUse()
	{
		var session = _activeSyringe;
		if (session == null)
		{
			return false;
		}

		var minigame = session.Minigame;

		if (session.OperationId != 0)
		{
			_activeSyringe = null;
			_cancelRequestSender?.Invoke(session.OperationId);
			RestoreIfNotAuthoritative(session);
		}
		else
		{
			// Ack is still pending. Keep the session so the later ack can send
			// the cancel request; the host must never hold an orphaned item.
			session.CancelledBeforeAck = true;
			session.EngineEnded = true;
			RestoreIfNotAuthoritative(session);
		}

		// End only the exact minigame this session started; never tear down an
		// unrelated native minigame that happened to be active after a stale
		// session leaked.
		if (minigame != null
			&& MinigameBase.main != null // Unity object — ==
			&& ReferenceEquals(MinigameBase.main.currentMinigame, minigame))
		{
			MinigameBase.main.EndMinigame();
		}

		return true;
	}

	/// <summary>
	/// The host's authoritative item-after state was applied to the local item.
	/// Cancel/close must not revert a real drained item back to its original
	/// condition.
	/// </summary>
	internal static void MarkAuthoritativeItemApplied(ulong itemInstanceId) =>
		AuthoritativeItemIds.Add(itemInstanceId);

	/// <summary>
	/// A host-initiated terminal arrived (timeout/disconnect/remote cancel):
	/// tear down the local native minigame and stop sending updates so the
	/// operator cannot keep injecting into a session the host already closed.
	/// </summary>
	internal static void OnHostTerminal(ulong operationId)
	{
		var session = _activeSyringe;
		if (session == null)
		{
			return;
		}

		// Before the start ack is received the local session has no operation
		// id yet; with only one active local minigame at a time, any terminal
		// result that arrives before the ack belongs to this pending session.
		if (session.OperationId != 0 && session.OperationId != operationId)
		{
			return;
		}

		_activeSyringe = null;
		RestoreIfNotAuthoritative(session);
		EndMinigameIfCurrent(session);
	}

	private bool TryStartRemoteSyringeUse(Item dragItem, int limbIndex, ulong target, ulong itemInstanceId)
	{
		if (_activeSyringe != null)
		{
			return false;
		}

		var display = RemoteMedicalView.DisplayBody;
		if (display == null // Unity object — ==
			|| limbIndex < 0
			|| limbIndex >= display.limbs.Length
			|| display.limbs[limbIndex] == null // Unity object — ==
			|| display.limbs[limbIndex].dismembered)
		{
			_domains.Log.LogWarning("[MedicalView] refused syringe use: no valid non-dismembered display limb {Limb}.", limbIndex);
			return false;
		}

		if (MinigameBase.main == null // Unity object — ==
			|| MinigameBase.main.currentMinigame != null)
		{
			_domains.Log.LogWarning("[MedicalView] refused syringe use: no free native minigame host for {Target}.", target);
			return false;
		}

		if (!RemoteMedicineCatalog.TryGetInjectionAmount(dragItem.id, out var fullDose) || fullDose <= 0f)
		{
			_domains.Log.LogWarning("[MedicalView] refused syringe use: {ItemId} has no known injection amount.", dragItem.id);
			return false;
		}

		var water = dragItem.GetComponent<WaterContainerItem>();
		if (water == null) // Unity object — ==
		{
			_domains.Log.LogWarning("[MedicalView] refused syringe use: {ItemId} has no WaterContainerItem.", dragItem.id);
			return false;
		}

		// A reused instance starts a new local lifecycle: previous authoritative
		// markers must not suppress restore-to-original on a cancel before this
		// session receives its first host state.
		AuthoritativeItemIds.Remove(itemInstanceId);
		var session = new RemoteSyringeUseSession(dragItem, water.Capacity, fullDose, target, itemInstanceId)
		{
			Progress = (operationId, delta) =>
				_domains.PlayerInteraction.MedicalOperations.SendUpdate(operationId, delta),
		};

		var minigame = new SyringeMinigame(
			mult => session.Accumulate(mult * fullDose),
			display.limbs[limbIndex],
			water.AverageColor());

		MinigameBase.main.StartMinigame(minigame, dragItem);
		if (!ReferenceEquals(MinigameBase.main.currentMinigame, minigame))
		{
			// StartMinigame refused (another minigame won a race after the
			// check above); never leave a session that cannot be completed.
			return false;
		}

		session.Minigame = minigame;
		_activeSyringe = session;
		_domains.PlayerInteraction.MedicalOperations.SendStartRequest(target, itemInstanceId, limbIndex);
		_domains.Log.LogInformation("[MedicalView] started remote syringe minigame for {Target} limb {Limb} with {ItemId} (id {InstanceId}).",
			target, limbIndex, dragItem.id, itemInstanceId);
		return true;
	}

	private void OnStartAckReceived(MedicalOperationStartAckMsg msg)
	{
		var session = _activeSyringe;
		if (session == null)
		{
			return;
		}

		if (!msg.Accepted)
		{
			_domains.Log.LogWarning("[MedicalView] remote syringe start rejected for {Target}: {Reason}.",
				msg.TargetSteamId, msg.RejectReason);
			_activeSyringe = null;
			RestoreIfNotAuthoritative(session);
			EndMinigameIfCurrent(session);
			return;
		}

		if (session.Target != msg.TargetSteamId || session.ItemInstanceId != msg.ItemInstanceId)
		{
			return;
		}

		session.OperationId = msg.OperationId;

		if (session.CancelledBeforeAck)
		{
			_domains.Log.LogInformation("[MedicalView] remote syringe session {OperationId} was cancelled before its ack; sending cancel.", msg.OperationId);
			_activeSyringe = null;
			_cancelRequestSender?.Invoke(msg.OperationId);
			RestoreIfNotAuthoritative(session);
			return;
		}

		if (session.EngineEnded)
		{
			_domains.Log.LogInformation("[MedicalView] remote syringe session {OperationId} ended before its ack; sending end.", msg.OperationId);
			_activeSyringe = null;
			_endRequestSender?.Invoke(msg.OperationId, session.InjectedMl);
			return;
		}

		var pending = session.InjectedMl - session.SentMl;
		if (pending > 0.01f && session.Progress is not null)
		{
			session.Progress(msg.OperationId, pending);
			session.SentMl += pending;
		}

		_domains.Log.LogInformation("[MedicalView] remote syringe session {OperationId} accepted for {Target}.", msg.OperationId, msg.TargetSteamId);
	}

	private static void EndMinigameIfCurrent(RemoteSyringeUseSession session)
	{
		if (session.Minigame != null
			&& MinigameBase.main != null // Unity object — ==
			&& ReferenceEquals(MinigameBase.main.currentMinigame, session.Minigame))
		{
			MinigameBase.main.EndMinigame();
		}
	}

	private static void RestoreIfNotAuthoritative(RemoteSyringeUseSession session)
	{
		if (!AuthoritativeItemIds.Contains(session.ItemInstanceId))
		{
			session.RestoreCondition();
		}
	}

	private sealed class RemoteSyringeUseSession
	{
		private readonly Item _item;
		private readonly float _originalCondition;
		private readonly float _capacity;

		internal RemoteSyringeUseSession(Item item, float capacity, float fullDose, ulong target, ulong itemInstanceId)
		{
			_item = item;
			_originalCondition = item.condition;
			_capacity = capacity > 0f ? capacity : fullDose;
			Target = target;
			ItemInstanceId = itemInstanceId;
		}

		internal ulong Target { get; }

		internal ulong ItemInstanceId { get; }

		internal ulong OperationId { get; set; }

		internal float InjectedMl { get; private set; }

		internal float SentMl { get; set; }

		internal Action<ulong, float>? Progress { get; set; }

		internal SyringeMinigame? Minigame { get; set; }

		internal bool EngineEnded { get; set; }

		internal bool CancelledBeforeAck { get; set; }

		private float _lastProgressSendAt;

		internal void Accumulate(float ml)
		{
			InjectedMl += ml;
			// Keep the native minigame's syringe fill moving in sync with the
			// ml delivered. The real liquid stacks are NOT changed here; the
			// host result is the only authority that drains them.
			_item.condition = Mathf.Max(0f, _originalCondition - InjectedMl / _capacity);
			MaybeSendProgress();
		}

		internal void RestoreCondition() => _item.condition = _originalCondition;

		private void MaybeSendProgress()
		{
			if (OperationId == 0 || Progress is null)
			{
				return;
			}

			var delta = InjectedMl - SentMl;
			if (delta < 0.01f)
			{
				return;
			}

			var now = Time.realtimeSinceStartup;
			if (_lastProgressSendAt == 0f || now - _lastProgressSendAt >= 0.5f || delta >= 10f)
			{
				Progress(OperationId, delta);
				SentMl += delta;
				_lastProgressSendAt = now;
			}
		}
	}
}
