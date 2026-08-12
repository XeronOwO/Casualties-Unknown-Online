namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// A CUO mod's contract. The framework discovers the mod via its
/// <see cref="CuoModAttribute"/> (first update frame — BepInEx loads plugins
/// one by one, so the framework's own Awake would miss plugins loaded after
/// it), instantiates it, calls <see cref="Bind"/> (the context — snapshot of
/// the session + message channel + logger + events), then drives the standard
/// ICuoService lifecycle: Initialize → Start → Update (every frame, Unity
/// main thread) → Stop → Dispose. The first-frame discovery runs all four
/// first stages (Bind/Initialize/Start) in the same frame; a mod crashing in
/// any stage is caught and logged — the pump and the other mods survive.
/// </summary>
public interface ICuoMod : ICuoService
{
	/// <summary>
	/// Called once at discovery, before <see cref="ICuoService.Initialize"/>:
	/// the mod stores the context and wires its handlers.
	/// </summary>
	void Bind(IModContext context);
}
