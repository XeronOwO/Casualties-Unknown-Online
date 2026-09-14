using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The once-per-body rule of the starting-supplies grant: which local bodies have
/// already been judged, so the pump's every-frame call is a once-in-a-lifetime
/// judgement instead.
///
/// The rule is per BODY, not per session, because a body is the thing a restore, the
/// grant and the game's own first-layer handout all act on. A layer descent keeps the
/// SAME body (<c>WorldGeneration.RegenerateWorld</c> calls <c>Clear()</c> +
/// <c>InstantiateWorld(true)</c> — the scene is never reloaded, so the local body
/// survives), which is exactly the semantics the stage's decision names: a player who
/// descends a level still has their character, so they are not a new player and are not
/// supplied again. A death or a reconnect destroys the body, and the next one is judged
/// on its own entry — which is where a restored player and an absent one are told apart.
///
/// It deliberately holds NOTHING ELSE. The "does this world have a character for me"
/// question is asked at the moment of the grant, not sampled when the body appeared
/// (<see cref="StartingSupplyCoordinator.Update"/>): a restore can arrive in the frames
/// between the two, and that restore is precisely the event that must cancel the grant.
/// A cached entry sample would answer with a falsified past.
///
/// Bodies are tracked by REFERENCE identity, never by <c>Equals</c>: Unity objects
/// overload equality to mean "destroyed", so an equality-keyed set would drop a live
/// body out of the "already supplied" set the moment its backing object changed state.
/// </summary>
internal sealed class StartingSupplyGrantTracker
{
	/// <summary>
	/// The bodies already judged, keyed by identity. A collection expression with one
	/// element is how this codebase writes a pre-sized/parameterized collection
	/// (IDE0028): <c>[with(ReferenceEqualityComparer.Instance)]</c> constructs the set
	/// with the comparer instead of allocating it empty and discarding it.
	/// </summary>
	private readonly HashSet<object> _supplied = [with(ReferenceEqualityComparer.Instance)];
	/// <summary>How many bodies this session has supplied (diagnostics and tests).</summary>
	internal int SuppliedCount => _supplied.Count;

	/// <summary>
	/// True = this body has already been judged. The pump runs every frame and the same
	/// body is the local body for as long as the world keeps it, so this is the check
	/// that makes the rule once-per-body rather than once-per-frame.
	/// </summary>
	internal bool WasSupplied(object body) => _supplied.Contains(body);

	/// <summary>This body has been judged and must never be judged again.</summary>
	internal void MarkSupplied(object body) => _supplied.Add(body);

	/// <summary>
	/// An independent run is taking over (the host clicked start, the session ended): the
	/// bodies tracked here belong to a world this client is leaving, and the next entry is
	/// a new-player entry by definition. Called from the same place every other run-scoped
	/// reset happens, so the rule cannot rot into "supplied once per process".
	/// </summary>
	internal void Clear() => _supplied.Clear();

	/// <summary>
	/// The set that keys these tables: Unity objects must never be compared with
	/// <c>Equals</c> here (a destroyed object can compare equal to null and to another
	/// object), so identity is the only sound key.
	/// </summary>
	private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
	{
		internal static readonly ReferenceEqualityComparer Instance = new();

		public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

		public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
	}
}
