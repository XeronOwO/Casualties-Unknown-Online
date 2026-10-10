using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The melee/tool behavior of a custom item. This is the contract a consumer
/// reads: <see cref="IModItemDefinition.Tool"/> is interface-typed, so a mod that
/// computes a tool value hands over its own implementation instead of filling in
/// the framework's, exactly as it does one level up for the declaration itself.
///
/// <see cref="ModItemTool"/> is the framework's ready-made implementation: use it
/// when every value is a constant, and implement this interface when one is
/// computed. Partial customisation is composition — an implementation hands back
/// a filled default for the members it does not touch — never inheritance from
/// the data class, which stays <c>sealed</c>.
///
/// The members are the whole contract on purpose: what a tool DOES with them
/// (the vanilla attack the Game Adapter builds from them, the condition it takes
/// off the item) is the framework's rule and cannot be overridden here.
/// </summary>
public interface IModItemTool
{
	/// <summary>Damage dealt to enemies and traders.</summary>
	float Damage { get; }

	/// <summary>Damage dealt to structures and tiles.</summary>
	float StructuralDamage { get; }

	/// <summary>Multiplier applied to the vanilla attack cooldown.</summary>
	float AttackCooldownMultiplier { get; }

	/// <summary>Maximum hit distance.</summary>
	float Distance { get; }

	/// <summary>Knockback force applied on hit.</summary>
	float KnockBack { get; }

	/// <summary>Base cooldown between uses.</summary>
	float Cooldown { get; }

	/// <summary>Animator trigger/state name used for attacks.</summary>
	string AttackAnimation { get; }

	/// <summary>Stamina consumed per attack.</summary>
	float StaminaUse { get; }

	/// <summary>Enables piercing hits.</summary>
	bool Piercing { get; }

	/// <summary>Swing sounds randomly used when attacking.</summary>
	List<string> SwingSounds { get; }

	/// <summary>Playback volume for swing sounds.</summary>
	float Volume { get; }

	/// <summary>Visual swing rotation amount.</summary>
	float RotateAmount { get; }

	/// <summary>Enables physical swing hit logic.</summary>
	bool PhysicalSwing { get; }

	/// <summary>Plays the attack animation when attacking.</summary>
	bool DoAttackAnimation { get; }

	/// <summary>Enables extra damage vs metal.</summary>
	bool MetalMoreDamage { get; }

	/// <summary>Tool condition lost per successful hit.</summary>
	float ConditionLossOnHit { get; }
}
