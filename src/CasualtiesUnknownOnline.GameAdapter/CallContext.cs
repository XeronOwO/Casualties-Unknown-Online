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
/// Swapping, Switching): nested scopes compose (a remote application may run an
/// internal reorder), Dispose restores the previous origin, and using compiles
/// to try/finally so exception paths release too. An unbalanced Enter (a scope
/// that outlives its mutation) is a programming error — fail loudly.
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

		/// <summary>Inside a crafting operation (Recipe.TryMake / Body.CombineItems) — the material/product item hooks stay silent (their facts ride the ONE craft report; the end-of-frame destroys ride the destroy-claim set in CraftingSync).</summary>
		Craft,

		/// <summary>The world-time domain is applying an authoritative speed (host policy or a host broadcast on the guest) — the SetTimeScale patch must let it through without re-reporting.</summary>
		WorldTimeApply,

		/// <summary>Inside PlayerCamera.HandleUnconsciousScreen — the vanilla per-side black-screen fast-forward is suppressed; the host's all-unconscious policy owns sleep acceleration.</summary>
		WorldTimeSleepLocal,

		/// <summary>Inside TutorialHandler.Update — Utils.Create calls in this scope are per-player tutorial-claw props (marked TutorialClawProp, kept out of the shared item/entity domains until a player picks the item up).</summary>
		TutorialClawSpawn,
	}

	/// <summary>Stack bound — real nesting is 2-3 levels (remote apply → container load → hooks).</summary>
	private const int MaxDepth = 16;

	private static readonly Origin[] Stack = new Origin[MaxDepth];
	private static int _depth;

	/// <summary>The innermost active origin — LocalAction when no scope is open (plain game-code calls, the default).</summary>
	internal static Origin Current => _depth > 0 ? Stack[_depth - 1] : Origin.LocalAction;

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
