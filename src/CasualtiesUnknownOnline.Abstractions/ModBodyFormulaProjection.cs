using System;
using System.IO;
using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The typed body-level status projection payload for
/// <see cref="ModStatusProjectionKind.BodyFormula"/>. It is a plain
/// DataContract in Abstractions: no Unity type, no game type, no Runtime
/// dependency. A mod publishes this DTO as the opaque byte value of a body
/// status slot; the Game Adapter decodes it and applies the contributed
/// deltas to the local vanilla body.
///
/// The field set is deliberately limited to the body values that are safe to
/// project as an additive overlay after the native body update: encumbrance,
/// immunity, jump speed, and average pain. Continuous circulation targets
/// (heart rate, respiratory rate, blood pressure) are intentionally not in
/// this first slice because a post-update additive overlay cannot express a
/// target offset without changing the native formula; those need a later
/// dedicated GameAdapter target patch seam.
/// </summary>
[DataContract]
public sealed class ModBodyFormulaProjection
{
	/// <summary>Contribution to <c>Body.maxEncumberance</c>.</summary>
	[DataMember(Order = 1)]
	public float MaxEncumbrance { get; set; }

	/// <summary>Contribution to <c>Body.totalEncumberance</c>.</summary>
	[DataMember(Order = 2)]
	public float TotalEncumbrance { get; set; }

	/// <summary>Contribution to <c>Body.immunity</c>.</summary>
	[DataMember(Order = 3)]
	public float Immunity { get; set; }

	/// <summary>Contribution to <c>Body.jumpSpeed</c>.</summary>
	[DataMember(Order = 4)]
	public float JumpSpeed { get; set; }

	/// <summary>Contribution to <c>Body.averagePain</c>.</summary>
	[DataMember(Order = 5)]
	public float AveragePain { get; set; }

	/// <summary>Serialize this projection into the opaque status value payload.</summary>
	public byte[] ToPayload()
	{
		using var stream = new MemoryStream();
		var serializer = new DataContractSerializer(typeof(ModBodyFormulaProjection));
		serializer.WriteObject(stream, this);
		return stream.ToArray();
	}

	/// <summary>Deserialize a body-formula status payload. Returns null when the payload is not valid.</summary>
	public static ModBodyFormulaProjection? FromPayload(byte[] payload)
	{
		if (payload is null)
		{
			return null;
		}

		try
		{
			using var stream = new MemoryStream(payload);
			var serializer = new DataContractSerializer(typeof(ModBodyFormulaProjection));
			return serializer.ReadObject(stream) as ModBodyFormulaProjection;
		}
		catch (Exception)
		{
			return null;
		}
	}
}
