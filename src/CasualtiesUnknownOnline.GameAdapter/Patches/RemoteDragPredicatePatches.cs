using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The predicate seams of the remote display-proxy release window. The native
/// release branch reads <c>PlayerCamera.body</c>, which is always the local body,
/// while the inventory ring and the dragged proxy belong to the displayed one:
/// these patches answer that branch's own guards from the body the ring is
/// showing, so the classification stays the game's own instead of becoming a CUO
/// routing table.
///
/// They answer ONLY inside an open release bracket on the owner's ring: while
/// they answer, every caller inside the bracket is the native release branch
/// itself (the bracket spans one <c>HandleReleaseDragging</c> invocation), and
/// CUO's own report hooks are guarded against display proxies so a redirected
/// read can never be reported as a local write.
/// </summary>
internal static class RemoteDragPredicatePatches
{
	/// <summary>
	/// Which body answers the native release branch's own guards while the release
	/// window is open.
	/// </summary>
	internal static class RemoteDragPredicateView
	{
		/// <summary>
		/// The displayed remote body whose slots the inventory ring is showing, or
		/// null when the bracket is closed, when the query is not about the local
		/// body, or when the ring is back on the local body (a display proxy whose
		/// view was closed while it was being dragged).
		///
		/// The native release branch reads <c>PlayerCamera.body</c>, which is always
		/// the local body, while both the ring and the dragged item belong to the
		/// displayed one: without this the branch's own guards evaluate against the
		/// wrong body, so the dragged proxy is never "held", a slot release never
		/// swaps, and the world fallbacks never fire — the gesture would classify as
		/// something the native code would never have done on a real inventory.
		/// </summary>
		internal static Body? AnsweringBody(Body instance)
		{
			var camera = PlayerCamera.main;
			if (camera == null // Unity object — ==
				|| !RemoteDragIntentWindow.Current.ShouldAnswerFromDisplayBody(instance == camera.body)) // Unity objects — ==
			{
				return null;
			}

			var answering = RemoteBackpackView.FocusedBody;
			if (answering == null || answering == instance) // Unity objects — ==
			{
				// The focus is fed exclusively from the remote render table
				// (RemoteBackpackCoordinator.Open → the renderer's remote body), so
				// this cannot be the local body today; the guard keeps a future
				// caller from making the postfix below call itself forever.
				return null;
			}

			return answering;
		}
	}

	/// <summary><c>Body.HoldingItem(Item)</c> answered by the body that actually displays the item (R8's guard, R9's first step, W2).</summary>
	[HarmonyPatch(typeof(Body), "HoldingItem", [typeof(Item)])]
	internal static class RemoteDragHoldingItemPatch
	{
		private static void Postfix(Body __instance, Item item, ref bool __result)
		{
			if (RemoteDragPredicateView.AnsweringBody(__instance) is { } answering)
			{
				__result = answering.HoldingItem(item);
			}
		}
	}

	/// <summary><c>Body.HoldingItem(int)</c> answered by the body the ring shows, so a slot index names the slot the player is pointing at.</summary>
	[HarmonyPatch(typeof(Body), "HoldingItem", [typeof(int)])]
	internal static class RemoteDragHoldingSlotPatch
	{
		private static void Postfix(Body __instance, int slot, ref bool __result)
		{
			if (RemoteDragPredicateView.AnsweringBody(__instance) is { } answering
				&& slot >= 0 && slot < answering.slots.Length)
			{
				__result = answering.HoldingItem(slot);
			}
		}
	}

	/// <summary><c>Body.GetItem(int)</c> answered by the body the ring shows (the slot release's occupying item, R8's slot argument).</summary>
	[HarmonyPatch(typeof(Body), "GetItem")]
	internal static class RemoteDragGetItemPatch
	{
		private static void Postfix(Body __instance, int slot, ref Item __result)
		{
			if (RemoteDragPredicateView.AnsweringBody(__instance) is { } answering
				&& slot >= 0 && slot < answering.slots.Length)
			{
				__result = answering.GetItem(slot);
			}
		}
	}

	/// <summary><c>Body.GetWearable(string)</c> answered by the body the ring shows (W3's worn-item guard).</summary>
	[HarmonyPatch(typeof(Body), "GetWearable")]
	internal static class RemoteDragGetWearablePatch
	{
		private static void Postfix(Body __instance, string itemid, ref Item __result)
		{
			if (RemoteDragPredicateView.AnsweringBody(__instance) is { } answering)
			{
				__result = answering.GetWearable(itemid);
			}
		}
	}

	/// <summary>
	/// <c>Body.DoPickupCheck</c> for the dragged display proxy. The check is a
	/// world-space distance/line-of-sight test between the local body and the item;
	/// a proxy stands where the remote player stands, so the native gate would drop
	/// the release before any branch ran. The body that displays the proxy is the
	/// one that can answer it.
	/// </summary>
	[HarmonyPatch(typeof(Body), "DoPickupCheck")]
	internal static class RemoteDragPickupCheckPatch
	{
		private static bool Prefix(Item item, ref bool __result)
		{
			var window = RemoteDragIntentWindow.Current;
			if (!window.IsOpen || !RemoteDragProxyQuery.IsProxy(item)
				|| RemoteDragProxyQuery.InstanceId(item) != window.DraggedItemId)
			{
				return true;
			}

			__result = true;
			return false;
		}
	}

	/// <summary>
	/// A remote container's window opened by the native release. The window is the
	/// game's own; CUO only tracks which authoritative container it shows so the
	/// projection can re-bind it after a rebuild.
	/// </summary>
	[HarmonyPatch(typeof(PlayerCamera), "OpenContainer")]
	internal static class RemoteDragOpenContainerPatch
	{
		private static void Postfix(Container cont)
		{
			if (RemoteDragProxyQuery.IsProxy(cont))
			{
				RemoteBackpackView.TrackOpenRemoteContainer(cont);
			}
		}
	}
}
