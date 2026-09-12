namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The item domain's side of the host's layer boundary. A new layer generates,
/// so the previous layer's WORLD-ROOTED items are gone with its scene: both the
/// authoritative kernel records and the projected world table must go with it,
/// while carried items (and their contents) cross the boundary. The world domain
/// drives this from its reset bus (<c>WorldService.ResetWorldLayerTables</c>) so
/// every layer-scoped table family resets together — the world-entity, player,
/// enemy and fluid domains already do.
/// </summary>
public interface IWorldItemLayerReset
{
	/// <summary>
	/// Host/solo: drop every world-rooted item for the new layer and tell the
	/// guests (the committed reset batch is broadcast like any other item fact).
	/// A guest does nothing here: its replay kernel resets when the host's batch
	/// arrives, so the two sides cannot disagree about which layer's items exist.
	/// </summary>
	void ResetForNewLayer();
}
