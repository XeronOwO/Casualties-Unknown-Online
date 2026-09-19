namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Host only: the LIVE scene the member-facing trap-layout table is re-derived
/// from at SEND time.
///
/// <see cref="TrapLayoutRegistry"/> is only as fresh as its last SCAN: the
/// generation-finished edge records the layer's entities once, the layer-boundary
/// reset clears the table, and (since W6) the periodic repair re-derives it from
/// the live scene. Anything the world removes after the last scan is therefore
/// still in the table — a self-destructed turret, a broken crystal — and an empty
/// table sends nothing at all, so a member entering on a fresh layer received no
/// layout and kept its own diverged generated traps. Either way, what an entering
/// or reconnecting member received did not describe the host's world at that
/// instant. The member-side apply is an absolute align (materialize missing,
/// destroy surplus) against the snapshot, and a freshly generated guest world
/// holds no such entity, so a record the world has since removed becomes a phantom
/// the host does not own: corrected only by the next scan, and a live hazard on
/// the member until then.
///
/// <see cref="WorldEntryFanout"/> therefore refreshes through this port before
/// every group that carries the layout — the world-entry fan-out (the InWorld edge
/// and the reconnect-while-InWorld handshake both send from it), the entry-window
/// repeat repair and the periodic in-session repair — so the SEND owns the
/// freshness instead of whichever scan happened to run last. The fan-out is the
/// shared point of both entry paths, which is why the port is consumed there and
/// not at the adapter's scene event.
///
/// It is a port because the Runtime cannot read the scene: only the Game Adapter
/// can scan it, and the composition root that owns the adapter registers the
/// implementation. OPTIONAL by design, like <see cref="INativeWorldFacts"/>: a
/// composition without one (the Runtime-only test host) sends the table as last
/// derived rather than being unable to construct the fan-out.
///
/// Calling it is host-only and safe to repeat: a layer that is still generating is
/// not scanned, a repeat inside one frame is a no-op in the adapter's
/// implementation, and an EMPTY scan against a non-empty table is refused by the
/// registry — so a scene in transition can never be read as "every trap was
/// destroyed" and the fail-safe direction stays a stale entry, never a mass
/// destroy on every peer.
/// </summary>
public interface ILiveTrapLayoutSource
{
	/// <summary>
	/// Host only: re-derive the host's trap-layout table from the live scene. A
	/// caller that does not hold the host role, or a layer that is still
	/// generating, leaves the table as it is; both the derived count and a refused
	/// empty scan are observable in the adapter's log.
	/// </summary>
	void RefreshFromLiveScene();
}
