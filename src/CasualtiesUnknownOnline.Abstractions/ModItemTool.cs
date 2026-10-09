using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Melee/tool behavior for a custom item. The values are plain data in
/// Abstractions; the Game Adapter converts them into a vanilla
/// <c>AttackInfo</c> and installs the <c>ItemInfo.useAction</c> delegate at item
/// registration time, so mods never pass game delegates.
/// </summary>
public sealed class ModItemTool
{
	/// <summary>Damage dealt to enemies and traders.</summary>
	public float Damage { get; set; } = 25f;

	/// <summary>Damage dealt to structures and tiles.</summary>
	public float StructuralDamage { get; set; } = 25f;

	/// <summary>Multiplier applied to the vanilla attack cooldown.</summary>
	public float AttackCooldownMultiplier { get; set; } = 0.66f;

	/// <summary>Maximum hit distance.</summary>
	public float Distance { get; set; } = 2.5f;

	/// <summary>Knockback force applied on hit.</summary>
	public float KnockBack { get; set; } = 270f;

	/// <summary>Base cooldown between uses.</summary>
	public float Cooldown { get; set; } = 0.35f;

	/// <summary>Animator trigger/state name used for attacks.</summary>
	public string AttackAnimation { get; set; } = "SwingAnim";

	/// <summary>Stamina consumed per attack.</summary>
	public float StaminaUse { get; set; } = 0.5f;

	/// <summary>Enables piercing hits.</summary>
	public bool Piercing { get; set; }

	/// <summary>Swing sounds randomly used when attacking.</summary>
	public List<string> SwingSounds
	{
		get;
		set => field = value ?? [];
	} = ["BSSwing1", "BSSwing2", "BSSwing3", "BSSwing4"];

	/// <summary>Playback volume for swing sounds.</summary>
	public float Volume { get; set; } = 0.5f;

	/// <summary>Visual swing rotation amount.</summary>
	public float RotateAmount { get; set; } = 15.5f;

	/// <summary>Enables physical swing hit logic.</summary>
	public bool PhysicalSwing { get; set; } = true;

	/// <summary>Plays the attack animation when attacking.</summary>
	public bool DoAttackAnimation { get; set; } = true;

	/// <summary>Enables extra damage vs metal.</summary>
	public bool MetalMoreDamage { get; set; }

	/// <summary>Tool condition lost per successful hit.</summary>
	public float ConditionLossOnHit { get; set; } = 0.02f;
}
