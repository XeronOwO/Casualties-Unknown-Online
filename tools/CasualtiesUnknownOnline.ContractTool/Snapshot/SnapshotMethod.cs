using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CasualtiesUnknownOnline.ContractTool.Snapshot;

/// <summary>
/// One method row. Parameter NAMES are stored (not only their types) because
/// Harmony binds patch arguments by name: a game update that renames a target
/// parameter silently detaches the patch argument, and that is a failure no
/// type-level comparison can see.
/// <see cref="IsCompilerGenerated"/> is stored for the same reason — a member
/// the game's own code reads inside a hooked method can sit in a compiler-
/// generated lambda, which no contract can name (decision 199's residual).
/// </summary>
public sealed record SnapshotMethod(
	[property: JsonPropertyName("name")] string Name,
	[property: JsonPropertyName("visibility")] string Visibility,
	[property: JsonPropertyName("isStatic")] bool IsStatic,
	[property: JsonPropertyName("isAbstract")] bool IsAbstract,
	[property: JsonPropertyName("isVirtual")] bool IsVirtual,
	[property: JsonPropertyName("isCompilerGenerated")] bool IsCompilerGenerated,
	[property: JsonPropertyName("genericParameters")] IReadOnlyList<string> GenericParameters,
	[property: JsonPropertyName("returns")] string Returns,
	[property: JsonPropertyName("parameters")] IReadOnlyList<SnapshotParameter> Parameters);
