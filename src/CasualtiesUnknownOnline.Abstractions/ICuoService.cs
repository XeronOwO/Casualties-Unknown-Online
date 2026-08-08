using System;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// A CUO framework service whose lifecycle is driven by BepInEx/Unity.
/// Microsoft.Extensions DI owns construction; BepInEx/Unity own the game loop —
/// never run a loop inside a service, the plugin forwards Unity's per-frame
/// Update into this interface (architecture.md §5.5).
/// The interface is an <see cref="IDisposable"/>: the container disposes
/// singleton services on provider dispose, so every implementation must be
/// safe to dispose more than once (the plugin also drives the lifecycle
/// explicitly in OnDisable).
/// </summary>
public interface ICuoService : IDisposable
{
	/// <summary>Called once after DI resolution, before <see cref="Start"/> (plugin Awake).</summary>
	void Initialize();

	/// <summary>Called once when the game is playable (reserved; no-op in the MVP).</summary>
	void Start();

	/// <summary>Called every frame on the Unity main thread (plugin Update).</summary>
	void Update();

	/// <summary>Called when the game is unloading, before <see cref="Dispose"/> (plugin OnDisable).</summary>
	void Stop();
}
