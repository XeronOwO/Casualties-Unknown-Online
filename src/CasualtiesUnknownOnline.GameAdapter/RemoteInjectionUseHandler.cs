using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Patches;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The injection family's operator-side session, split out of
/// <see cref="RemoteMedicalOperationHandler"/> when Part A of
/// <c>mod-cross-player-native-semantics</c> made the operator run the item's OWN
/// native limb action: it owns the local syringe session, the dose stream the
/// native <c>WaterContainerItem.Inject</c> calls feed, and the completion/cancel
/// paths the patches, the remote view and the medical apply all reach. The
/// gesture ROUTING stays in the handler; this type owns what happens once the
/// injection family owns the gesture.
/// </summary>
internal sealed class RemoteInjectionUseHandler
{
	private static RemoteSyringeUseSession? _activeSyringe;
	private static Action<ulong>? _cancelRequestSender;
	private static Action<ulong, float>? _endRequestSender;
	private static readonly HashSet<ulong> AuthoritativeItemIds = [];

	private readonly GameAdapterDomains _domains;

	internal RemoteInjectionUseHandler(GameAdapterDomains domains)
	{
		_domains = domains;
		_cancelRequestSender = id => domains.PlayerInteraction.MedicalOperations.SendCancelRequest(id);
		_endRequestSender = (id, total) => domains.PlayerInteraction.MedicalOperations.SendEndRequest(id, total);
		domains.PlayerInteraction.MedicalOperations.StartAckReceived += OnStartAckReceived;
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

	/// <summary>
	/// One native injection call reached <c>WaterContainerItem.Inject</c> while
	/// the operator's own client was running this session's item. The ml is the
	/// dose the game itself computed (the syringe minigame's per-frame rate or a
	/// one-shot delegate's fixed amount), so it becomes the session's delta and
	/// the original call is swallowed: the host commits the drain, the patient's
	/// client applies the effect, and the display-only body copy is never
	/// mutated. Returns false for every injection this session does not own.
	/// </summary>
	internal bool TryDivertInjection(WaterContainerItem container, Limb limb, float amount)
	{
		var session = _activeSyringe;
		if (session is null || amount <= 0f || !session.Owns(container, limb))
		{
			return false;
		}

		session.Accumulate(amount);
		_domains.Log.LogDebug(
			"[MedicalView] remote syringe delivered {Delta:F3} ml (total {Total:F2} ml) for {Target}.",
			amount, session.InjectedMl, session.Target);
		return true;
	}

	/// <summary>
	/// Start the injection family's session for one accepted gesture: run the
	/// item's OWN native limb action inside the treatment capture window and let
	/// the resulting <c>WaterContainerItem.Inject</c> calls feed the dose stream.
	/// Every refusal returns false, and the native action never runs for a start
	/// the host rejected on the spot.
	/// </summary>
	internal bool TryStart(Item dragItem, int limbIndex, ulong target, ulong itemInstanceId) =>
		TryStartRemoteSyringeUse(dragItem, limbIndex, target, itemInstanceId);

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

		var water = dragItem.GetComponent<WaterContainerItem>();
		if (water == null) // Unity object — ==
		{
			_domains.Log.LogWarning("[MedicalView] refused syringe use: {ItemId} has no WaterContainerItem.", dragItem.id);
			return false;
		}

		// The dose belongs to the item's own data: its native limb action starts
		// the syringe minigame (or injects in one shot) and multiplies that frame's
		// plunger depth by its own ml/s literal, so CUO needs no dose table.
		if (!Item.GlobalItems.TryGetValue(dragItem.id, out var info) || info?.useLimbAction is null)
		{
			_domains.Log.LogWarning("[MedicalView] refused syringe use: {ItemId} has no native limb action to run.", dragItem.id);
			return false;
		}

		var limb = display.limbs[limbIndex];

		// A reused instance starts a new local lifecycle: previous authoritative
		// markers must not suppress restore-to-original on a cancel before this
		// session receives its first host state.
		AuthoritativeItemIds.Remove(itemInstanceId);
		var session = new RemoteSyringeUseSession(dragItem, water, limb, target, itemInstanceId)
		{
			Progress = (operationId, delta) =>
				_domains.PlayerInteraction.MedicalOperations.SendUpdate(operationId, delta),
		};

		_activeSyringe = session;
		_domains.PlayerInteraction.MedicalOperations.SendStartRequest(target, itemInstanceId, limbIndex);
		if (!ReferenceEquals(_activeSyringe, session))
		{
			// The start was refused locally before the native action could run (no
			// line of sight, no live item instance): nothing was injected.
			return false;
		}

		// Run the item's OWN limb action inside the treatment capture window: the
		// game decides the minigame, the item's colour and the dose, and every ml
		// its delegate hands to Inject arrives at TryDivertInjection above.
		using (NativeLimbActionScope.Enter(limb.body))
		{
			info.useLimbAction(limb, dragItem);
		}

		var minigame = MinigameBase.main?.currentMinigame;
		if (minigame is null)
		{
			// A one-shot delegate (a bloodbag injects 375 ml without a minigame):
			// the dose is already in, so this session ends here.
			_domains.Log.LogInformation(
				"[MedicalView] {ItemId} injected {Ml:F2} ml without a minigame for {Target} (id {InstanceId}).",
				dragItem.id, session.InjectedMl, target, itemInstanceId);
			CompleteOneShotUse(session);
			return true;
		}

		session.Minigame = minigame;
		_domains.Log.LogInformation("[MedicalView] started remote syringe minigame for {Target} limb {Limb} with {ItemId} (id {InstanceId}).",
			target, limbIndex, dragItem.id, itemInstanceId);
		return true;
	}

