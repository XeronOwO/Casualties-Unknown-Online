using System;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Remote-medical WoundView gesture routing. It maps a local dragged medical
/// item released on a remote body-limb diagram to the existing
/// host-authoritative heal/use request path and keeps the display-only remote
/// body copy untouched. Injectable/IV medicines are routed through the native
/// syringe minigame first: the acting player physically pumps the amount, and
/// only the ml actually delivered is sent to the host. The item must be locally
/// owned with an authoritative instance id; the native view supplies the
/// selected limb index.
/// </summary>
internal sealed class RemoteMedicalOperationHandler(GameAdapterDomains domains)
{
	private static RemoteSyringeUseSession? _activeSyringe;

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
			domains.Log.LogWarning("[MedicalView] refused limb use: dragged item {ItemId} is a remote display proxy, not a local medical item.",
				dragItem.id);
			return false;
		}

		var target = RemoteMedicalView.TargetSteamId;
		if (target == 0)
		{
			domains.Log.LogWarning("[MedicalView] refused limb use: remote medical focus has no target.");
			return false;
		}

		var instance = dragItem.GetComponent<ItemInstanceId>();
		if (instance == null || instance.Id == 0) // Unity object — ==
		{
			domains.Log.LogWarning("[MedicalView] refused limb use: local item {ItemId} has no authoritative instance id.",
				dragItem.id);
			return false;
		}

		if (!LocalUseItemEligibility.IsMedicalLimbUseItem(dragItem))
		{
			domains.Log.LogWarning("[MedicalView] refused limb use: {ItemId} is not a supported remote medical/limb-treatment item.",
				dragItem.id);
			return false;
		}

		if (RemoteHealProfiles.IsHealItem(dragItem.id))
		{
			domains.PlayerInteraction.SendHealRequest(target, instance.Id, limbIndex);
			domains.Log.LogInformation("[MedicalView] requested heal of {Target} limb {Limb} with {ItemId} (id {InstanceId}).",
				target, limbIndex, dragItem.id, instance.Id);
			return true;
		}

		if (RemoteMedicineCatalog.IsInjectableItem(dragItem.id))
		{
			return TryStartRemoteSyringeUse(dragItem, limbIndex, target, instance.Id);
		}

		domains.PlayerInteraction.SendUseRequest(target, instance.Id, limbIndex);
		domains.Log.LogInformation("[MedicalView] requested use of {Target} limb {Limb} with {ItemId} (id {InstanceId}).",
			target, limbIndex, dragItem.id, instance.Id);
		return true;
	}

	/// <summary>
	/// Called when the native minigame system ends the active remote syringe
	/// minigame. The painkiller/injectable amount physically delivered during
	/// the minigame is what gets sent to the host; an empty minigame sends
	/// nothing. The display body's temporary condition change is always
	/// restored first; the host result is the only authority that persists the
	/// item drain.
	/// </summary>
	internal static void CompleteActiveSyringeUse()
	{
		var session = _activeSyringe;
		if (session == null)
		{
			return;
		}

		_activeSyringe = null;
		session.RestoreCondition();

		// A remote view that closed without going through the normal cancel
		// path must never send a stale cross-player request.
		if (!RemoteMedicalView.IsOpen)
		{
			return;
		}

		if (session.InjectedMl > 0.01f)
		{
			session.Complete?.Invoke(session.InjectedMl);
		}
	}

	/// <summary>
	/// Cancel an in-flight remote syringe session without sending a request.
	/// Used when the remote medical focus closes before the minigame ends;
	/// returns true when a session was active so the caller can also end the
	/// native minigame.
	/// </summary>
	internal static bool CancelActiveSyringeUse()
	{
		var session = _activeSyringe;
		if (session == null)
		{
			return false;
		}

		var minigame = session.Minigame;
		_activeSyringe = null;
		session.RestoreCondition();

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
			domains.Log.LogWarning("[MedicalView] refused syringe use: no valid non-dismembered display limb {Limb}.", limbIndex);
			return false;
		}

		if (MinigameBase.main == null // Unity object — ==
			|| MinigameBase.main.currentMinigame != null)
		{
			domains.Log.LogWarning("[MedicalView] refused syringe use: no free native minigame host for {Target}.", target);
			return false;
		}

		if (!RemoteMedicineCatalog.TryGetInjectionAmount(dragItem.id, out var fullDose) || fullDose <= 0f)
		{
			domains.Log.LogWarning("[MedicalView] refused syringe use: {ItemId} has no known injection amount.", dragItem.id);
			return false;
		}

		var water = dragItem.GetComponent<WaterContainerItem>();
		if (water == null) // Unity object — ==
		{
			domains.Log.LogWarning("[MedicalView] refused syringe use: {ItemId} has no WaterContainerItem.", dragItem.id);
			return false;
		}

		var session = new RemoteSyringeUseSession(dragItem, water.Capacity, fullDose)
		{
			Complete = injected =>
			{
				domains.PlayerInteraction.SendUseRequest(target, itemInstanceId, limbIndex, injected);
				domains.Log.LogInformation(
					"[MedicalView] requested remote syringe use of {Target} limb {Limb} with {ItemId} (id {InstanceId}), dose {Dose:F2} ml.",
					target, limbIndex, dragItem.id, itemInstanceId, injected);
			},
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
		domains.Log.LogInformation("[MedicalView] started remote syringe minigame for {Target} limb {Limb} with {ItemId} (id {InstanceId}).",
			target, limbIndex, dragItem.id, itemInstanceId);
		return true;
	}

	private sealed class RemoteSyringeUseSession
	{
		private readonly Item _item;
		private readonly float _originalCondition;
		private readonly float _capacity;

		internal RemoteSyringeUseSession(Item item, float capacity, float fullDose)
		{
			_item = item;
			_originalCondition = item.condition;
			_capacity = capacity > 0f ? capacity : fullDose;
		}

		internal float InjectedMl { get; private set; }

		internal Action<float>? Complete { get; set; }

		internal SyringeMinigame? Minigame { get; set; }

		internal void Accumulate(float ml)
		{
			InjectedMl += ml;
			// Keep the native minigame's syringe fill moving in sync with the
			// ml delivered. The real liquid stacks are NOT changed here; the
			// host result is the only authority that drains them.
			_item.condition = Mathf.Max(0f, _originalCondition - InjectedMl / _capacity);
		}

		internal void RestoreCondition() => _item.condition = _originalCondition;
	}
}
