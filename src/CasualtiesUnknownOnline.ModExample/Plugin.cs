using BepInEx;

namespace CasualtiesUnknownOnline.ModExample;

/// <summary>
/// The BepInEx plugin SHELL — it exists only so BepInEx loads this assembly
/// (the chainloader loads plugins one by one and Awakes each; the CUO framework
/// then discovers the [CuoMod] class in its first frame — after every plugin's
/// Awake). The shell MUST stay empty: the [CuoMod] class is instantiated by
/// CUO, never by BepInEx — a type playing both roles would yield two instances
/// (the double-instance trap, docs/mod-api.md). All business logic lives in
/// <see cref="ExampleMod"/> and references CUO.Abstractions only.
/// </summary>
[BepInPlugin("CasualtiesUnknownOnline.ModExample", "CUO Mod Example", "0.1.0")]
public sealed class Plugin : BaseUnityPlugin
{
}
