using System;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Patches;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Remote-medical Stage 3 adapter: starts the native Bandage/Dislocation/AED/ManualDefib/Amputation minigames on the remote display
/// body, routes semantic updates through the generic medical operation session, and handles the one-shot splint/tourniquet removal
/// sessions. One session at a time — the native engine runs one minigame per client. The session's bookkeeping (which one is
/// active, the updates it owes, the ack and the terminal paths) lives in <see cref="RemoteOtherUseSessions"/>.
/// </summary>
internal sealed class RemoteOtherMedicalOperationHandler
{
	private readonly GameAdapterDomains _domains;

	internal RemoteOtherMedicalOperationHandler(GameAdapterDomains domains)
	{
		_domains = domains;
		RemoteOtherUseSessions.Initialize(domains.PlayerInteraction.MedicalOperations, domains.Log);
		domains.PlayerInteraction.MedicalOperations.StartAckReceived += RemoteOtherUseSessions.OnStartAckReceived;
	}

	internal bool TryStartRemoteBandageUse(Item item, int limbIndex, ulong target, ulong itemInstanceId)
	{
		var display = RemoteMedicalView.DisplayBody;
		if (RemoteOtherUseSessions.IsActive
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
				_ => RemoteOtherUseSessions.ReportBandageWrap(),
				Color.white,
				display.limbs[limbIndex]),
		};

		return StartMinigameSession(session, item);
	}

	internal bool TryStartRemoteDislocationUse(Limb limb, bool wrench, Item? item, ulong target, ulong itemInstanceId)
	{
		var display = RemoteMedicalView.DisplayBody;
		if (RemoteOtherUseSessions.IsActive
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
		if (RemoteOtherUseSessions.IsActive
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
		if (RemoteOtherUseSessions.IsActive
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
		if (RemoteOtherUseSessions.IsActive
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
		if (RemoteOtherUseSessions.IsActive
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

		RemoteOtherUseSessions.Arm(session);
		_domains.PlayerInteraction.MedicalOperations.SendOtherStartRequest(
			session.Target, 0, session.LimbIndex, session.Kind);
		_domains.Log.LogInformation("[MedicalView] started remote {Kind} removal for {Target} limb {Limb}.",
			kind, session.Target, session.LimbIndex);
		return true;
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

		RemoteOtherUseSessions.Arm(session);
		RemoteOtherMedicalMinigamePatch.Reset();
		_domains.PlayerInteraction.MedicalOperations.SendOtherStartRequest(
			session.Target, session.ItemInstanceId, session.LimbIndex, session.Kind);
		_domains.Log.LogInformation("[MedicalView] started remote {Kind} minigame for {Target} limb {Limb} (item {ItemId}).",
			session.Kind, session.Target, session.LimbIndex, item?.id ?? "-");
		return true;
	}
}
