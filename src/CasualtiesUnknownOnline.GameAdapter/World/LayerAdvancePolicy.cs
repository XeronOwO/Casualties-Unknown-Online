using CasualtiesUnknownOnline.Runtime.Session;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The end-of-layer choice's decisions, without the game: who reports the choice,
/// and whether an admitted request may drive the host's advance. Kept out of
/// <see cref="LayerAdvanceCoordinator"/> so both rules have a test host — a
/// coordinator that reads <c>WorldGeneration</c> and <c>PlayerCamera</c> directly
/// cannot be constructed outside the game.
/// <para>
/// <see cref="DecideDrive"/> is the native entry's own guard
/// (<c>WorldGeneration.ContinueRun</c>, WorldGeneration.cs:1013) minus its
/// LOCAL-BODY clause: <c>!this.doingRegen &amp;&amp; !this.generatingWorld &amp;&amp;
/// this.worldExists</c> are the clauses that ask "can this world take a
/// regeneration right now?", and the fourth — the local body standing at the
/// layer's bottom — is the clause that ties the choice to the body that made it,
/// which is exactly what a member's request does not have.
/// </para>
/// </summary>
internal static class LayerAdvancePolicy
{
	/// <summary>Does this side hand its descent to the host instead of taking it? A guest in a live session does: the layer's baseline is the host's capture and its own regeneration has nothing new to apply. The host's and a solo player's own choice IS the session's advance.</summary>
	internal static bool ShouldDelegateLocalAdvance(SessionRole role, bool sessionActive) =>
		role == SessionRole.Guest && sessionActive;

	/// <summary>
	/// Must the local descent be suppressed? Both facts are needed and neither implies the other: the call
	/// came from the panel's entry's own body (a pod's or the console's own descent is the game's) AND the
	/// port accepted the delegation (it refused when there was nothing on this side to arbitrate the choice
	/// against). Kept as a named decision because it is the line the whole member-side fix hangs on.
	/// </summary>
	internal static bool ShouldSuppressLocalDescent(bool fromPanelEntry, bool delegated) =>
		fromPanelEntry && delegated;

	/// <summary>May an admitted member request drive the host's advance? The native entry's own guard, minus its local-body clause.</summary>
	internal static LayerAdvanceDecision DecideDrive(
		SessionRole role,
		bool sessionActive,
		bool worldExists,
		bool generating,
		bool regenerating)
	{
		if (role != SessionRole.Host || !sessionActive)
		{
			return LayerAdvanceDecision.NotThisSides;
		}

		if (!worldExists)
		{
			return LayerAdvanceDecision.NoWorld;
		}

		return generating || regenerating ? LayerAdvanceDecision.AlreadyAdvancing : LayerAdvanceDecision.Drive;
	}
}
