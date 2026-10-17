namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// One item state's schema: the root compound's shape. A consumer walks it with a partial
/// document to answer "which keys are legal at this position" — the console's completion is
/// the only one today.
/// </summary>
public sealed record ItemStateSchema(ItemStateCompoundShape Root);
