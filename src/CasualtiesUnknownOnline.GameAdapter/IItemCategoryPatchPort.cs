namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The item CATEGORY's patch port: what a static Harmony patch needs for the standing-item-object
/// category — the one data fact that tells a standing object from a world item (whether the
/// authoritative item data holds an id as a world row), and the report for the one refusal the
/// patches themselves make. Every other reader of the category goes through
/// <see cref="Items.StandingItems"/> and <see cref="Items.ItemWorldSync"/>; a patch class is static
/// and cannot be handed the item data by construction, so it reads <c>PatchBridge.ItemCategory</c>
/// and thereby states the dependency, instead of reaching the whole <see cref="IPatchBridge"/>
/// aggregate.
/// <para>
/// The aggregate does NOT compose this port and does not declare these members: a call written
/// against <see cref="IPatchBridge"/> cannot reach them at all, so the split is enforced by the
/// compiler rather than by convention. The seam resolves the port by casting the bound bridge, which
/// the only implementation (<c>GameAdapterBridge</c>) satisfies; the shape gate pins that class
/// declaration, so a bridge that stopped serving the port fails there. The query is the Runtime's own
/// (<c>IItemControl.IsWorldItemRegistered</c>) forwarded verbatim — one name, one meaning, one
/// answer, whichever layer asks it.
/// </para>
/// </summary>
internal interface IItemCategoryPatchPort
{
	/// <summary>
	/// Read-only: does the authoritative item data hold this id as a WORLD row? A carried id, an id
	/// the data has never judged and id 0 all answer false. The standing-item-object category is
	/// anchored on this answer — see <see cref="Items.StandingItems"/> for the whole rule.
	/// </summary>
	bool IsWorldItemRegistered(ulong itemId);

	/// <summary>
	/// A local gesture on <paramref name="item"/> was refused because it is a standing item object —
	/// another member's carried item incarnated locally, which must stay unreachable by every local
	/// gesture (the food chain reaches it through the use action, never through a pickup). The
	/// refusal is designed behaviour and rare, so it is named rather than silent: the gate that
	/// refused it is <paramref name="source"/>.
	/// </summary>
	void ReportStandingItemGestureRefused(Item item, string source);
}
