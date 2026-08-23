using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Pure revive/respawn rules (KrokMP-inspired co-op lifecycle). This is the
/// L0-locked decision surface for the Unity-facing <c>RespawnCoordinator</c>
/// and the gate check inside <c>TraderRecruitCoordinator</c>: Permadeath,
/// trader-revive permission, next-level auto-revive permission, and the
/// keep-inventory/keep-skills respawn shaping. The respawn uses the full
/// character-snapshot restore path (unlike the trader recruit heal-in-place
/// slice), because the keep flags need a real inventory/skill reset when
/// disabled.
/// </summary>
internal static class RespawnPolicy
{
	/// <summary>Whether a trader recruit is allowed under the current rules.</summary>
	internal static bool CanUseTraderRecruit(RespawnOptions rules) =>
		!rules.Permadeath && rules.ReviveFromTrader;

	/// <summary>Whether a dead player is automatically revived when the host starts the next layer.</summary>
	internal static bool CanAutoReviveOnNextLevel(RespawnOptions rules) =>
		!rules.Permadeath && rules.ReviveOnNextLevel;

	/// <summary>The target is revivable when the host's authoritative snapshot says it is dead.</summary>
	internal static bool IsDead(CharacterDataMsg? data) =>
		data?.Health is { } health && !health.Alive;

	/// <summary>
	/// Build the full post-respawn character snapshot. The physiological baseline
	/// comes from <see cref="TraderRecruitPolicy.PrepareRevive"/>; the keep flags
	/// then shape inventory/skills; position is deliberately nulled so the
	/// restore does not teleport the body back to an old layer — the respawn lands
	/// at the spawn point of the current world instead.
	/// </summary>
	internal static CharacterDataMsg PrepareRespawn(
		CharacterDataMsg source,
		bool keepInventory,
		bool keepSkills)
	{
		var respawn = TraderRecruitPolicy.PrepareRevive(source);

		if (!keepInventory)
		{
			respawn.Items.Clear();
			respawn.HandSlot = 0; // wire encoding: 0 = empty hand
		}

		if (!keepSkills)
		{
			respawn.Skills = new CharacterSkillsMsg();
		}

		respawn.Position = null;
		return respawn;
	}
}
