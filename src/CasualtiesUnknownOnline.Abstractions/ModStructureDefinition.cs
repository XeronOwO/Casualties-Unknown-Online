using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The versioned, mod-authored data contract for one static multi-block
/// structure definition. It is a plain data object in Abstractions: no Unity
/// type, no game type, no Runtime dependency. The payload is registered through
/// the opaque <see cref="IModContent"/> channel; the Game Adapter decodes it
/// and exposes the structure to the mod-facing structure placement seam.
///
/// The grid is authored as rows from top to bottom. The first row is the visual
/// top of the structure; placement treats the supplied origin as the bottom-left
/// block coordinate of the grid. A cell marked <c>'.'</c> or <c>' '</c> is air.
/// Every other marker must map to exactly one of <see cref="VanillaBlocks"/> or
/// <see cref="TileIds"/>.
/// </summary>
[DataContract]
public sealed class ModStructureDefinition
{
	/// <summary>Player-facing structure name.</summary>
	[DataMember(Order = 1)]
	public string DisplayName { get; set; } = "";

	/// <summary>Player-facing structure description.</summary>
	[DataMember(Order = 2)]
	public string Description { get; set; } = "";

	/// <summary>Structure width in blocks.</summary>
	[DataMember(Order = 3)]
	public int Width { get; set; } = 1;

	/// <summary>Structure height in blocks.</summary>
	[DataMember(Order = 4)]
	public int Height { get; set; } = 1;

	/// <summary>
	/// Visible grid rows from top to bottom. Each row must contain exactly
	/// <see cref="Width"/> characters; the whole list must contain exactly
	/// <see cref="Height"/> rows.
	/// </summary>
	[DataMember(Order = 5)]
	public List<string> Rows { get; set; } = [];

	/// <summary>
	/// Marker character → vanilla block index. Markers are single-character
	/// strings; <c>'.'</c> and <c>' '</c> are always air. A marker must not
	/// appear in both this map and <see cref="TileIds"/>.
	/// </summary>
	[DataMember(Order = 6)]
	public Dictionary<string, int> VanillaBlocks { get; set; } = [];

	/// <summary>
	/// Marker character → custom tile content id. The referenced tile must be
	/// registered by a shared-content mod through <see cref="ModContentKind.Tile"/>
	/// before the structure can be placed.
	/// </summary>
	[DataMember(Order = 7)]
	public Dictionary<string, string> TileIds { get; set; } = [];

	/// <summary>
	/// Optional future worldgen distribution counts, one per depth. This seam
	/// stores the authored values for later worldgen providers but does not
	/// perform automatic distribution yet.
	/// </summary>
	[DataMember(Order = 8)]
	public List<int> SpawnCounts { get; set; } = [];

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	[DataMember(Order = 9)]
	public Dictionary<string, string> CustomData { get; set; } = [];

	/// <summary>Serialize this definition into the opaque payload format.</summary>
	public byte[] ToPayload()
	{
		using var stream = new MemoryStream();
		var serializer = new DataContractSerializer(typeof(ModStructureDefinition));
		serializer.WriteObject(stream, this);
		return stream.ToArray();
	}

	/// <summary>Deserialize a structure definition payload. Returns null when the payload is not a valid structure definition.</summary>
	public static ModStructureDefinition? FromPayload(byte[] payload)
	{
		if (payload is null)
		{
			return null;
		}

		try
		{
			using var stream = new MemoryStream(payload);
			var serializer = new DataContractSerializer(typeof(ModStructureDefinition));
			return serializer.ReadObject(stream) as ModStructureDefinition;
		}
		catch (Exception)
		{
			// Any deserialization failure means the payload is not a valid
			// structure definition under the current contract; the binder should
			// refuse it rather than fail the whole mod discovery.
			return null;
		}
	}
}
