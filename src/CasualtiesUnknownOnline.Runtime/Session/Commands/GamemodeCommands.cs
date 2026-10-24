using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The mode switch: <c>/gamemode survivor|spectator</c>. It is a cheat verb (<see cref="ConsoleCommandGate.Cheat"/>),
/// so the host's own choice is the online switch and a member needs both the host's cheat-verb switch and its own
/// grant (<c>/op</c>) — the ruling the cheat family already enforces for <c>/give</c> and <c>/heal</c>.
///
/// <para>
/// The mode is LOCAL presentation: it changes this client's own camera and HUD, so nothing here crosses the wire.
/// The other entrance is the death screen's button (the same mode, reached by a dead player), and every revive
/// path leaves it; this verb is only the player's own way in and out.
/// </para>
/// </summary>
internal sealed class GamemodeCommands(
	ISpectatorControl? spectator,
	ILogger log)
{
	private readonly ISpectatorControl? _spectator = spectator;
	private readonly ILogger _log = log;

	[ConsoleCommand("gamemode", "Cheat verb: switch between normal play and spectator mode.", CommandPermission.Anyone, "/gamemode survivor|spectator", Gate = ConsoleCommandGate.Cheat)]
	internal string Gamemode(IReadOnlyList<string> args)
	{
		if (_spectator is not { } spectator)
		{
			return "The spectator mode is not available on this client: no game adapter is attached.";
		}

		if (args.Count < 2)
		{
			return spectator.IsSpectator
				? "Usage: /gamemode survivor|spectator (currently spectator)."
				: "Usage: /gamemode survivor|spectator (currently survivor).";
		}

		if (string.Equals(args[1], "spectator", StringComparison.OrdinalIgnoreCase))
		{
			if (spectator.IsSpectator)
			{
				return "Already in spectator mode.";
			}

			if (!spectator.EnterSpectatorMode())
			{
				return "Spectator mode is unavailable here: no active session, no world entered, or no local body.";
			}

			_log.LogInformation("[Command] /gamemode spectator: the local client is now a spectator.");
			return "Spectator mode on. Right-click a member to watch them; /gamemode survivor returns to play.";
		}

		if (string.Equals(args[1], "survivor", StringComparison.OrdinalIgnoreCase))
		{
			if (!spectator.IsSpectator)
			{
				return "Already playing as a survivor.";
			}

			spectator.ExitSpectatorMode();
			_log.LogInformation("[Command] /gamemode survivor: the local client is back to normal play.");
			return "Normal play.";
		}

		return $"Unknown mode '{args[1]}'. Usage: /gamemode survivor|spectator";
	}
}
