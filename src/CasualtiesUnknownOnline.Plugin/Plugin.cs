using BepInEx;
using BepInEx.Logging;
using CasualtiesUnknownOnline.Core.Logging;

namespace CasualtiesUnknownOnline;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("CasualtiesUnknown.exe")]
public class Plugin : BaseUnityPlugin
{
	internal static new ManualLogSource Logger = null!;

	private void Awake()
	{
		// Plugin startup logic
		Logger = base.Logger;
		Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
		LogBridge.Initialize(new BepInExLogSink(Logger));
	}

	/// <summary>Bridges CUO Core's ILogger to BepInEx logging.</summary>
	private sealed class BepInExLogSink : ILogger
	{
		private readonly ManualLogSource _source;

		public BepInExLogSink(ManualLogSource source) => _source = source;

		public void Info(string message) => _source.LogInfo(message);

		public void Warning(string message) => _source.LogWarning(message);

		public void Error(string message) => _source.LogError(message);
	}
}
