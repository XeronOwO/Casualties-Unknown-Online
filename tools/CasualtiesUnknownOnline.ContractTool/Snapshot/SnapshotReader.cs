using System.IO;
using System.Linq;
using System.Text.Json;

namespace CasualtiesUnknownOnline.ContractTool.Snapshot;

/// <summary>
/// Reads a snapshot back for <c>diff</c>. The reader is deliberately strict: an
/// unknown schema id fails loudly, a row the artifact declares but does not carry
/// (null collections, a nameless type) is refused instead of being dereferenced,
/// and the census a snapshot declares about itself is verified against the rows it
/// carries — a truncated or hand-edited artifact must not be able to produce a
/// quietly smaller diff than the build deserves.
/// A malformed document raises <see cref="JsonException"/>, which the CLI
/// translates into its documented "input could not be read" exit code.
/// </summary>
public static class SnapshotReader
{
	private static readonly JsonSerializerOptions Options = new()
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
	};

	/// <summary>Reads and validates the snapshot at <paramref name="path"/>.</summary>
	public static SnapshotDocument Read(string path)
	{
		var fullPath = Path.GetFullPath(path);
		if (!File.Exists(fullPath))
		{
			throw new FileNotFoundException($"snapshot not found: {fullPath}", fullPath);
		}

		var document = JsonSerializer.Deserialize<SnapshotDocument>(File.ReadAllText(fullPath), Options)
			?? throw new InvalidDataException($"snapshot '{fullPath}' is empty");
		if (document.Schema != SnapshotSchema.Snapshot)
		{
			throw new InvalidDataException($"snapshot '{fullPath}' declares schema '{document.Schema}'; this tool reads '{SnapshotSchema.Snapshot}'");
		}

		if (document.Assembly is null || document.Counts is null || document.Types is null || document.Contracts is null)
		{
			throw new InvalidDataException($"snapshot '{fullPath}' is missing its identity, census, type or contract block");
		}

		foreach (var type in document.Types)
		{
			if (type is null || type.Name is null || type.Methods is null || type.Fields is null || type.Properties is null || type.EnumMembers is null)
			{
				throw new InvalidDataException($"snapshot '{fullPath}' carries a type row without a name or without its member lists");
			}
		}

		var declared = document.Counts;
		var actual = new SnapshotCounts(
			document.Types.Count,
			document.Types.Sum(type => type.Methods.Count),
			document.Types.Sum(type => type.Fields.Count),
			document.Types.Sum(type => type.Properties.Count),
			document.Types.Sum(type => type.EnumMembers.Count),
			document.Types.Sum(type => type.Fields.Count(field => field.IsSerialized)),
			document.Contracts.Count);
		if (actual != declared)
		{
			throw new InvalidDataException($"snapshot '{fullPath}' declares {declared} but its rows are {actual}");
		}

		return document;
	}
}
