using System.Text.Json.Serialization;

namespace CasualtiesUnknownOnline.ContractTool.Snapshot;

/// <summary>
/// Which build a snapshot describes. <see cref="Sha256"/> is the file hash of
/// the snapshotted assembly: it makes the artifact self-identifying (two
/// snapshots of the same build carry the same hash) and it is what a report
/// quotes so a reader can tell which build a verdict belongs to.
/// </summary>
public sealed record AssemblyIdentity(
	[property: JsonPropertyName("name")] string Name,
	[property: JsonPropertyName("version")] string Version,
	[property: JsonPropertyName("moduleVersionId")] string ModuleVersionId,
	[property: JsonPropertyName("sha256")] string Sha256);
