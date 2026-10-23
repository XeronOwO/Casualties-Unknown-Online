using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Music;

/// <summary>
/// The local console verbs: <c>/music play &lt;path&gt;</c> plays a WAV file from this machine's disk,
/// <c>/music stop</c> stops it, and <c>/music status</c> answers whether anything plays. Local-only — the
/// sharing half's verbs land with the transfer stage, and this half deliberately works with or without a
/// session, which is what makes the decoder verifiable on one client.
/// </summary>
internal static class MusicCommand
{
	private static MusicDevice? _device;

	private static MusicDevice Device => _device ??= new MusicDevice();

	internal static string? Execute(IModConsoleCommandContext context)
	{
		var arguments = context.Arguments;
		if (arguments.Count == 0)
		{
			return "usage: /music play <path> | /music stop | /music status";
		}

		switch (arguments[0].ToLowerInvariant())
		{
			case "play":
				return arguments.Count < 2
					? "usage: /music play <path>"
					: Play(string.Join(" ", arguments.Skip(1)));

			case "stop":
				Device.Stop();
				return "stopped.";

			case "status":
				return Device.Playing
					? $"playing ({Device.Position:0.0}s of {Device.Length:0.0}s)."
					: "not playing.";

			default:
				return $"unknown verb '{arguments[0]}' — usage: /music play <path> | /music stop | /music status";
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
