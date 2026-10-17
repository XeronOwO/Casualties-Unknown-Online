namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>A list whose elements share one shape.</summary>
public sealed record ItemStateListShape(ItemStateShape Element) : ItemStateShape;
