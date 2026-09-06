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
/// Remote shrapnel shared-session adapter. Starts the native
/// <see cref="ShrapnelMinigame"/> on the remote display limb, routes the
/// session join to the host, mirrors authoritative piece state into the local
/// minigame, force-ungrabs non-owner pieces and reports local piece
/// moves/releases/break-grasps to the host.
/// </summary>
internal sealed class RemoteShrapnelOperationHandler
{
	private static RemoteShrapnelUseSession? _activeShrapnel;
	private static RemoteShrapnelUseSession? _observerShrapnel;
	private static Action<ulong>? _cancelRequestSender;
	private static Action<ulong>? _shrapnelEndRequestSender;
	private static Action<ulong, ShrapnelPieceUpdate>? _shrapnelUpdateSender;

	private readonly GameAdapterDomains _domains;

	internal RemoteShrapnelOperationHandler(GameAdapterDomains domains)
	{
		_domains = domains;
		_cancelRequestSender = id => domains.PlayerInteraction.MedicalOperations.SendCancelRequest(id);
		_shrapnelEndRequestSender = id => domains.PlayerInteraction.MedicalOperations.SendShrapnelEndRequest(id);
		_shrapnelUpdateSender = (id, update) => domains.PlayerInteraction.MedicalOperations.SendShrapnelUpdate(id, update);
		domains.PlayerInteraction.MedicalOperations.StartAckReceived += OnStartAckReceived;
	}

	internal bool TryStartRemoteShrapnelUse(Item dragItem, int limbIndex, ulong target, ulong itemInstanceId)
	{
		if (_activeShrapnel != null)
		{
			return false;
		}

		var display = RemoteMedicalView.DisplayBody;
		if (display == null // Unity object — ==
			|| limbIndex < 0
			|| limbIndex >= display.limbs.Length
			|| display.limbs[limbIndex] == null // Unity object — ==
			|| display.limbs[limbIndex].dismembered
			|| !display.limbs[limbIndex].hasShrapnel)
		{
			_domains.Log.LogWarning("[MedicalView] refused shrapnel use: no valid shrapnel display limb {Limb}.", limbIndex);
			return false;
		}

		return StartRemoteShrapnel(display.limbs[limbIndex], tweezers: true, item: dragItem, target, itemInstanceId);
	}

	internal bool TryStartRemoteShrapnelSpecial(Limb limb)
	{
		if (_activeShrapnel != null)
		{
			return false;
		}

		if (!RemoteMedicalView.IsOpen
			|| limb == null // Unity object — ==
			|| limb.dismembered
			|| !limb.hasShrapnel)
		{
			return false;
		}

		return StartRemoteShrapnel(limb, tweezers: false, item: null, RemoteMedicalView.TargetSteamId, 0);
	}

	// ---- Static patch/apply seam ----

	internal static void CompleteActiveShrapnelUse()
	{
		if (_observerShrapnel is not null)
		{
			_observerShrapnel = null;
			RemoteShrapnelMinigamePatch.ResetLastHeld();
			return;
		}

		var session = _activeShrapnel;
		if (session == null)
		{
			return;
		}

		if (session.OperationId != 0)
		{
			// The native ShrapnelMinigame calls EndMinigame from inside its
			// Update method, before this minigame's Update Postfix can report
			// the final removal. Report the currently held piece at the removal
			// threshold before clearing the session and asking the host to end.
			ReportFinalHeldRemoval(session);
			RemoteShrapnelMinigamePatch.ResetLastHeld();
			_activeShrapnel = null;
			_shrapnelEndRequestSender?.Invoke(session.OperationId);
			return;
		}

		RemoteShrapnelMinigamePatch.ResetLastHeld();
		session.EngineEnded = true;
		if (!RemoteMedicalView.IsOpen)
		{
			session.CancelledBeforeAck = true;
		}
	}

	internal static bool CancelActiveShrapnelUse()
	{
		if (_observerShrapnel is { } observer)
		{
			_observerShrapnel = null;
			RemoteShrapnelMinigamePatch.ResetLastHeld();
			if (observer.Minigame != null
				&& MinigameBase.main != null // Unity object — ==
				&& ReferenceEquals(MinigameBase.main.currentMinigame, observer.Minigame))
			{
				MinigameBase.main.EndMinigame();
			}

			return true;
		}

		var session = _activeShrapnel;
		if (session == null)
		{
			return false;
		}

		var minigame = session.Minigame;
		RemoteShrapnelMinigamePatch.ResetLastHeld();
		if (session.OperationId != 0)
		{
			_activeShrapnel = null;
			_cancelRequestSender?.Invoke(session.OperationId);
		}
		else
		{
			session.CancelledBeforeAck = true;
			session.EngineEnded = true;
		}

		if (minigame != null
			&& MinigameBase.main != null // Unity object — ==
			&& ReferenceEquals(MinigameBase.main.currentMinigame, minigame))
		{
			MinigameBase.main.EndMinigame();
		}

		return true;
	}