	/// <summary>
	/// The native limb action finished without leaving a minigame behind, so the
	/// session has nothing left to wait for. An ack that already arrived sends the
	/// EndRequest now; one still in flight keeps the delivered ml so its own
	/// handler sends it, instead of holding an orphaned reservation until the
	/// host's idle timeout.
	/// </summary>
	private static void CompleteOneShotUse(RemoteSyringeUseSession session)
	{
		_activeSyringe = null;
		if (session.OperationId != 0)
		{
			_endRequestSender?.Invoke(session.OperationId, session.InjectedMl);
			return;
		}

		session.EngineEnded = true;
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
		private readonly WaterContainerItem _container;
		private readonly Limb _limb;
		private readonly float _originalCondition;
		private readonly float _capacity;

		internal RemoteSyringeUseSession(Item item, WaterContainerItem container, Limb limb, ulong target, ulong itemInstanceId)
		{
			_item = item;
			_container = container;
			_limb = limb;
			_originalCondition = item.condition;
			_capacity = container.Capacity;
			Target = target;
			ItemInstanceId = itemInstanceId;
		}

		internal ulong Target { get; }

		internal ulong ItemInstanceId { get; }

		internal ulong OperationId { get; set; }

		internal float InjectedMl { get; private set; }

		internal float SentMl { get; set; }

		internal Action<ulong, float>? Progress { get; set; }

		internal Minigame? Minigame { get; set; }

		internal bool EngineEnded { get; set; }

		internal bool CancelledBeforeAck { get; set; }

		/// <summary>True when this native injection call is this session's own: the container the item's limb action was given, and the very limb it was given.</summary>
		internal bool Owns(WaterContainerItem container, Limb limb) =>
			ReferenceEquals(container, _container) && ReferenceEquals(limb, _limb);

		internal void Accumulate(float ml)
		{
			InjectedMl += ml;
			// Keep the native minigame's syringe fill moving in sync with the
			// ml delivered. The real liquid stacks are NOT changed here; the
			// host result is the only authority that drains them.
			if (_capacity > 0f)
			{
				_item.condition = Mathf.Max(0f, _originalCondition - InjectedMl / _capacity);
			}

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
			if (delta < 0.001f)
			{
				return;
			}

			// Per-frame report: every native minigame Update that actually
			// delivered more liquid sends its delta immediately. The host
			// broadcasts one authoritative State per accepted update, so the
			// target/third-party view follows the syringe at frame granularity.
			// Future adaptive flow control can lower this cadence dynamically
			// without changing the protocol (see backlog future ticket).
			Progress(OperationId, delta);
			SentMl += delta;
		}
	}
}
