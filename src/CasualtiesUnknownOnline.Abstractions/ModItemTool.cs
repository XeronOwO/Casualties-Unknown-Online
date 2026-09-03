using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Melee/tool behavior for a custom item. The values are plain data in
/// Abstractions; the Game Adapter converts them into a vanilla
/// <c>AttackInfo</c> and installs the <c>ItemInfo.useAction</c> delegate at item
/// registration time, so mods never pass game delegates.
/// </summary>
[DataContract]
public sealed class ModItemTool
{
	/// <summary>Damage dealt to enemies and traders.</summary>
	[DataMember(Order = 1)]
	public float Damage { get; set; } = 25f;

	/// <summary>Damage dealt to structures and tiles.</summary>
	[DataMember(Order = 2)]
	public float StructuralDamage { get; set; } = 25f;

	/// <summary>Multiplier applied to the vanilla attack cooldown.</summary>
	[DataMember(Order = 3)]
	public float AttackCooldownMultiplier { get; set; } = 0.66f;

	/// <summary>Maximum hit distance.</summary>
	[DataMember(Order = 4)]
	public float Distance { get; set; } = 2.5f;

	/// <summary>Knockback force applied on hit.</summary>
	[DataMember(Order = 5)]
	public float KnockBack { get; set; } = 270f;

	/// <summary>Base cooldown between uses.</summary>
	[DataMember(Order = 6)]
	public float Cooldown { get; set; } = 0.35f;

	/// <summary>Animator trigger/state name used for attacks.</summary>
	[DataMember(Order = 7)]
	public string AttackAnimation { get; set; } = "SwingAnim";

	/// <summary>Stamina consumed per attack.</summary>
	[DataMember(Order = 8)]
	public float StaminaUse { get; set; } = 0.5f;

	/// <summary>Enables piercing hits.</summary>
	[DataMember(Order = 9)]
	public bool Piercing { get; set; }

	/// <summary>Swing sounds randomly used when attacking.</summary>
	[DataMember(Order = 10)]
	public List<string> SwingSounds { get; set; } = ["BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4"];

	/// <summary>Playback volume for swing sounds.</summary>
	[DataMember(Order = 11)]
	public float Volume { get; set; } = 0.5f;

	/// <summary>Visual swing rotation amount.</summary>
	[DataMember(Order = 12)]
	public float RotateAmount { get; set; } = 15.5f;

	/// <summary>Enables physical swing hit logic.</summary>
	[DataMember(Order = 13)]
	public bool PhysicalSwing { get; set; } = true;

	/// <summary>Plays the attack animation when attacking.</summary>
	[DataMember(Order = 14)]
	public bool DoAttackAnimation { get; set; } = true;

	/// <summary>Enables extra damage vs metal.</summary>
	[DataMember(Order = 15)]
	public bool MetalMoreDamage { get; set; }

	/// <summary>Tool condition lost per successful hit.</summary>
	[DataMember(Order = 16)]
	public float ConditionLossOnHit { get; set; } = 0.02f;
}
