using System;
using System.Collections.Generic;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The finiteness guard on a character report: every float the host will map onto a body, or whose
/// value can reach the host model through the display inventory's derived numbers, must be finite
/// before the snapshot is stored, because a NaN or an infinity that reached the host's simulated
/// clone would poison every later rule — the native stages compute with it, and the 10 Hz row would
/// push it to the owner. That is the health/skills/limbs block plus the item condition, liquid
/// amounts and float component state (<c>Item.totalWeight</c> scales with the condition; container
/// encumbrance sums contents). Integer/string item state is presentation-only and is not scanned,
/// so one corrupt label cannot freeze a player's whole physiological report.
///
/// <para>
/// The field list is derived from the message types themselves (every <c>float</c> property), so a
/// field added to the wire is validated without a second list to forget. The lookup table is built
/// once; a report is judged at 1 Hz per player.
/// </para>
/// </summary>
internal static class CharacterDataFinite
{
	private static readonly PropertyInfo[] HealthFloats = FloatProperties(typeof(CharacterHealthMsg));
	private static readonly PropertyInfo[] LimbFloats = FloatProperties(typeof(CharacterLimbMsg));
	private static readonly PropertyInfo[] SkillFloats = FloatProperties(typeof(CharacterSkillsMsg));

	/// <summary>
	/// The first non-finite physiological field of <paramref name="data"/>, named for the log and
	/// the refusal, or null when the report is usable.
	/// </summary>
	internal static string? FirstNonFinite(CharacterDataMsg data)
	{
		if (data.Health is { } health && FirstNonFinite(health, HealthFloats) is { } healthField)
		{
			return "health." + healthField;
		}

		if (data.Skills is { } skills && FirstNonFinite(skills, SkillFloats) is { } skillField)
		{
			return "skills." + skillField;
		}

		foreach (var limb in data.Limbs)
		{
			if (FirstNonFinite(limb, LimbFloats) is { } limbField)
			{
				return $"limb {limb.Index}.{limbField}";
			}
		}

		foreach (var item in data.Items)
		{
			if (FirstNonFinite(item) is { } itemField)
			{
				return "item " + itemField;
			}
		}

		return null;
	}

	/// <summary>The item fields that reach the host model through weight/encumbrance; see the type remarks.</summary>
	private static string? FirstNonFinite(CharacterItemMsg item)
	{
		if (!IsFinite(item.Condition))
		{
			return item.ItemId + ".Condition";
		}

		foreach (var liquid in item.Liquids)
		{
			if (!IsFinite(liquid.Amount))
			{
				return item.ItemId + ".Liquid " + liquid.LiquidId;
			}
		}

		foreach (var component in item.Components)
		{
			foreach (var field in component.Fields)
			{
				if (field.Kind == 1 && !IsFinite(field.FloatValue))
				{
					return $"{item.ItemId}.{component.TypeName}.{field.Name}";
				}
			}
		}

		foreach (var content in item.Contents)
		{
			if (FirstNonFinite(content) is { } nested)
			{
				return item.ItemId + "/" + nested;
			}
		}

		return null;
	}

	/// <summary>The same verdict over an already-decoded health block (the adapter's own snapshot re-check).</summary>
	internal static string? FirstNonFinite(CharacterHealthMsg health) => FirstNonFinite(health, HealthFloats);

	/// <summary>
	/// The log-and-refuse gate every authority write shares: a non-finite field is named once and the
	/// caller keeps the previous snapshot. One definition, so the save path, the host's own snapshot
	/// and the event merges cannot disagree about what "refused whole by name" means.
	/// </summary>
	internal static bool Refuse(ILogger log, ulong peer, CharacterDataMsg data, string path)
	{
		if (FirstNonFinite(data) is not { } field)
		{
			return false;
		}

		log.LogError("[Character] refusing a non-finite {Path} for {Peer}: {Field} — the previous snapshot stays.",
			path, peer, field);
		return true;
	}

	/// <summary>One limb row's float scan (the enemy/injury event payloads).</summary>
	internal static string? FirstNonFiniteLimb(CharacterLimbMsg limb) => FirstNonFinite(limb, LimbFloats);

	/// <summary>
	/// The float scan over ANY event message's own public float properties (the in-place enemy
	/// appliers, which do not build a CharacterDataMsg). Nested messages are the caller's job.
	/// </summary>
	internal static string? FirstNonFiniteFields(object value) => FirstNonFinite(value, FloatProperties(value.GetType()));

	private static string? FirstNonFinite(object target, PropertyInfo[] fields)
	{
		foreach (var field in fields)
		{
			if (!IsFinite((float)field.GetValue(target)!))
			{
				return field.Name;
			}
		}

		return null;
	}

	/// <summary>net48 has no <c>float.IsFinite</c>.</summary>
	internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

	private static PropertyInfo[] FloatProperties(Type type)
	{
		var properties = new List<PropertyInfo>();
		foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			if (property.PropertyType == typeof(float) && property.CanRead)
			{
				properties.Add(property);
			}
		}

		return [.. properties];
	}
}
