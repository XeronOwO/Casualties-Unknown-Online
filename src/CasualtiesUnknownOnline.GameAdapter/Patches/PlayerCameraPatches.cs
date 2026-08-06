namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Guest input runs the ORIGINAL HandleInput unchanged (movement, jump, crouch,
/// aim, switch hands, crafting menu, attack/mine) — the guest body is locally
/// simulated, per the sync model ("local compute, remote verify/sync"). World
/// mutations (block damage) are synced via the BlockDamaged message by the
/// WorldGeneration patch; nothing input-level is intercepted.
/// </summary>
internal static class PlayerCameraPatches
{
	// No patches here: guest input is fully local (single-player feel). Block
	// damage sync lives in WorldGenPatches (WorldGeneration.DamageBlock hook).
}
