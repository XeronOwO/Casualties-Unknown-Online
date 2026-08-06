using System;
using CasualtiesUnknownOnline.Runtime.Session;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The Game Adapter boundary (architecture.md §4): the only layer that knows
/// the game's private types. One implementation per game build; the Runtime
/// defines the contract, the adapter project (CUO.GameAdapter) implements it.
/// The plugin resolves this via DI and forwards ICuoService lifecycle calls.
/// </summary>
public interface IGameAdapter : IDisposable
{
	/// <summary>Probes the loaded game assembly for the types/methods this build supports.</summary>
	bool ProbeGame();

	/// <summary>Human-readable capability report for startup logs.</summary>
	string CapabilityReport { get; }

	/// <summary>Installs Harmony patches. Must be safe to fail without breaking single-player.</summary>
	bool Install();

	/// <summary>Uninstalls Harmony patches.</summary>
	void Uninstall();

	/// <summary>Host side: called at run start (PreRunScript.StartRun) — captures Random.state + run settings.</summary>
	void CaptureWorldParams();

	/// <summary>Guest side: called right before world generation — applies the host's world params.</summary>
	void ApplyWorldParams(WorldStartParams parameters);
}
