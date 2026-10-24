using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The keyboard path to the spectator camera: <c>/spectate &lt;selector&gt;</c> starts following a member and
/// <c>/spectate off</c> goes back to the death screen. It is its own owner for the same reason the
/// host-administration group is — a command group is how the console grows, so adding commands never means
/// growing <see cref="CommandConsoleService"/> past the architecture gate.
///
/// <para>
/// Permission is <see cref="CommandPermission.Anyone"/> and that is the point: watching is THIS client's own
/// camera, so it is nobody's to authorise. What a session decides is what the mode allows, and the mode is the
/// only gate — the free camera is the mode's own capability. The member list in the Online UI is the other
/// chooser; this is the keyboard one the ticket asks for. Every answer names its kind, so a refusal reads as one.
/// </para>
/// </summary>
internal sealed class SpectatorCommands(
	ISessionControl session,
	IEntitySyncControl entities,
	ISpectatorControl? spectator,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly IEntitySyncControl _entities = entities;
	private readonly ISpectatorControl? _spectator = spectator;
	private readonly ILogger _log = log;

	[ConsoleCommand("spectate", "Watch a teammate's body while in spectator mode (/gamemode spectator).", CommandPermission.Anyone, "/spectate <selector> | /spectate off | /spectate freefly on|off", CommandArgumentKind.Selector)]
	internal ConsoleAnswer Spectate(IReadOnlyList<string> args)
	{
		if (_spectator is not { } spectator)
		{
			// The camera lives in the game adapter: a process without one has nothing to point, and saying so
			// beats a command that looks like it worked.
			return ConsoleAnswer.Refused("Watching is not available on this client: no game adapter is attached.");
		}

		if (args.Count < 2)
		{
			return ConsoleAnswer.Refused("Usage: /spectate <selector> | /spectate off");
		}

		if (string.Equals(args[1], "freefly", StringComparison.OrdinalIgnoreCase))
		{
			if (args.Count < 3)
			{
				return ConsoleAnswer.Refused("Usage: /spectate freefly on | /spectate freefly off");
			}

			var wanted = string.Equals(args[2], "on", StringComparison.OrdinalIgnoreCase);
			if (!wanted && !string.Equals(args[2], "off", StringComparison.OrdinalIgnoreCase))
			{
				return ConsoleAnswer.Refused("Usage: /spectate freefly on | /spectate freefly off");
			}

			if (!spectator.TrySetFreeFly(wanted))
			{
				return ConsoleAnswer.Refused("This client is not watching anyone, so there is no camera to switch.");
			}

			return ConsoleAnswer.Ok(wanted ? "Free camera on." : "Free camera off.");
		}

		if (string.Equals(args[1], "off", StringComparison.OrdinalIgnoreCase))
		{
			if (!spectator.IsWatching)
			{
				return ConsoleAnswer.Ok("Not watching anyone.");
			}

			spectator.StopWatching();
			_log.LogInformation("[Command] /spectate off: back to the death screen.");
			return ConsoleAnswer.Ok("Stopped watching.");
		}

		var targets = CommandPlayerTargets.Resolve(_session, _entities, args[1]);
		if (targets.Count == 0)
		{
			return ConsoleAnswer.Refused($"No players match selector '{args[1]}'.");
		}

		if (targets.Count > 1)
		{
			// One camera, one body: following several is not a thing the game's camera can do, and picking the
			// first match for the player would be the fallback the entry-mapping rule forbids.
			return ConsoleAnswer.Refused($"Selector '{args[1]}' matches {targets.Count} players; name one of them.");
		}

		if (!spectator.TryWatch(targets[0]))
		{
			_log.LogInformation("[Command] /spectate {Selector} was refused: nothing to watch from this client's state.", args[1]);
			return ConsoleAnswer.Refused("Cannot watch that member: this client is not in spectator mode (/gamemode spectator), or their body is not here to follow.");
		}

		_log.LogInformation("[Command] /spectate is now following {SteamId}.", targets[0]);
		return ConsoleAnswer.Ok($"Watching {targets[0]}.");
	}
}
