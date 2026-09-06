using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Character;
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
		var session = _activeShrapnel;
		if (session == null)
		{
			return;
		}

		if (session.OperationId != 0)
		{
			_activeShrapnel = null;
			_shrapnelEndRequestSender?.Invoke(session.OperationId);
			return;
		}

		session.EngineEnded = true;
		if (!RemoteMedicalView.IsOpen)
		{
			session.CancelledBeforeAck = true;
		}
	}

	internal static bool CancelActiveShrapnelUse()
	{
		var session = _activeShrapnel;
		if (session == null)
		{
			return false;
		}

		var minigame = session.Minigame;
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
		var session = _activeShrapnel;
		if (session == null)
		{
			return;
		}

		if (session.OperationId != 0 && session.OperationId != operationId)
		{
			return;
		}

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
		var session = _activeShrapnel;
		if (session == null || (session.OperationId != 0 && session.OperationId != msg.OperationId))
		{
			return;
		}

		session.AuthoritativePieces = [.. msg.ShrapnelPieces];
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

	internal static void ReportShrapnelUpdate(ShrapnelPieceUpdate update)
	{
		if (_activeShrapnel is { OperationId: not 0 } session)
		{
			_shrapnelUpdateSender?.Invoke(session.OperationId, update);
		}
	}

	internal static bool IsActiveShrapnelMinigame(ShrapnelMinigame minigame) =>
		_activeShrapnel?.Minigame is not null && ReferenceEquals(_activeShrapnel.Minigame, minigame);

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
			_activeShrapnel = null;
			_cancelRequestSender?.Invoke(msg.OperationId);
			return;
		}

		if (session.EngineEnded)
		{
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
