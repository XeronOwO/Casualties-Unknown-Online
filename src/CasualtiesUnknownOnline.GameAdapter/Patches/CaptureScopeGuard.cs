using CasualtiesUnknownOnline.GameAdapter.Character;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The eligibility rule every capture scope shares: a sound is reported only for
/// THIS client's own body action, in a plain local call. A render clone (a
/// <see cref="RemoteBodyDriver"/> in the parents) and any remote application
/// both stay silent, so a remote-driven mutation can never be reported as the
/// local player's action — the rule every write-report patch follows. The
/// local-action check is what keeps a scope from being opened INSIDE a
/// RemoteApply scope: a capture there would be meaningless (the facts travel
/// with the intent being applied) and it keeps the scope stack clean. The two
/// <c>Sound.Play</c> patches read the CHAIN (<c>CallContext.IsWithin(RemoteApply)</c>)
/// for their echo guard, so they stay silent under any nested sub-scope a remote
/// application opens.
/// </summary>
internal static class CaptureScopeGuard
{
	/// <summary>No scope is open above this call — a plain game-code action.</summary>
	internal static bool IsLocalAction() => CallContext.Current == CallContext.Origin.LocalAction;

	/// <summary>The body is not a remote render clone (and may be another player's — a treatment targets a limb, not the actor).</summary>
	internal static bool IsOwnBody(Body? body) =>
		body != null // Unity object — ==
		&& body.GetComponentInParent<RemoteBodyDriver>() == null; // Unity object — ==

	/// <summary>The body is this client's own player body.</summary>
	internal static bool IsLocalPlayerBody(Body? body) =>
		IsOwnBody(body)
		&& PlayerCamera.main != null // Unity object — ==
		&& body == PlayerCamera.main.body; // Unity objects — ==
}
