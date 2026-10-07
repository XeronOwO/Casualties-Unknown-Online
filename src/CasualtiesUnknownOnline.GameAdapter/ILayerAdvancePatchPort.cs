namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The layer-transition domain's patch port: what the native end-of-layer
/// entry's hook may reach. A patch that needs this domain reads
/// <see cref="PatchBridge.LayerAdvance"/> and thereby states that dependency,
/// instead of reaching the whole <see cref="IPatchBridge"/> aggregate
/// (<c>done/patch-bridge-domain-ports.md</c> is the precedent).
/// <para>
/// It declares one member because the domain has one input: the member's own
/// choice. The native end-of-layer panel is per-client UI — it appears on the
/// client whose own body is at the layer's bottom, and only that client's entry
/// runs — while the layer a session enters is the HOST's capture, so the one step
/// a member must not take is the local regeneration. The choice is reported
/// instead, and the member follows the host's baseline and the session's world
/// entry, which is the transition every member already takes.
/// </para>
/// </summary>
internal interface ILayerAdvancePatchPort
{
	/// <summary>
	/// The local player chose to continue at the end of a layer: the game's own
	/// end-of-layer entry (<c>WorldGeneration.ContinueRun</c>) has run its guard and
	/// its local steps and is about to start the local regeneration. Returns true
	/// when that regeneration must be SUPPRESSED — this client is a guest in a live
	/// session, so the choice is reported to the host and the layer comes from the
	/// host's baseline. A host, a solo player and a client with no session return
	/// false: their own descent IS the session's advance and the game runs it.
	/// </summary>
	bool TryDelegateLocalAdvance();
}
