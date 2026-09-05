namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The remote-backpack half of the Harmony patch bridge. Kept as its own
/// interface so <see cref="IPatchBridge"/> stays under the 600-line
/// architecture gate while the growing native remote-backpack gesture set has
/// one discoverable, focused surface.
/// </summary>
internal interface IRemoteBackpackPatchBridge
{
	/// <summary>
	/// Remote-backpack drag release: while the native remote backpack view is
	/// open, the dragged item is a display proxy under the focused remote
	/// clone. Sends the existing host-authoritative take request for that
	/// instance id and returns true so the native non-mutating path is skipped.
	/// Returns false when the drag is not a remote clone item or the view is
	/// closed.
	/// </summary>
	bool TryHandleRemoteBackpackTake(Item dragItem);

	/// <summary>Remote-backpack edge drop: sends a host-authoritative drop request for the dragged remote display proxy's authoritative item.</summary>
	bool TryHandleRemoteBackpackDrop(Item dragItem);

	/// <summary>Remote-backpack container move: sends a host-authoritative move-to-container request for the dragged remote display proxy into the focused remote player's container.</summary>
	bool TryHandleRemoteBackpackMoveToContainer(Item dragItem, Item targetContainer);

	/// <summary>Remote-backpack pour/dump: sends a host-authoritative request to empty the dragged remote water container's liquid stacks.</summary>
	bool TryHandleRemoteBackpackPour(Item dragItem);

	/// <summary>Remote-backpack combine: sends a host-authoritative native combine request for two remote display-proxy items owned by the same focused player.</summary>
	bool TryHandleRemoteBackpackCombine(Item dragItem, Item target);

	/// <summary>Remote-backpack use: sends a host-authoritative native use request for a dragged remote display-proxy item on its owner.</summary>
	bool TryHandleRemoteBackpackUse(Item dragItem);

	/// <summary>Remote-backpack wear: sends a host-authoritative native wear request for a dragged remote display-proxy wearable on its owner.</summary>
	bool TryHandleRemoteBackpackWear(Item dragItem);

	/// <summary>Remote-backpack battery load: sends a host-authoritative native battery-load request for a remote battery into a remote battery-powered item.</summary>
	bool TryHandleRemoteBackpackBatteryLoad(Item dragItem, Item target);

	/// <summary>Remote-backpack battery unload: sends a host-authoritative native battery-unload request for a remote battery-powered item.</summary>
	bool TryHandleRemoteBackpackBatteryUnload(Item dragItem, Item target);

	/// <summary>Remote-backpack favourite toggle: sends a host-authoritative favourite-flag toggle for one remote display-proxy item.</summary>
	bool TryHandleRemoteBackpackFavoriteToggle(Item item);

	/// <summary>Remote-backpack slot move/swap: sends a host-authoritative native move-to-slot request for a remote display-proxy item.</summary>
	bool TryHandleRemoteBackpackMoveToSlot(Item dragItem, int targetSlot);

	/// <summary>Tab-switch transfer: sends the existing host-authoritative take request for a remote display proxy released into the local inventory after the remote view was closed.</summary>
	bool TryHandleRemoteProxyTransferToLocal(Item dragItem);

	/// <summary>
	/// Cancels an active drag whose item is a remote-clone display proxy. The
	/// proxy must never escape into the native local-body/cross-player release
	/// flows; the bridge logs the cancellation so the rare UI path is
	/// observable. Returns true when it cancelled a proxy drag.
	/// </summary>
	bool CancelRemoteProxyDrag(PlayerCamera camera, string reason);
}
