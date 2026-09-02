using System;
using System.IO;
using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The typed limb-level status projection payload for
/// <see cref="ModStatusProjectionKind.LimbPhysiology"/>. It is a plain
/// DataContract in Abstractions: no Unity type, no game type, no Runtime
/// dependency. A mod publishes this DTO as the opaque byte value of a limb
/// status slot; the Game Adapter decodes it and applies the optional additive
/// overlays to the matching local vanilla limb.
///
/// Nullable properties mean "do not touch this field". The first slice covers
/// continuous physiology fields that the native limb update already modifies
/// additively; terminal limb latches (broken/dismembered/dislocated/splinted)
/// remain kernel-owned facts and are deliberately not part of this projection.
/// </summary>
[DataContract]
public sealed class ModLimbProjection
{
	/// <summary>Optional overlay to <c>Limb.bleedAmount</c>.</summary>
	[DataMember(Order = 1)]
	public float? BleedAmount { get; set; }

	/// <summary>Optional overlay to <c>Limb.skinHealth</c>.</summary>
	[DataMember(Order = 2)]
	public float? SkinHealth { get; set; }

	/// <summary>Optional overlay to <c>Limb.muscleHealth</c>.</summary>
	[DataMember(Order = 3)]
	public float? MuscleHealth { get; set; }

	/// <summary>Optional overlay to <c>Limb.infectionAmount</c>.</summary>
	[DataMember(Order = 4)]
	public float? InfectionAmount { get; set; }

	/// <summary>Serialize this projection into the opaque status value payload.</summary>
	public byte[] ToPayload()
	{
		using var stream = new MemoryStream();
		var serializer = new DataContractSerializer(typeof(ModLimbProjection));
		serializer.WriteObject(stream, this);
		return stream.ToArray();
	}

	/// <summary>Deserialize a limb status payload. Returns null when the payload is not valid.</summary>
	public static ModLimbProjection? FromPayload(byte[] payload)
	{
		if (payload is null)
		{
			return null;
		}

		try
		{
			using var stream = new MemoryStream(payload);
			var serializer = new DataContractSerializer(typeof(ModLimbProjection));
			return serializer.ReadObject(stream) as ModLimbProjection;
		}
		catch (Exception)
		{
			return null;
		}
	}
}
