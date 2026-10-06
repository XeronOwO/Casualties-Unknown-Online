namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The materialization adopt scan's tie-break, as a pure rule: whether a scene object may BE an authority
/// row's copy instead of that row materializing one beside it.
///
/// <para>
/// Why it is a rule of its own: the scan's guards used to read the scene directly, and one of them —
/// <c>GetComponentInParent&lt;RemoteCloneRender&gt;()</c> — silently stopped answering for exactly the object
/// the scan must never claim. <c>CloneInventoryRenderer.RestoreRemoteContents</c> retires a stale clone
/// display proxy by unloading it (<c>Container.UnloadItem</c>: <c>transform.SetParent(null)</c>, which makes
/// <see cref="ItemWorldSync.IsWorldItem"/> true, plus <c>Vector3.up * 1.5f</c> — the renderer's retire step
/// moves it exactly onto the scan's own <see cref="RemoteItemSceneOps.AdoptTolerance"/>), then deactivates it
/// and queues its <c>Object.Destroy</c> for the end of the frame. An inactive object is invisible to the
/// default <c>includeInactive: false</c> component lookups, so the proxy passed as a generation-time world
/// item: batch `20261006-h` stamped the second child's id onto it 10-30 ms before Unity destroyed it, and that
/// destroy then reported the id — the adapter's own remote kill zeroes ids first, which is why this one did
/// not look like one — leaving the item <c>terminal</c> in the kernel and no standing object anywhere.
/// </para>
///
/// <para>
/// Two clauses are that fix: <c>displayProxy</c> (a clone display proxy is presentation only, whoever owns its
/// lifetime) and <c>retired</c> (an object the scene has already taken out of play must never be handed an id
/// that outlives it). The other five state the scan's existing intent, so the whole tie-break reads and tests
/// in one place. The truth table is pinned by <c>AdoptTargetRuleTests</c>; the wiring that supplies the facts
/// from the scene is pinned by <c>AdoptTargetGateTests</c>.
/// </para>
/// </summary>
internal static class AdoptTargetRule
{
	/// <summary>
	/// Whether the candidate may become the row's copy. Every argument is the FACT read from the candidate, in
	/// the POSITIVE — the caller passes what it observed, and the negation lives in the body below and nowhere
	/// else (a reader who takes an argument name for its verdict would invert four of the seven).
	/// <paramref name="sameDefinition"/>: the candidate is the row's definition.
	/// <paramref name="alreadySynced"/>: it already carries a domain id, which belongs to another row.
	/// <paramref name="displayProxy"/>: <see cref="ItemWorldSync.IsDisplayProxy"/> answered true — a remote
	/// clone display proxy is presentation only and never an authority item.
	/// <paramref name="retired"/>: the scene has already taken the object out of play (it is inactive — what the
	/// clone renderer does to a stale proxy immediately before its deferred destroy), so an id stamped on it
	/// would outlive it and come back as a destroy report.
	/// <paramref name="worldItem"/>: it is part of the world rather than inventory or limb state.
	/// <paramref name="tutorialProp"/>: it is a per-player course prop — never a bind target, because binding a
	/// shared item to one would let a pickup remove another player's private course object (the claw
	/// double-give fix must not become a cross-player course stall).
	/// <paramref name="withinTolerance"/>: it sits within <see cref="RemoteItemSceneOps.AdoptTolerance"/> of the
	/// row's reported position.
	/// </summary>
	internal static bool Allows(
		bool sameDefinition,
		bool alreadySynced,
		bool displayProxy,
		bool retired,
		bool worldItem,
		bool tutorialProp,
		bool withinTolerance) =>
		sameDefinition
		&& !alreadySynced
		&& !displayProxy
		&& !retired
		&& worldItem
		&& !tutorialProp
		&& withinTolerance;
}
