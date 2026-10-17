using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>A compound whose keys are <see cref="Keys"/>, in the order a suggestion lists them.</summary>
public sealed record ItemStateCompoundShape(IReadOnlyList<ItemStateKeyShape> Keys) : ItemStateShape;
