namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The layer-transition domain's patch port: what the native layer-advance hook may
/// reach. A patch that needs this domain reads
/// <see cref="PatchBridge.LayerAdvance"/> and thereby states that dependency,
/// instead of reaching the whole <see cref="IPatchBridge"/> aggregate
/// (<c>done/patch-bridge-domain-ports.md</c> is the precedent).
/// <para>
/// It declares both directions of one transition. The native end-of-layer panel is
/// per-client UI — it appears on the client whose own body is at the layer's bottom,
/// and only that client's entry runs — while the layer a session enters is the
/// HOST's capture, so the one step a member must not take on its own is the local
/// regeneration for a choice it made. The choice is reported instead
/// (<see cref="TryDelegateLocalAdvance"/>). The host's own descent is the opposite
/// case: it is the session's step, and every member must take its local
/// regeneration for it, in parallel (<see cref="OnHostDescent"/>).
/// </para>
/// </summary>
internal interface ILayerAdvancePatchPort
{
	/// <summary>
	/// A local descent the SESSION owns is about to start (the panel's click, or the drill pod's own descent):
	/// report the step to the host instead of taking it. Returns true when that regeneration must be SUPPRESSED
	/// — this client is a guest in a live session, so the choice is reported and the layer comes from the host's
	/// baseline. A host, a solo player and a client with no session return false: their own descent IS the
	/// session's advance and the game runs it. <paramref name="twice"/> is the native two-layer flag (the drill
	/// pod), carried into the request so the host drives the step the requester would have taken.
	/// </summary>
	bool TryDelegateLocalAdvance(bool twice);

	/// <summary>
	/// A local <c>WorldGeneration.RegenerateWorld</c> is about to start and the local
	/// regeneration was NOT suppressed. On the host of a live session this is the
	/// shared descent edge: the members are told now, before the regeneration's scene
	/// transition can reach them as an exit, so each generates its own new layer in
	/// parallel. On a guest (its own pod/console descent, or no session) and in solo
	/// play this is not the session's step and the port sends nothing.
	/// <paramref name="twice"/> is the native two-layer flag (the drill pod).
	/// </summary>
	void OnHostDescent(bool twice);
}
