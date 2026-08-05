using System;
using System.IO;

namespace CasualtiesUnknownOnline.Core.Logging;

/// <summary>
/// ILogger writing to a file (e.g. <c>BepInEx/CUO.log</c>). Useful when the
/// BepInEx log is unavailable or you want a CUO-dedicated audit trail.
/// Never throws — logging must not crash the plugin.
/// </summary>
public sealed class FileLogger : ILogger
{
	private readonly object _sync = new();
	private readonly string _path;

	public FileLogger(string path) => _path = path;

	public void Info(string message) => Write("INFO", message);

	public void Warning(string message) => Write("WARN", message);

	public void Error(string message) => Write("ERROR", message);

	private void Write(string level, string message)
	{
		try
		{
			lock (_sync)
				File.AppendAllText(_path, $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}\r\n");
		}
		catch
		{
			// Swallow: logging is best-effort.
		}
	}
}
