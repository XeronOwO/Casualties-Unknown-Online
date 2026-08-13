using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The codec's field-kind contract for the gun/mag [Saveable] components the
/// crafting digest carries (ItemStateCodec.ComponentFieldKind: 1=float 2=int
/// 3=bool 4=string 5=List&lt;string&gt; 6=enum). The enum kind (6) is NEW with the
/// crafting domain — before it, GunScript.roundInChamber was silently dropped
/// from every digest and no test noticed (the silent-death guard family:
/// GameFieldContractTests). A game update that retypes a field (or flips a
/// [SerializeField]) fails this run BEFORE the digest silently drops it.
/// The eligibility rule mirrors the codec: public, or private with
/// [SerializeField]; never static or initonly.
/// </summary>
public class CraftCodecContractTests
{
	/// <summary>One row per gun/mag field the digest covers (eligible) or deliberately skips (ineligible).</summary>
	private static readonly (string Type, string Field, bool Eligible, int Kind, string Why)[] Declared =
	[
		("GunScript", "ammoType", true, 6, "the gun's feed ammo type (immutable per prefab, carried for the round-trip)"),
		("GunScript", "roundInChamber", true, 6, "the chambered round — the enum the digest silently dropped before kind 6"),
		("GunScript", "firingMode", true, 6, "the firing mode (immutable per prefab)"),
		("GunScript", "feedType", true, 6, "the feed type (immutable per prefab)"),
		("GunScript", "roundsInMag", true, 2, "the mag's live round count — the combine face's core fact"),
		("GunScript", "magCapacity", true, 2, "the mag capacity (immutable per prefab)"),
		("GunScript", "hasMag", true, 3, "the mag presence — LoadMag's core fact"),
		("GunScript", "triggerPressed", true, 3, "the trigger state"),
		("GunScript", "firingPinStruck", true, 3, "the firing state"),
		("GunScript", "safe", true, 3, "the safety latch"),
		("GunScript", "racked", true, 3, "the rack state"),
		("GunScript", "lastRacked", true, 3, "the rack animation state"),
		("GunScript", "knockBack", true, 1, "a float gun stat"),
		("GunScript", "structureDamage", true, 1, "a float gun stat"),
		("GunScript", "animalDamage", true, 1, "a float gun stat"),
		("GunScript", "loudness", true, 1, "a float gun stat"),
		("GunScript", "desiredGasTime", true, 1, "a float gun stat"),
		("GunScript", "shotsPerFire", true, 2, "an int gun stat"),
		("GunScript", "verticalSpread", true, 1, "a float gun stat"),
		("GunScript", "conditionLossPerShot", true, 1, "a float gun stat"),
		("AmmoScript", "itemType", true, 6, "round vs magazine — the LoadMag/LoadRound branch fact"),
		("AmmoScript", "ammoType", true, 6, "the ammo family (immutable per prefab)"),
		("AmmoScript", "maxRounds", true, 2, "the magazine capacity (immutable per prefab)"),
		("AmmoScript", "rounds", true, 2, "the mag's live round count — LoadRound's core fact"),
		("GunScript", "fireSound", true, 0, "AudioClip — a Unity reference, never serialized"),
		("GunScript", "customRack", true, 0, "AudioClip — a Unity reference"),
		("GunScript", "barrel", true, 0, "Transform — a Unity reference"),
		("GunScript", "normalSprite", true, 0, "Sprite — a Unity reference"),
		("GunScript", "muzzleParticle", true, 0, "ParticleSystem — a Unity reference"),
		("GunScript", "render", false, 0, "private without [SerializeField] — the codec's eligibility rule skips it"),
		("GunScript", "it", false, 0, "private without [SerializeField]"),
		("GunScript", "gasTime", false, 0, "private without [SerializeField] — skipped even though float would serialize"),
	];

	private static int KindOf(Type type) =>
		type == typeof(float) ? 1
		: type == typeof(int) ? 2
		: type == typeof(bool) ? 3
		: type == typeof(string) ? 4
		: type == typeof(List<string>) ? 5
		: type.IsEnum ? 6
		: 0;

	[Fact]
	public void DeclaredTable_EveryFieldMatchesItsCodecKind()
	{
		var violations = new List<string>();
		foreach (var (typeName, fieldName, eligible, expectedKind, why) in Declared)
		{
			var type = GameAssemblyHost.Game.GetType(typeName);
			if (type == null)
			{
				violations.Add($"type {typeName} not found in the game assembly (renamed?)");
				continue;
			}

			var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (field == null)
			{
				violations.Add($"{typeName}.{fieldName} not found (renamed?)");
				continue;
			}

			var isEligible = field.IsPublic
				|| field.GetCustomAttributes(inherit: true).Any(a => a.GetType().Name == "SerializeField");
			if (isEligible != eligible)
			{
				violations.Add($"{typeName}.{fieldName} eligibility is {isEligible}, the codec contract says {eligible} — {why}");
				continue;
			}

			if (eligible)
			{
				// The kind matters only for eligible fields — the codec checks
				// eligibility first (ineligible fields are skipped regardless
				// of their type, e.g. private float gasTime).
				var kind = KindOf(field.FieldType);
				if (kind != expectedKind)
				{
					violations.Add($"{typeName}.{fieldName} is {field.FieldType.Name} (codec kind {kind}), the digest contract expects kind {expectedKind} — {why}");
				}
			}
		}

		Assert.True(violations.Count == 0,
			$"craft codec contracts broken against the game assembly ({violations.Count}):\n" + string.Join("\n", violations));
	}
}
