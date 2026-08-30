namespace CasualtiesUnknownOnline.GameState.Domains.Entities;

/// <summary>
/// Kernel-side discriminator for enemy-proximity side-effect result events.
/// The Runtime projection maps this to the presentation effect kind when it
/// restores the Game Adapter event surface.
/// </summary>
public enum EnemyCombatEffectKind : byte
{
	ElderHorrorTick = 1,
	ElderHorrorDefeat = 2,
	XalorisSepticTick = 3,
	GrabberGrabbed = 4,
}
