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
/// camera, so it is nobody's to authorise. What a session decides is what the rules ALLOW — the free camera, and
/// whether an unconscious player may watch at all — and those are read where the camera is, not here. The member
/// list in the Online UI is the other chooser; this is the keyboard one the ticket asks for.
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
	internal string Spectate(IReadOnlyList<string> args)
	{
		if (_spectator is not { } spectator)
		{
			// The camera lives in the game adapter: a process without one has nothing to point, and saying so
			// beats a command that looks like it worked.
			return "Watching is not available on this client: no game adapter is attached.";
		}

		if (args.Count < 2)
		{
			return "Usage: /spectate <selector> | /spectate off";
		}

		if (string.Equals(args[1], "freefly", StringComparison.OrdinalIgnoreCase))
		{
			if (args.Count < 3)
			{
				return "Usage: /spectate freefly on | /spectate freefly off";
			}

			var wanted = string.Equals(args[2], "on", StringComparison.OrdinalIgnoreCase);
			if (!wanted && !string.Equals(args[2], "off", StringComparison.OrdinalIgnoreCase))
			{
				return "Usage: /spectate freefly on | /spectate freefly off";
			}

			if (!spectator.TrySetFreeFly(wanted))
			{
				return "The host's rules do not allow the free camera.";
			}

			return wanted ? "Free camera on." : "Free camera off.";
		}
		if (string.Equals(args[1], "off", StringComparison.OrdinalIgnoreCase))
		{
			if (!spectator.IsWatching)
			{
				return "Not watching anyone.";
			}

			spectator.StopWatching();
			_log.LogInformation("[Command] /spectate off: back to the death screen.");
			return "Stopped watching.";
		}

		var targets = CommandPlayerTargets.Resolve(_session, _entities, args[1]);
		if (targets.Count == 0)
		{
			return $"No players match selector '{args[1]}'.";
		}

		if (targets.Count > 1)
		{
			// One camera, one body: following several is not a thing the game's camera can do, and picking the
			// first match for the player would be the fallback the entry-mapping rule forbids.
			return $"Selector '{args[1]}' matches {targets.Count} players; name one of them.";
		}

		if (!spectator.TryWatch(targets[0]))
		{
			_log.LogInformation("[Command] /spectate {Selector} was refused: nothing to watch from this client's state.", args[1]);
			return "Cannot watch that member: your character is not dead (or the rules do not allow watching), or their body is not here to follow.";
		}

		_log.LogInformation("[Command] /spectate is now following {SteamId}.", targets[0]);
		return $"Watching {targets[0]}.";
	}
}
