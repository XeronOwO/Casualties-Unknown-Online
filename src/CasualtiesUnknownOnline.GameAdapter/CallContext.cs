using System;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Process-wide call identity: every game-object mutation CUO syncs runs inside
/// a scope that declares WHO is mutating — a local player action, a remote
/// message being applied, or an inventory-internal reorder. Guards read
/// <see cref="Current"/> instead of inferring identity from parameter values —
/// "you cannot tell who you are from the parameters" was the root of the
/// quake-break subset bug (the numbering gate swallowed remote breaks) and the
/// slot-drag pickup bug (the scene looked like a world pickup when a same-frame
/// drop had already un-slotted the item). A static class is correct here: call
/// identity is process context, not state owned by any one domain, and the
/// Harmony patches (static classes, no DI) are writers of InternalReorder
/// scopes — HarmonyTraverse is the same precedent. The scope STACK replaces the
/// boolean reentry guards (IsApplyingRemote, IsApplyingRemoteBlockPlace,
/// Swapping, Switching): nested scopes compose — <see cref="Current"/> answers
/// the INNERMOST origin (the scope CLASSIFICATION: which sub-scope is running),
/// while <see cref="IsWithin"/> answers whether an origin is anywhere in the
/// chain (MUTATION ATTRIBUTION: a remote application that runs an internal
/// reorder, a craft or the game's own damage roll has to stay "within
/// <see cref="Origin.RemoteApply"/> for every guard that keeps a replay from
/// being reported as the local player's action) — Dispose restores the previous
/// origin, and using compiles to try/finally so exception paths release too. An
/// unbalanced Enter (a scope that outlives its mutation) is a programming
/// error — fail loudly.
/// </summary>
internal static class CallContext
{
	/// <summary>Who is mutating the scene right now.</summary>
	internal enum Origin
	{
		/// <summary>Plain game-code calls — the default when no scope is open.</summary>
		LocalAction,

		/// <summary>A remote message is being applied.</summary>
		RemoteApply,

		/// <summary>An inventory-internal reorder (no world-meaningful move).</summary>
		InternalReorder,

		/// <summary>Inside a local DamageBlock roll — Utils.Create calls in this scope are block drops (marked with DropOrigin, folded into the pending break report).</summary>
		DamageBlockOrigin,

		/// <summary>Inside BuildingEntity.Update's local death branch (health &lt; 0.5 without RemoteEntityDeath) — Item.Awake sees this scope and marks each spawned item as a building-death drop.</summary>
		BuildingDeathDrop,

		/// <summary>Inside a crafting operation (Recipe.TryMake / Body.CombineItems) — the material/product item hooks stay silent (their facts ride the ONE craft report; the end-of-frame destroys ride the destroy-claim set in CraftingSync).</summary>
		Craft,

		/// <summary>The world-time domain is applying an authoritative speed (host policy or a host broadcast on the guest) — the SetTimeScale patch must let it through without re-reporting.</summary>
		WorldTimeApply,

		/// <summary>Inside PlayerCamera.HandleUnconsciousScreen — the vanilla per-side black-screen fast-forward is suppressed; the host's all-unconscious policy owns sleep acceleration.</summary>
		WorldTimeSleepLocal,

		/// <summary>Inside TutorialHandler.Update — Utils.Create calls in this scope are per-player tutorial-claw props (marked TutorialClawProp, kept out of the shared item/entity domains until a player picks the item up).</summary>
		TutorialClawSpawn,

		/// <summary>Inside Body.Attack — string Sound.Play calls in this scope are the local attack swing/exert sounds (block hit sounds run in the innermost DamageBlockOrigin scope and are excluded).</summary>
		CharacterAttack,

		/// <summary>Inside Body.ThrowItem — the local throw swing sound reports from this scope.</summary>
		CharacterThrow,

		/// <summary>Inside a local direct placeable-item use (Body.UseItem /
		/// Body.UseItemInHand for scrapmetal / climbingrope / scaffoldingpack) —
		/// the string placement sound (<c>"scrapmetal"</c> / <c>"ropeplace"</c>)
		/// reports from this scope.</summary>
		CharacterItemPlacement,

		/// <summary>Inside Body.TryExertSound — the local exertion sound reports from this scope.</summary>
		CharacterExert,

		/// <summary>Inside Body.FootStep — the local step sound (fallback string or material/water AudioClip) reports from this scope.</summary>
		CharacterFootstep,

		/// <summary>Inside Body.HandleGroundedState — the local landing impact AudioClip reports from this scope (the nested FootStep call reports as CharacterFootstep).</summary>
		CharacterLandingImpact,

		/// <summary>Inside PantSound.Update — the local one-shot pain (AudioClip) and yawn (string) vocalizations report from this scope. The continuous pant AudioSource is not a Sound.Play call and is never captured.</summary>
		CharacterVocalization,

		/// <summary>Inside LockpingMinigame.Update — the local lockpick-failure pain sound (<c>"gore2"</c>) reports from this scope. The success <c>"unlock"</c> sound is not classified by this scope.</summary>
		CharacterLockpickPain,

		/// <summary>Inside PantSound.Bark — the local B-key bark AudioClip reports from this scope.</summary>
		CharacterBark,

		/// <summary>Inside PantSound.TryGrowl — the local low-happiness growl string reports from this scope.</summary>
		CharacterGrowl,

