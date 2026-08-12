using System;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// One mod found by <see cref="ModRegistry.Discover"/>: the framework-built
/// manifest (from the [CuoMod] attribute — the single declared source) and the
/// type the registry validated (public, concrete, ICuoMod-implementing, with a
/// public parameterless constructor). The instance is created by the ModService
/// (discovery = facts, instantiation + lifecycle = ownership).
/// </summary>
public sealed record DiscoveredMod(ModManifest Manifest, Type Type);
