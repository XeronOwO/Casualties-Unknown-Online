using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The world-fluid domain (#129) — ONE coordinator owning the
/// host-authoritative fluid chain: the host simulates the world grid alone
/// (FluidSimulationAuthority — the game's per-side step is replaced by a
/// multi-member pass over every member's viewport) and streams each member's
/// viewport (10 Hz changed-box diff + 1 Hz full snapshot, absolute RLE
/// overwrites); the guest never simulates and renders the streamed regions
/// (FluidRegionApplication); the player's drinking is a report → host-execute →
/// relay chain (FluidInteractionSync). The patches are thin adapters calling
/// the bridge's OnFluidFixedUpdate / OnFluidDrinkReported.
/// </summary>
internal sealed class FluidWorldSync(
	IWorldControl world, ISessionControl session, EntitySyncService entities, ILoggerFactory loggerFactory)
{
	private readonly IWorldControl _world = world;
	private readonly FluidSimulationAuthority _authority = new(world, session, entities, loggerFactory.CreateLogger<FluidSimulationAuthority>());
	private readonly FluidRegionApplication _application = new(loggerFactory.CreateLogger<FluidRegionApplication>());
	private readonly FluidInteractionSync _interaction = new(world, session, loggerFactory.CreateLogger<FluidInteractionSync>());

	internal void BindToSession()
	{
		_world.FluidRegionReceived += _application.Apply;
		_interaction.BindToSession();
	}

	internal void Unbind()
	{
		_world.FluidRegionReceived -= _application.Apply;
		_interaction.Unbind();
	}

	/// <summary>Per physical frame: the host drives the multi-member simulation
	/// (guest: the authority's role guard makes this a no-op — the guest's grid
	/// only ever changes through the streamed regions and the local drink).</summary>
	internal void OnFluidFixedUpdate() => _authority.Step();

	/// <summary>Per frame: the host streams the members' viewports (no-op on guest).</summary>
	internal void Update() => _authority.Update();

	/// <summary>The local player drank (DrinkLiquid ran with the full local
	/// effect) — report the consumed cell (guest → host; host → broadcast).</summary>
	internal void OnDrinkReported(Vector2Int pos) => _interaction.OnDrinkReported(pos);
}
