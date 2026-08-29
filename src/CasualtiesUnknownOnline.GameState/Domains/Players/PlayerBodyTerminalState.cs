namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Authoritative body-level terminal latch facts in the kernel. Continuous
/// physiological values (blood, stamina, temperature, timers) remain in the
/// high-frequency character snapshot/stream; only discrete durable body states
/// that affect later gameplay belong to the domain.
/// </summary>
public sealed record PlayerBodyTerminalState(
	bool Disfigured,
	bool EyeGone,
	bool BothEyesGone,
	bool HasPulmonaryEmbolism,
	bool TriedRollingLastStand,
	bool SuccesfullyRolledLastStand,
	bool UsedNeuralBooster,
	bool FibrillationForced,
	bool MindwipeScriptPresent,
	bool MindwipeScriptActive);
