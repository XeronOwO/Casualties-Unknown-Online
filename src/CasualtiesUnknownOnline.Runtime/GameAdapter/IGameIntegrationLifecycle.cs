namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The consumer-facing half of the Game Adapter's integration lifecycle: the
/// game's quit broadcast, forwarded by the plugin (the BepInEx/Unity host)
/// before the scene unloads. The patch lifecycle itself — probe, install,
/// uninstall — is driven by <c>ICuoService</c> on the same adapter instance and
/// is deliberately NOT part of a port: no consumer resolves it, and a port
/// nobody resolves only forces an implementation on a build that lacks the
/// capability.
/// </summary>
public interface IGameIntegrationLifecycle
{
	/// <summary>
	/// The game is quitting (Unity's OnApplicationQuit — broadcast before the
	/// scene unloads). The teardown must engage BEFORE the unload: the world
	/// items' OnDestroy then fires while the session still reads as alive, and
	/// reporting each as a player-operation destroy wiped the host's world
	/// copies (#191).
	/// </summary>
	void OnApplicationQuit();
}
