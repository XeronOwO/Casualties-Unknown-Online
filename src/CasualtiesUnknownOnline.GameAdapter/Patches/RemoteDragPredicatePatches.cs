using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The query seams of the remote display-proxy release window. The native
/// release branch reads <c>PlayerCamera.body</c>, which is always the local body,
/// while the inventory ring and the dragged proxy belong to the displayed one:
/// these patches answer that branch's own guards from the body the ring is
/// showing, so the classification stays the game's own instead of becoming a CUO
/// routing table.
///
/// Every one of them answers by SKIPPING the native body, and that is the rule
/// rather than a style choice: the game's own query bodies read the LOCAL body's
/// state behind the very guard the redirect answered. <c>Body.GetItem(int)</c> is
/// the shape that proved it — it guards on <c>HoldingItem(slot)</c> and then
/// indexes <c>this.slots[slot].transform.GetChild(0)</c> (<c>Body.cs:1346-1353</c>),
/// so answering only the guard made it throw `Transform child out of bounds` on a
/// local slot the clone has occupied, and the whole release died with it: no
/// intent, no refusal line, and <c>HandleReleaseDragging</c> never reached its own
/// <c>dragItem = null</c> (batch `20261006-f`, matrix row 6's swap half). A prefix
/// that returns false cannot leave a native body reading state the redirect did
/// not answer, so the invariant holds for every query this file answers and is
/// pinned by <c>RemoteDragQuerySeamGateTests</c>.
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
				// caller from making the prefix below call itself forever.
				return null;
			}

			return answering;
		}

		/// <summary>
		/// Whether a slot index names a slot of the body the ring shows. Both
		/// bodies are instantiated from the same "Experiment" template
		/// (<c>RemoteBodyFactory.CreateRemoteBody</c>) and carry its serialized slot
		/// array, so the ring's own button index is valid on either; an index the
		/// displayed body does not have is not a read about the ring at all, and its
		/// caller keeps the native answer instead.
		/// </summary>
		internal static bool NamesASlotOfTheRing(Body answering, int slot) =>
			slot >= 0 && slot < answering.slots.Length;
	}

	/// <summary><c>Body.HoldingItem(Item)</c> answered by the body that actually displays the item (R8's guard, R9's first step, W2).</summary>
	[HarmonyPatch(typeof(Body), "HoldingItem", [typeof(Item)])]
	internal static class RemoteDragHoldingItemPatch
	{
		private static bool Prefix(Body __instance, Item item, ref bool __result)
		{
			if (RemoteDragPredicateView.AnsweringBody(__instance) is not { } answering)
			{
				return true;
			}

			__result = answering.HoldingItem(item);
			return false;
		}
	}

	/// <summary><c>Body.HoldingItem(int)</c> answered by the body the ring shows, so a slot index names the slot the player is pointing at.</summary>
	[HarmonyPatch(typeof(Body), "HoldingItem", [typeof(int)])]
	internal static class RemoteDragHoldingSlotPatch
	{
		private static bool Prefix(Body __instance, int slot, ref bool __result)
		{
			if (RemoteDragPredicateView.AnsweringBody(__instance) is not { } answering
				|| !RemoteDragPredicateView.NamesASlotOfTheRing(answering, slot))
			{
				return true;
			}

			__result = answering.HoldingItem(slot);
			return false;
		}
	}

	/// <summary>
	/// <c>Body.GetItem(int)</c> answered by the body the ring shows (the slot
	/// release's occupying item, R8's slot argument). The answer must SKIP the
	/// native body: that body guards on <c>HoldingItem(slot)</c> — which this file
	/// answers from the clone — and then indexes the LOCAL body's slot transform,
	/// which is the `Transform child out of bounds` of batch `20261006-f`. Called
	/// from inside the bracket (R8's own `SwapSlots` argument walk in
	/// <c>RemoteDragMutationPatches</c>), this is the read that makes the whole
	/// branch answer about the owner's body.
	/// </summary>
	[HarmonyPatch(typeof(Body), "GetItem")]
	internal static class RemoteDragGetItemPatch
	{
		private static bool Prefix(Body __instance, int slot, ref Item __result)
		{
			if (RemoteDragPredicateView.AnsweringBody(__instance) is not { } answering
				|| !RemoteDragPredicateView.NamesASlotOfTheRing(answering, slot))
			{
				return true;
			}

			__result = answering.GetItem(slot);
			return false;
		}
	}

	/// <summary>
	/// <c>Body.GetWearable(string)</c> answered by the body the ring shows (W3's
	/// worn-item guard). The native body walks the LOCAL body's limb transforms,
	/// whose items are the local player's — a query about the displayed body's worn
	/// item must not be answered from them.
	/// </summary>
	[HarmonyPatch(typeof(Body), "GetWearable")]
	internal static class RemoteDragGetWearablePatch
	{
		private static bool Prefix(Body __instance, string itemid, ref Item __result)
		{
			if (RemoteDragPredicateView.AnsweringBody(__instance) is not { } answering)
			{
				return true;
			}

			__result = answering.GetWearable(itemid);
			return false;
		}
	}

	/// <summary>
	/// <c>Body.DoPickupCheck</c> for the dragged display proxy. The check is a
	/// world-space distance/line-of-sight test between the local body and the item;
	/// a proxy stands where the remote player stands, so the native gate would drop
	/// the release before any branch ran — and in a while-dragging frame it would
	/// drop that frame's drain tick the same way. The body that displays the proxy is
	/// the one that can answer it, and
	/// <see cref="RemoteDragIntentCapture.AnswersPickupCheckFor"/> is the one place
	/// that decides whether this call is that case (both bracket kinds answer; an
	/// item that is not the dragged proxy, or that carries no authoritative id,
	/// keeps the native answer).
	/// </summary>
	[HarmonyPatch(typeof(Body), "DoPickupCheck")]
	internal static class RemoteDragPickupCheckPatch
	{
		private static bool Prefix(Item item, ref bool __result)
		{
			if (!RemoteDragProxyQuery.IsProxy(item)
				|| !RemoteDragIntentWindow.Current.AnswersPickupCheckFor(RemoteDragProxyQuery.InstanceId(item)))
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
	/// projection can re-bind it after a rebuild — so this one patches an ACTION
	/// whose native body must run, not a query, and it keeps its postfix.
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
