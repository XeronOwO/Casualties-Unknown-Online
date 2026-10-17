using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The acceptance-only console verbs: commands that exist so a run can DRIVE a case its own UI cannot reach, never so
/// a player has a shortcut.
/// <para>
/// It is its own owner object rather than a member of <see cref="CommandConsoleService"/> for two reasons: that class
/// sits at the class-size cap, and a verb that exists for a run should be findable as a group instead of scattered
/// through the player-facing console. Registration is the same attribute scan the other owners use —
/// <see cref="ConsoleCommandRegistry.AddBuiltIns"/> — done from this object's own constructor, so the console service
/// does not grow a line for it.
/// </para>
/// <para>
/// `/blockreport` publishes one block write as THIS client. It exists because the bound under test refuses a player
/// break further from the reporter's own body than the shared reach, and proving that needs a client to report a cell
/// it could not have touched — the member side has no other way to say so. Nothing is trusted through it: the host
/// bounds the report exactly as it bounds the game's own, which is the point.
/// </para>
/// </summary>
public sealed class AcceptanceCommands
{
	private readonly IWorldControl _world;
	private readonly IEntitySyncControl _entities;
	private readonly ISessionControl _session;

	public AcceptanceCommands(
		ConsoleCommandRegistry registry,
		IWorldControl world,
		IEntitySyncControl entities,
		ISessionControl session)
	{
		_world = world;
		_entities = entities;
		_session = session;
		registry.AddBuiltIns(this);
	}

	[ConsoleCommand("blockreport", "Acceptance only: publish one block write at an offset from this client's own body.", CommandPermission.Anyone, "/blockreport <dx> <dy> <block> <playerBreak>")]
	private string BlockReport(IReadOnlyList<string> args)
	{
		if (args.Count < 5
			|| !int.TryParse(args[1], out var dx)
			|| !int.TryParse(args[2], out var dy)
			|| !ushort.TryParse(args[3], out var block)
			|| !bool.TryParse(args[4], out var playerBreak))
		{
			return "Usage: /blockreport <dx> <dy> <block> <playerBreak>";
		}

		var local = _entities.LocalPlayer;
		var x = (int)Math.Round(local.Position.X) + dx;
		var y = (int)Math.Round(local.Position.Y) + dy;
		_world.SendBlockPlacedReport(x, y, block, playerBreak);
		return $"Reported block {block} at ({x},{y}) playerBreak={playerBreak} as {_session.LocalSteamId}.";
	}

	[ConsoleCommand("entityopen", "Acceptance only: publish one building-entity open at an offset from this client's own body.", CommandPermission.Anyone, "/entityopen <dx> <dy>")]
	private string EntityOpen(IReadOnlyList<string> args)
	{
		if (args.Count < 3 || !int.TryParse(args[1], out var dx) || !int.TryParse(args[2], out var dy))
		{
			return "Usage: /entityopen <dx> <dy>";
		}

		var local = _entities.LocalPlayer;
		var x = (float)(Math.Round(local.Position.X) + dx);
		var y = (float)(Math.Round(local.Position.Y) + dy);
		_world.SendBuildingEntityOpened(new NetVector2(x, y));
		return $"Reported an open at ({x},{y}) as {_session.LocalSteamId}.";
	}
}
