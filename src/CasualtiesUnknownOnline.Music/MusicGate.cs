using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Music;

/// <summary>
/// The log sink the static command and device classes read; the CUO mod entry binds it from the mod-scoped
/// logger at Bind (the BepInEx shell's Awake may not run when CUO discovers the assembly by itself — the
/// discipline the other satellite mods learned the hard way).
/// </summary>
internal static class MusicGate
{
	internal static ILogger? Log { get; private set; }

	internal static void Bind(ILogger log) => Log = log;
}
