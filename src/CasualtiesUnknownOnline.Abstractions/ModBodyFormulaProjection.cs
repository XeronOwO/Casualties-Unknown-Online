namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The typed body-level status projection value for
/// <see cref="ModStatusProjectionKind.BodyFormula"/>. It is a plain data object
/// in Abstractions: no Unity type, no game type, no Runtime dependency. A mod
/// publishes it as the value of a body status slot (built with
/// <see cref="ToValue"/>, or by hand as a map with these field names); the Game
/// Adapter reads it and applies the contributed deltas to the local vanilla
/// body.
///
/// The field set covers body values that are safe to project as an additive
/// overlay after the native body update (encumbrance, immunity, jump speed,
/// average pain) plus continuous circulation targets (heart rate, respiratory
/// rate, blood pressure). Circulation targets use a dedicated GameAdapter
/// formula seam: the overlay is removed before <c>Body.HandleCirculation</c>
/// runs, the native formula sees the unmodified base, and the current mod
/// offset is reapplied after the formula so it stays stable frame to frame.
///
/// A map field that is absent reads as zero, the default a DataMember used to
/// carry; a field that is present but is not a number refuses the whole value,
/// so a typo cannot silently become a zero.
/// </summary>
public sealed class ModBodyFormulaProjection
{
	/// <summary>The <c>maxEncumbrance</c> contribution field.</summary>
	public const string FieldMaxEncumbrance = "maxEncumbrance";

	/// <summary>The <c>totalEncumbrance</c> contribution field.</summary>
	public const string FieldTotalEncumbrance = "totalEncumbrance";

	/// <summary>The <c>immunity</c> contribution field.</summary>
	public const string FieldImmunity = "immunity";

	/// <summary>The <c>jumpSpeed</c> contribution field.</summary>
	public const string FieldJumpSpeed = "jumpSpeed";

	/// <summary>The <c>averagePain</c> contribution field.</summary>
	public const string FieldAveragePain = "averagePain";

	/// <summary>The <c>heartRateOffset</c> field.</summary>
	public const string FieldHeartRateOffset = "heartRateOffset";

	/// <summary>The <c>respiratoryRateOffset</c> field.</summary>
	public const string FieldRespiratoryRateOffset = "respiratoryRateOffset";

	/// <summary>The <c>bloodPressureOffset</c> field.</summary>
	public const string FieldBloodPressureOffset = "bloodPressureOffset";

	/// <summary>Contribution to <c>Body.maxEncumberance</c>.</summary>
	public float MaxEncumbrance { get; set; }

	/// <summary>Contribution to <c>Body.totalEncumberance</c>.</summary>
	public float TotalEncumbrance { get; set; }

	/// <summary>Contribution to <c>Body.immunity</c>.</summary>
	public float Immunity { get; set; }

	/// <summary>Contribution to <c>Body.jumpSpeed</c>.</summary>
	public float JumpSpeed { get; set; }

	/// <summary>Contribution to <c>Body.averagePain</c>.</summary>
	public float AveragePain { get; set; }

	/// <summary>Stable offset exposed on <c>Body.heartRate</c> around the native circulation formula.</summary>
	public float HeartRateOffset { get; set; }

	/// <summary>Stable offset exposed on <c>Body.respiratoryRate</c> around the native circulation formula.</summary>
	public float RespiratoryRateOffset { get; set; }

	/// <summary>Stable offset exposed on <c>Body.bloodPressure</c> around the native circulation formula.</summary>
	public float BloodPressureOffset { get; set; }

	/// <summary>Render this projection as the status value that travels.</summary>
	public ModValue ToValue() =>
		ModValue.Map(
			(FieldMaxEncumbrance, ModValue.Number(MaxEncumbrance)),
			(FieldTotalEncumbrance, ModValue.Number(TotalEncumbrance)),
			(FieldImmunity, ModValue.Number(Immunity)),
			(FieldJumpSpeed, ModValue.Number(JumpSpeed)),
			(FieldAveragePain, ModValue.Number(AveragePain)),
			(FieldHeartRateOffset, ModValue.Number(HeartRateOffset)),
			(FieldRespiratoryRateOffset, ModValue.Number(RespiratoryRateOffset)),
			(FieldBloodPressureOffset, ModValue.Number(BloodPressureOffset)));

	/// <summary>Read a body-formula projection out of a value. Returns false when the value is not a map or a field it carries is not a number.</summary>
	public static bool TryFromValue(ModValue? value, out ModBodyFormulaProjection projection)
	{
		projection = null!;
		if (value is null || value.Kind != ModValueKind.Map
			|| !TryReadNumber(value, FieldMaxEncumbrance, out var maxEncumbrance)
			|| !TryReadNumber(value, FieldTotalEncumbrance, out var totalEncumbrance)
			|| !TryReadNumber(value, FieldImmunity, out var immunity)
			|| !TryReadNumber(value, FieldJumpSpeed, out var jumpSpeed)
			|| !TryReadNumber(value, FieldAveragePain, out var averagePain)
			|| !TryReadNumber(value, FieldHeartRateOffset, out var heartRateOffset)
			|| !TryReadNumber(value, FieldRespiratoryRateOffset, out var respiratoryRateOffset)
			|| !TryReadNumber(value, FieldBloodPressureOffset, out var bloodPressureOffset))
		{
			return false;
		}

		projection = new ModBodyFormulaProjection
		{
			MaxEncumbrance = maxEncumbrance,
			TotalEncumbrance = totalEncumbrance,
			Immunity = immunity,
			JumpSpeed = jumpSpeed,
			AveragePain = averagePain,
			HeartRateOffset = heartRateOffset,
			RespiratoryRateOffset = respiratoryRateOffset,
			BloodPressureOffset = bloodPressureOffset,
		};

		return true;
	}

	private static bool TryReadNumber(ModValue value, string field, out float number)
	{
		number = 0f;
		if (!value.TryGetField(field, out var found))
		{
			return true;
		}

		if (!found.TryGetNumber(out var parsed))
		{
			return false;
		}

		number = (float)parsed;
		return true;
	}
}
