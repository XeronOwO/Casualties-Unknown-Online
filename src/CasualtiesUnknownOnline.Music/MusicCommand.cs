using System;
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

	internal static ConsoleAnswer Execute(IModConsoleCommandContext context)
	{
		var arguments = context.Arguments;
		if (arguments.Count == 0)
		{
			return ConsoleAnswer.Refused("usage: /music play <path> | share <path> | stop | status");
		}

		switch (arguments[0].ToLowerInvariant())
		{
			case "play":
				if (arguments.Count < 2)
				{
					return ConsoleAnswer.Refused("usage: /music play <path>");
				}

				var requested = string.Join(" ", arguments.Skip(1));
				var refused = Device.PlayFile(requested);
				if (refused is not null)
				{
					MusicGate.Log?.LogWarning($"[Music] refused {requested}: {refused}");
					return ConsoleAnswer.Refused($"refused: {refused}");
				}

				return ConsoleAnswer.Ok($"playing {requested} ({Device.Length:0.0}s).");

			case "share":
				if (arguments.Count < 2)
				{
					return ConsoleAnswer.Refused("usage: /music share <path>");
				}

				if (_share is not { } share)
				{
					return ConsoleAnswer.Refused("sharing is unavailable (the mod is not bound)");
				}

				var answer = share.Share(string.Join(" ", arguments.Skip(1)));
				return answer.StartsWith("refused", StringComparison.OrdinalIgnoreCase)
					? ConsoleAnswer.Refused(answer)
					: ConsoleAnswer.Ok(answer);

			case "stop":
				_share?.Stop();
				Device.Stop();
				return ConsoleAnswer.Ok("stopped.");

			case "status":
				return ConsoleAnswer.Ok(_share?.Status() ?? "not playing.");

			default:
				return ConsoleAnswer.Refused($"unknown verb '{arguments[0]}' — usage: /music play <path> | share <path> | stop | status");
		}
	}
}
