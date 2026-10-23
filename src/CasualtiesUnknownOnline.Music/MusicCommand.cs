using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Music;

/// <summary>
/// The local console verbs: <c>/music play &lt;path&gt;</c> plays a WAV file on this client,
/// <c>/music share &lt;path&gt;</c> shares one with the session (the host in this stage),
/// <c>/music stop</c> stops both, and <c>/music status</c> answers what this client is doing. Local-only: the
/// verbs never travel, and the sharing itself rides the mod's declared packets.
/// </summary>
internal static class MusicCommand
{
	private static MusicShareService? _share;

	internal static MusicDevice Device { get; } = new();

	internal static void Bind(MusicShareService share) => _share = share;

	internal static string? Execute(IModConsoleCommandContext context)
	{
		var arguments = context.Arguments;
		if (arguments.Count == 0)
		{
			return "usage: /music play <path> | share <path> | stop | status";
		}

		switch (arguments[0].ToLowerInvariant())
		{
			case "play":
				return arguments.Count < 2
					? "usage: /music play <path>"
					: Play(string.Join(" ", arguments.Skip(1)));

			case "share":
				return arguments.Count < 2
					? "usage: /music share <path>"
					: _share?.Share(string.Join(" ", arguments.Skip(1))) ?? "sharing is unavailable (the mod is not bound)";

			case "stop":
				_share?.Stop();
				Device.Stop();
				return "stopped.";

			case "status":
				return _share?.Status() ?? "not playing.";

			default:
				return $"unknown verb '{arguments[0]}' — usage: /music play <path> | share <path> | stop | status";
		}
	}

	private static string Play(string path)
	{
		var refused = Device.PlayFile(path);
		if (refused is not null)
		{
			MusicGate.Log?.LogWarning($"[Music] refused {path}: {refused}");
			return $"refused: {refused}";
		}

		return $"playing {path} ({Device.Length:0.0}s).";
	}
}
