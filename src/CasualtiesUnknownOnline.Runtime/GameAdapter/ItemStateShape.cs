namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The completion/description half of the item state language's schema: the shape of the
/// document the adapter projects, as a small recursive type.
///
/// <para>
/// It is a SHAPE, not a validator: the adapter binds and refuses a document against the live
/// item or prefab it holds, while this type answers "what can be written here" for the
/// console's completion. Keeping them apart is what lets completion offer the keys the capture
/// really carries without duplicating a single legality rule.
/// </para>
/// </summary>
public abstract record ItemStateShape;
