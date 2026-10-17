using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>What a scalar value's kind is, and, for a named set (a liquid id), the names it admits.</summary>
public sealed record ItemStateScalarShape(ItemStateValueKind Kind, IReadOnlyList<string>? Choices = null) : ItemStateShape;
