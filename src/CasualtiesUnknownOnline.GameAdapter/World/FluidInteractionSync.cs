using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The player's fluid interactions (drinking): the drinking side applies the
/// full local effect (the game's DrinkLiquid — body effects immediate, its
/// grid cell cleared) and reports; the host executes on its own grid (the
/// authority: the cell non-empty → clear → the handler relays; already empty →
/// a duplicate event, every side is consistent) and relays (source excluded,
/// it already applied locally). The bath-soiled water (LiquidAffect's
/// SetLiquid(5)) is NOT reported: low-frequency/low-perception, healed by the
/// 1 Hz viewport snapshot (recorded design decision).
/// </summary>
internal sealed class FluidInteractionSync(IWorldControl world, ISessionControl session, ILogger<FluidInteractionSync> log)
{
	private readonly IWorldControl _world = world;
	private readonly ISessionControl _session = session;
	private readonly ILogger<FluidInteractionSync> _log = log;

	internal void BindToSession() => _world.FluidInteractionReceived += OnFluidInteraction;

	internal void Unbind() => _world.FluidInteractionReceived -= OnFluidInteraction;

	/// <summary>The local player drank — report the consumed cell (one operation,
	/// one message; the type is read from the host's grid, the authority).</summary>
	internal void OnDrinkReported(Vector2Int pos)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		_log.LogInformation("[Fluid] drink at=({X},{Y}) origin=Report.", pos.x, pos.y);
		_world.SendFluidInteraction(new FluidInteractionMsg
		{
			Kind = FluidInteractionMsg.KindDrink,
			X = pos.x,
			Y = pos.y,
		});
	}

	private void OnFluidInteraction(ulong sender, FluidInteractionMsg msg)
	{
		if (msg.Kind != FluidInteractionMsg.KindDrink)
		{
			return;
		}

		// Execute on the LOCAL grid: the host's is the authority, a guest's is
		// the streamed copy — clearing is idempotent on both (the cell already
		// empty = a duplicate event).
		var fluid = FluidManager.main;
		var world = WorldGeneration.world;
		if (fluid == null || world == null) // Unity objects — ==
		{
			return;
		}

		var x = Mathf.Clamp(msg.X, 0, (int)world.width - 1);
		var y = Mathf.Clamp(msg.Y, 0, (int)world.height - 1);
		if (fluid.fluid[x, y] == 0)
		{
			return; // already empty — a duplicate event (the first one cleared it everywhere)
		}

		fluid.fluid[x, y] = 0;
		_log.LogInformation("[Fluid] drink applied at=({X},{Y}) from {Sender}.", msg.X, msg.Y, sender);
	}
}