	internal static void OnShrapnelHostTerminal(ulong operationId)
	{
		if (_observerShrapnel is { } observer
			&& (observer.OperationId == 0 || observer.OperationId == operationId))
		{
			_observerShrapnel = null;
			RemoteShrapnelMinigamePatch.ResetLastHeld();
			if (observer.Minigame != null
				&& MinigameBase.main != null // Unity object — ==
				&& ReferenceEquals(MinigameBase.main.currentMinigame, observer.Minigame))
			{
				MinigameBase.main.EndMinigame();
			}

			return;
		}

		var session = _activeShrapnel;
		if (session == null)
		{
			return;
		}

		if (session.OperationId != 0 && session.OperationId != operationId)
		{
			return;
		}

		RemoteShrapnelMinigamePatch.ResetLastHeld();
		_activeShrapnel = null;
		var minigame = session.Minigame;
		if (minigame != null
			&& MinigameBase.main != null // Unity object — ==
			&& ReferenceEquals(MinigameBase.main.currentMinigame, minigame))
		{
			MinigameBase.main.EndMinigame();
		}
	}

	internal static void ApplyShrapnelState(MedicalOperationStateMsg msg)
	{
		if (_activeShrapnel is { } active
			&& (active.OperationId == 0 || active.OperationId == msg.OperationId))
		{
			active.AuthoritativePieces = [.. msg.ShrapnelPieces];
			ApplyStateToMinigame(active, msg);
			return;
		}

		if (_observerShrapnel is { } observer
			&& (observer.OperationId == 0 || observer.OperationId == msg.OperationId))
		{
			ApplyStateToMinigame(observer, msg);
			return;
		}

		if (_activeShrapnel is null
			&& RemoteMedicalView.IsOpen
			&& RemoteMedicalView.TargetSteamId == msg.TargetSteamId
			&& TryStartObserver(msg) is { } newObserver)
		{
			ApplyStateToMinigame(newObserver, msg);
		}
	}