		/// <summary>Inside a local body's item-use action (<c>Body.UseItem</c> /
		/// <c>Body.UseItemInHand</c>, every usable item). The edible use actions
		/// play their ingest clips (<c>"eatCrunch"</c> / <c>"eatFlesh"</c> /
		/// <c>"glass"</c> / <c>"crystalenemylaugh"</c>) inside this scope, and the
		/// policy classifies exactly those; the direct placeable family keeps its
		/// own innermost <see cref="CharacterItemPlacement"/> scope.</summary>
		CharacterItemUse,

		/// <summary>Inside <c>Body.HandleVisuals</c> for a local body — the meal-end
		/// <c>"burp"</c> (Body.cs:3137-3142) reports from this scope. The scope
		/// exists for that one clip, the way <see cref="CharacterLockpickPain"/>
		/// exists for <c>"gore2"</c>.</summary>
		CharacterBurp,

		/// <summary>Inside <c>PlayerCamera.ApplyWoundItem</c> on THIS client's own
		/// camera — the choke point every local limb action enters (the item's own
		/// <c>useLimbAction</c> delegate at PlayerCamera.cs:754 and the container's
		/// <c>WaterContainerItem.ApplyToLimb</c> at :760), where the medical clips
		/// play at the treated limb's position — which may be another player's
		/// body. Opened only for a plain local action on the local camera: the
		/// remote medical view blocks the native call and a drag-release window
		/// captures the intent instead of applying it, so a remote-driven
		/// treatment never reports as this player's action.</summary>
		CharacterMedicalUse,

		/// <summary>Inside <c>FluidManager.DrinkLiquid</c> for the local body — the
		/// world-liquid drink (<c>"drink"</c> at FluidManager.cs:314 and the
		/// liquid registry's own <c>onDrink</c> clip at Liquids.cs:1501) reports
		/// from this scope. The container-drink half of "drinking" rides
		/// <see cref="CharacterItemUse"/> instead.</summary>
		CharacterWorldDrink,

		/// <summary>Inside <c>Body.CombineLiquids</c> — the transfer UI's finish
		/// (LiquidTransfer.cs:38) plays <c>"waterpour"</c> there and opens no
		/// scope of its own. The other two inventory gestures need none: SwitchHands
		/// / SwapSlots already run inside <see cref="InternalReorder"/> and
		/// CombineItems inside <see cref="Craft"/>, whose origins the capture map
		/// classifies.</summary>
		CharacterInventoryGesture,

		/// <summary>Inside a local body's own one-shot coroutine — the Vomiter
		/// vomit routines (<c>"vomit1"</c> / <c>"vomit2"</c>), the nap
		/// <c>"stretch"</c> and the water <c>"dogshake"</c>. Entered per coroutine
		/// STEP: a coroutine body runs in its state machine's MoveNext after the
		/// patched method already returned, so a scope around the method would be
		/// disposed before the first body statement.</summary>
		CharacterBodySound,
	}

	/// <summary>Stack bound — real nesting is 2-3 levels (remote apply → container load → hooks).</summary>
	private const int MaxDepth = 16;

	private static readonly Origin[] Stack = new Origin[MaxDepth];
	private static int _depth;

	/// <summary>The innermost active origin — the scope CLASSIFICATION query; LocalAction when no scope is open (plain game-code calls, the default). Use <see cref="IsWithin"/> for mutation attribution.</summary>
	internal static Origin Current => _depth > 0 ? Stack[_depth - 1] : Origin.LocalAction;

	/// <summary>
	/// True when <paramref name="origin"/> is anywhere in the active scope chain —
	/// the MUTATION-ATTRIBUTION query. A guard that keeps a replayed mutation from
	/// being reported as the local player's action must ask this one: a nested
	/// sub-scope (the game's damage roll pushes <see cref="Origin.DamageBlockOrigin"/>,
	/// an inventory load <see cref="Origin.InternalReorder"/>, a craft
	/// <see cref="Origin.Craft"/>) sits ON TOP of the caller's
	/// <see cref="Origin.RemoteApply"/> and <see cref="Current"/> no longer names
	/// it — the leak that reported a receiving side's presentation write back to
	/// the host (ticket <c>unhooked-damage-block-callers</c>, row 4).
	/// <see cref="Origin.LocalAction"/> is the implicit bottom of the stack: it
	/// answers true only while no scope is open.
	/// </summary>
	internal static bool IsWithin(Origin origin)
	{
		if (origin == Origin.LocalAction)
		{
			return _depth == 0;
		}

		for (var i = 0; i < _depth; i++)
		{
			if (Stack[i] == origin)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>Opens a scope; Dispose restores the previous origin. using-scoped — try/finally guarantees release on exception paths.</summary>
	internal static IDisposable Enter(Origin origin)
	{
		if (_depth >= MaxDepth)
		{
			throw new InvalidOperationException($"CallContext.Enter depth {MaxDepth} exceeded — an Enter was never disposed (leaked scope).");
		}

		Stack[_depth++] = origin;
		return new Scope();
	}

	/// <summary>Nested disposable returned by Enter — restores the stack depth on Dispose.</summary>
	private sealed class Scope : IDisposable
	{
		private bool _disposed;

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			if (_depth > 0)
			{
				_depth--;
			}
		}
	}
}
