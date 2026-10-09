using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The typed limb-level status projection value for
/// <see cref="ModStatusProjectionKind.LimbPhysiology"/>. It is a plain data
/// object in Abstractions: no Unity type, no game type, no Runtime dependency. A
/// mod publishes it as the value of a limb status slot (built with
/// <see cref="ToValue"/>, or by hand as a map with these field names); the Game
/// Adapter reads it and applies the optional additive overlays to the matching
/// local vanilla limb.
///
/// A field that is ABSENT means "do not touch this field", which is what a null
/// property meant; a field that is present but is not a number refuses the whole
/// value. The first slice covers continuous physiology fields that the native
/// limb update already modifies additively; terminal limb latches
/// (broken/dismembered/dislocated/splinted) remain kernel-owned facts and are
/// deliberately not part of this projection.
/// </summary>
public sealed class ModLimbProjection
{
	/// <summary>The <c>bleedAmount</c> overlay field.</summary>
	public const string FieldBleedAmount = "bleedAmount";

	/// <summary>The <c>skinHealth</c> overlay field.</summary>
	public const string FieldSkinHealth = "skinHealth";

	/// <summary>The <c>muscleHealth</c> overlay field.</summary>
	public const string FieldMuscleHealth = "muscleHealth";

	/// <summary>The <c>infectionAmount</c> overlay field.</summary>
	public const string FieldInfectionAmount = "infectionAmount";

	/// <summary>Optional overlay to <c>Limb.bleedAmount</c>.</summary>
	public float? BleedAmount { get; set; }

	/// <summary>Optional overlay to <c>Limb.skinHealth</c>.</summary>
	public float? SkinHealth { get; set; }

	/// <summary>Optional overlay to <c>Limb.muscleHealth</c>.</summary>
	public float? MuscleHealth { get; set; }

	/// <summary>Optional overlay to <c>Limb.infectionAmount</c>.</summary>
	public float? InfectionAmount { get; set; }

	/// <summary>Render this projection as the status value that travels. A null overlay is left out of the map, which is how "do not touch this field" travels.</summary>
	public ModValue ToValue()
	{
		var fields = new List<(string Key, ModValue Value)>();
		Add(fields, FieldBleedAmount, BleedAmount);
		Add(fields, FieldSkinHealth, SkinHealth);
		Add(fields, FieldMuscleHealth, MuscleHealth);
		Add(fields, FieldInfectionAmount, InfectionAmount);
		return ModValue.Map([.. fields]);
	}

	/// <summary>Read a limb projection out of a value. Returns false when the value is not a map or a field it carries is not a number.</summary>
	public static bool TryFromValue(ModValue? value, out ModLimbProjection projection)
	{
		projection = null!;
		if (value is null || value.Kind != ModValueKind.Map
			|| !TryReadNumber(value, FieldBleedAmount, out var bleedAmount)
			|| !TryReadNumber(value, FieldSkinHealth, out var skinHealth)
			|| !TryReadNumber(value, FieldMuscleHealth, out var muscleHealth)
			|| !TryReadNumber(value, FieldInfectionAmount, out var infectionAmount))
		{
			return false;
		}

		projection = new ModLimbProjection
		{
			BleedAmount = bleedAmount,
			SkinHealth = skinHealth,
			MuscleHealth = muscleHealth,
			InfectionAmount = infectionAmount,
		};

		return true;
	}

	private static void Add(List<(string Key, ModValue Value)> fields, string field, float? overlay)
	{
		if (overlay is { } number)
		{
			fields.Add((field, ModValue.Number(number)));
		}
	}

	private static bool TryReadNumber(ModValue value, string field, out float? number)
	{
		number = null;
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
