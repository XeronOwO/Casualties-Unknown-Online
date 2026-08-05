namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// A CUO framework service whose lifecycle is driven by BepInEx/Unity.
/// Microsoft.Extensions DI owns construction; BepInEx/Unity own the game loop —
/// never run a loop inside a service, the plugin forwards Unity's per-frame
/// Update into this interface (architecture.md §5.5).
/// </summary>
public interface ICuoService
{
	/// <summary>Called once after DI resolution, before <see cref="Start"/> (plugin Awake).</summary>
	void Initialize();

	/// <summary>Called once when the game is playable (reserved; no-op in the MVP).</summary>
	void Start();

	/// <summary>Called every frame on the Unity main thread (plugin Update).</summary>
	void Update();

	/// <summary>Called when the game is unloading, before <see cref="Dispose"/> (plugin OnDisable).</summary>
	void Stop();

	/// <summary>
	/// Releases unmanaged resources. Must be safe to call more than once — the DI
	/// container also disposes IDisposable singletons when the provider is disposed.
	/// </summary>
	void Dispose();
}