	private static void ApplyStateToMinigame(RemoteShrapnelUseSession session, MedicalOperationStateMsg msg)
	{
		if (session.Minigame == null
			|| MinigameBase.main == null // Unity object — ==
			|| !ReferenceEquals(MinigameBase.main.currentMinigame, session.Minigame))
		{
			return;
		}

		var traverse = Traverse.Create(session.Minigame);
		var objects = traverse.Field("objects").GetValue<List<RectTransform>>();
		if (objects is null)
		{
			return;
		}

		var held = traverse.Field("currentlyHeld").GetValue<RectTransform>();
		foreach (var piece in msg.ShrapnelPieces)
		{
			if (piece.PieceIndex < 0 || piece.PieceIndex >= objects.Count)
			{
				continue;
			}

			var rect = objects[piece.PieceIndex];
			if (piece.Removed)
			{
				rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 8000f + piece.PieceIndex);
				continue;
			}

			if (ReferenceEquals(held, rect))
			{
				if (piece.OwnerSteamId != 0 && piece.OwnerSteamId != session.LocalSteamId)
				{
					MinigameBase.main.ForceUngrab();
					traverse.Field("currentlyHeld").SetValue(null);
					held = null;
				}

				continue;
			}

			rect.anchoredPosition = new Vector2(piece.X, piece.Y);
		}
	}

	private static RemoteShrapnelUseSession? TryStartObserver(MedicalOperationStateMsg msg)
	{
		if (_observerShrapnel != null
			|| MinigameBase.main == null // Unity object — ==
			|| MinigameBase.main.currentMinigame != null)
		{
			return null;
		}

		var display = RemoteMedicalView.DisplayBody;
		if (display == null
			|| msg.LimbIndex < 0
			|| msg.LimbIndex >= display.limbs.Length
			|| display.limbs[msg.LimbIndex] == null) // Unity object — ==
		{
			return null;
		}

		var limb = display.limbs[msg.LimbIndex];
		if (limb.dismembered || !limb.hasShrapnel)
		{
			return null;
		}

		var minigame = new ShrapnelMinigame(limb, false);
		MinigameBase.main.StartMinigame(minigame, null);
		if (!ReferenceEquals(MinigameBase.main.currentMinigame, minigame))
		{
			return null;
		}

		var observer = new RemoteShrapnelUseSession(msg.TargetSteamId, 0, 0)
		{
			Minigame = minigame,
			OperationId = msg.OperationId,
		};
		_observerShrapnel = observer;
		RemoteShrapnelMinigamePatch.ResetLastHeld();
		return observer;
	}

	internal static void ReportShrapnelUpdate(ShrapnelPieceUpdate update)
	{
		if (_activeShrapnel is { OperationId: not 0 } session)
		{
			_shrapnelUpdateSender?.Invoke(session.OperationId, update);
		}
	}

	private static void ReportFinalHeldRemoval(RemoteShrapnelUseSession session)
	{
		var minigame = session.Minigame;
		if (minigame == null)
		{
			return;
		}

		var traverse = Traverse.Create(minigame);
		var objects = traverse.Field("objects").GetValue<List<RectTransform>>();
		var held = traverse.Field("currentlyHeld").GetValue<RectTransform>();
		if (objects is null || held == null)
		{
			return;
		}

		var index = objects.IndexOf(held);
		if (index < 0)
		{
			return;
		}

		var position = held.anchoredPosition;
		if (position.y < 35f)
		{
			return;
		}

		ReportShrapnelUpdate(new ShrapnelPieceUpdate
		{
			PieceIndex = index,
			X = position.x,
			Y = position.y,
			Grabbed = true,
			OwnershipChange = true,
		});
	}

	internal static bool IsActiveShrapnelMinigame(ShrapnelMinigame minigame) =>
		(_activeShrapnel?.Minigame is not null && ReferenceEquals(_activeShrapnel.Minigame, minigame))
		|| (_observerShrapnel?.Minigame is not null && ReferenceEquals(_observerShrapnel.Minigame, minigame));

	internal static bool IsObserverShrapnelMinigame(ShrapnelMinigame minigame) =>
		_observerShrapnel?.Minigame is not null && ReferenceEquals(_observerShrapnel.Minigame, minigame);

	internal static bool IsShrapnelPieceOwnedByOther(int pieceIndex)
	{
		var session = _activeShrapnel;
		if (session == null)
		{
			return false;
		}

		foreach (var piece in session.AuthoritativePieces)
		{
			if (piece.PieceIndex == pieceIndex)
			{
				return piece.OwnerSteamId != 0 && piece.OwnerSteamId != session.LocalSteamId;
			}
		}

		return false;
	}

	private bool StartRemoteShrapnel(Limb limb, bool tweezers, Item? item, ulong target, ulong itemInstanceId)
	{
		if (MinigameBase.main == null // Unity object — ==
			|| MinigameBase.main.currentMinigame != null)
		{
			_domains.Log.LogWarning("[MedicalView] refused shrapnel: no free native minigame host for {Target}.", target);
			return false;
		}

		var session = new RemoteShrapnelUseSession(target, itemInstanceId, _domains.Session.LocalSteamId)
		{
			Minigame = new ShrapnelMinigame(limb, tweezers),
		};

		MinigameBase.main.StartMinigame(session.Minigame, item);
		if (!ReferenceEquals(MinigameBase.main.currentMinigame, session.Minigame))
		{
			return false;
		}

		_activeShrapnel = session;
		RemoteShrapnelMinigamePatch.ResetLastHeld();
		_domains.PlayerInteraction.MedicalOperations.SendShrapnelStartRequest(target, itemInstanceId, Array.IndexOf(limb.body.limbs, limb));
		_domains.Log.LogInformation("[MedicalView] started remote shrapnel minigame for {Target} limb {Limb} (tweezers={Tweezers}).",
			target, Array.IndexOf(limb.body.limbs, limb), tweezers);
		return true;
	}

	private void OnStartAckReceived(MedicalOperationStartAckMsg msg)
	{
		var session = _activeShrapnel;
		if (session == null)
		{
			return;
		}

		if (!msg.Accepted)
		{
			_domains.Log.LogWarning("[MedicalView] remote shrapnel start rejected for {Target}: {Reason}.",
				msg.TargetSteamId, msg.RejectReason);
			RemoteShrapnelMinigamePatch.ResetLastHeld();
			_activeShrapnel = null;
			var minigame = session.Minigame;
			if (minigame != null
				&& MinigameBase.main != null // Unity object — ==
				&& ReferenceEquals(MinigameBase.main.currentMinigame, minigame))
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
		if (session.CancelledBeforeAck)
		{
			RemoteShrapnelMinigamePatch.ResetLastHeld();
			_activeShrapnel = null;
			_cancelRequestSender?.Invoke(msg.OperationId);
			return;
		}

		if (session.EngineEnded)
		{
			RemoteShrapnelMinigamePatch.ResetLastHeld();
			_activeShrapnel = null;
			_shrapnelEndRequestSender?.Invoke(msg.OperationId);
		}
	}

	private sealed class RemoteShrapnelUseSession(ulong target, ulong itemInstanceId, ulong localSteamId)
	{
		internal ulong Target { get; } = target;
		internal ulong ItemInstanceId { get; } = itemInstanceId;
		internal ulong LocalSteamId { get; } = localSteamId;
		internal ulong OperationId { get; set; }
		internal ShrapnelMinigame? Minigame { get; set; }
		internal List<ShrapnelPieceMsg> AuthoritativePieces { get; set; } = [];
		internal bool EngineEnded { get; set; }
		internal bool CancelledBeforeAck { get; set; }
	}
}
