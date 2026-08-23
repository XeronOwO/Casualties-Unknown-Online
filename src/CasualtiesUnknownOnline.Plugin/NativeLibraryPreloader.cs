using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Logging;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Loads <c>steam_api64.dll</c> from the plugin folder before any Steam call.
/// Kept out of the BepInEx lifecycle class so <c>Plugin</c> stays a thin shell.
/// </summary>
internal static class NativeLibraryPreloader
{
	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr LoadLibrary(string lpFileName);

	internal static void Preload(ManualLogSource logger)
	{
		try
		{
			var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var path = Path.Combine(dir ?? "", "steam_api64.dll");
			if (LoadLibrary(path) == IntPtr.Zero)
			{
				logger.LogWarning($"CUO: LoadLibrary failed for {path} (Win32 error {Marshal.GetLastWin32Error()})");
			}
		}
		catch (Exception ex)
		{
			logger.LogWarning($"CUO: native library preload failed: {ex.Message}");
		}
	}
}
