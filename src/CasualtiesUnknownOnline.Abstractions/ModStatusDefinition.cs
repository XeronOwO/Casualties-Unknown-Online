using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The versioned, mod-authored data contract for one static status descriptor.
/// It is a plain data object in Abstractions: no Unity type, no game type, no
/// Runtime dependency. The payload is registered through the opaque
/// <see cref="IModContent"/> channel with
/// <see cref="ModContentKind.Status"/>. This seam describes the status type
/// and its presentation/save metadata; the per-player/per-limb runtime values
/// belong to a future typed mod-data domain and are deliberately not part of
/// this contract.
/// </summary>
[DataContract]
public sealed class ModStatusDefinition
{
	/// <summary>Player-facing status name.</summary>
	[DataMember(Order = 1)]
	public string DisplayName { get; set; } = "";

	/// <summary>Player-facing status description.</summary>
	[DataMember(Order = 2)]
	public string Description { get; set; } = "";

	/// <summary>Whether the status is body-level or per-limb.</summary>
	[DataMember(Order = 3)]
	public ModStatusScope Scope { get; set; } = ModStatusScope.Body;

	/// <summary>
	/// Whether a future status runtime may persist this status value in a
	/// mod-owned save payload. Static metadata only; no save is implemented by
	/// this seam.
	/// </summary>
	[DataMember(Order = 4)]
	public bool SaveEnabled { get; set; } = true;

	/// <summary>Optional id of a <see cref="ModMoodleDefinition"/> used to present this status.</summary>
	[DataMember(Order = 5)]
	public string MoodleId { get; set; } = "";

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	[DataMember(Order = 6)]
	public Dictionary<string, string> CustomData { get; set; } = [];

	/// <summary>
	/// When true for a limb-scoped status, the local vanilla moodle row shows
	/// one moodle per affected limb instead of collapsing all limb presences
	/// into a single row. Only meaningful with
	/// <see cref="ModStatusScope.Limb"/>.
	/// </summary>
	[DataMember(Order = 7)]
	public bool ShowPerLimbMoodles { get; set; }

	/// <summary>
	/// Optional static per-limb moodle routing entries. Each maps a vanilla
	/// limb name to a <see cref="ModMoodleDefinition"/> id. Entries are only
	/// used when <see cref="ShowPerLimbMoodles"/> is true and the status is
	/// limb-scoped; a limb without a matching entry falls back to
	/// <see cref="MoodleId"/>.
	/// </summary>
	[DataMember(Order = 8)]
	public List<ModLimbMoodleBinding> LimbMoodles { get; set; } = [];

	/// <summary>
	/// Resolve the moodle id that should be presented for one affected limb.
	/// Returns <see cref="MoodleId"/> for body-scoped statuses, when
	/// per-limb rows are disabled, or when no authored
	/// <see cref="LimbMoodles"/> entry matches the limb name.
	/// </summary>
	public string ResolveMoodleId(string? limbName)
	{
		if (!ShowsPerLimbMoodles || limbName is null)
		{
			return MoodleId;
		}

		foreach (var binding in LimbMoodles ?? [])
		{
			if (binding is null)
			{
				continue;
			}

			if (string.Equals(binding.LimbName, limbName, StringComparison.OrdinalIgnoreCase))
			{
				return binding.MoodleId;
			}
		}

		return MoodleId;
	}

	/// <summary>Whether this status asks for one moodle row per affected limb.</summary>
	public bool ShowsPerLimbMoodles => Scope == ModStatusScope.Limb && ShowPerLimbMoodles;

	/// <summary>Serialize this definition into the opaque payload format.</summary>
	public byte[] ToPayload()
	{
		using var stream = new MemoryStream();
		var serializer = new DataContractSerializer(typeof(ModStatusDefinition));
		serializer.WriteObject(stream, this);
		return stream.ToArray();
	}

	/// <summary>Deserialize a status definition payload. Returns null when the payload is not a valid status definition.</summary>
	public static ModStatusDefinition? FromPayload(byte[] payload)
	{
		if (payload is null)
		{
			return null;
		}

		try
		{
			using var stream = new MemoryStream(payload);
			var serializer = new DataContractSerializer(typeof(ModStatusDefinition));
			return serializer.ReadObject(stream) as ModStatusDefinition;
		}
		catch (Exception)
		{
			// Any deserialization failure means the payload is not a valid
			// status definition under the current contract; the binder should
			// refuse it rather than fail the whole mod discovery.
			return null;
		}
	}
}
