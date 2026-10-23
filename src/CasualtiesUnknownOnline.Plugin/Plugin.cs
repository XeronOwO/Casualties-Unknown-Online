using BepInEx;
using BepInEx.Logging;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The BepInEx 5 entry. It owns ONE job now: create the host that runs the plugin, and hand it the logger and
/// config. The startup itself lives in <see cref="PluginHost"/>, and that split is load-bearing for the
/// single-file package: a type whose FIELDS name packed assemblies cannot be loaded before the resolver
/// exists, so the entry must name none of them.
///
/// <para>
/// The static constructor installs the packed resolver. In the normal deployment the assembly carries no
/// <c>Packed.</c> resources, so the handler is inert; in the packed artifact it must exist before the entry's
/// <c>Awake</c> is compiled, which is the only place it can be installed early enough for the entry.
/// </para>
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("CasualtiesUnknown.exe")]
public class Plugin : BaseUnityPlugin
{
	internal static new ManualLogSource Logger = null!;

	static Plugin()
	{
		PackedAssemblyResolver.Install();
	}

	private void Awake()
	{
		Logger = base.Logger;
		var host = gameObject.AddComponent<PluginHost>();
		host.Initialize(Logger, Config);
	}
}
