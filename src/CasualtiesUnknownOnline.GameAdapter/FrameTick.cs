using System.Diagnostics;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Diagnostics;
using CasualtiesUnknownOnline.Runtime.Session;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The adapter's frame pump: the ONE ordered walk over every domain, from the menu guards at the top of the frame to the cut seam at the bottom. The order is the
/// behaviour — a renderer before the carried-body follow, the drop flushes before the cut, the call-context boundary after everything — so it lives in one
/// place that can be read top to bottom.
/// </summary>
/// <remarks>
/// Its own type because the adapter itself is the object graph and the lifecycle (probe, install, bind); the frame walk is a sequence of calls over that graph
/// with one piece of its own state (the previous frame's local body, which is the world-entry edge for the destroy-suppression reset).
/// </remarks>
internal sealed class FrameTick(
	GameAdapterDomains domains,
	PlayerInteractionApply playerInteraction,
	LatencyInstrumentation latency)
{
	private Body? _lastLocalBody; // Unity object — == (the world-entry edge for the destroy-suppression reset)

	internal void Run()
	{
		var frameStopwatch = latency.IsEnabled ? Stopwatch.StartNew() : null;
		domains.GuestMenu.Update();
		domains.RunSettingsRange.Update();
		domains.MenuInput.Update();
		using (latency.Measure("Run"))
		{
			domains.Run.Update();
		}
		domains.Reentry.Update(); // guest: the bounded ask for the entry instruction it may have missed; host: the answer rides the event
		domains.PlayerInteraction.Update(); // host: a placement question nobody answered is refused at its deadline, not at the next give

		using (latency.Measure("WorldTime"))
		{
			domains.WorldTimeSync.Update(); // host policy + direct-write adoption + resend; guest ramp stepping and host-speed enforcement (suspended while a local initiation is in flight)
		}

		// World-entry edge: the teardown of the PREVIOUS scene finished (its
		// destroys were suppressed, #191) — the new world's real destroys report
		// again. The edge rides the local body (null in any menu scene; Unity
		// object — ==).
		var localBody = domains.Run.LocalBody;
		if (localBody != null && _lastLocalBody == null) // Unity objects — ==
		{
			domains.ItemWorldSync.ResetDestroySuppression();
		}

		// The custody projection (shadow mode): the kernel's rows against this client's objects, once a second, changing nothing. Stage 1 of
		// docs/backlog/todo/item-custody-single-authority.md — a running session with `mismatches=0` is what lets the readers be switched.
		domains.ItemCustody.Tick(domains.Session.LocalSteamId, [.. domains.Session.Members.Select(member => member.SteamId)]);

		_lastLocalBody = localBody;
		using (latency.Measure("StartGate"))
		{
			domains.Gate.Update(domains.Run.LocalBody);
		}

		domains.LayerAdvance.UpdatePanelChoices(); // the end-of-layer panel's two choices belong to the host
		domains.GenItemAuthority.Update(); // host/solo: publish the generation-time items when the generation finished
		domains.GenItemApplication.Update(domains.Run.IsEnteringWorld); // guest: apply the host's generation snapshot once the local generation finished (held while the entry loads its world)
		domains.StartingSupplies.Update(); // a player this world has no character for: the run's starting supplies, once per body (S4.3)
		using (latency.Measure("Respawn"))
		{
			domains.Respawn.Update(); // host: next-level respawn once a world generation finishes
		}
		domains.TrapLayoutScanner.Update(); // host: report the generated trap layout on the same falling edge
		domains.TrapLayoutApplication.Update(); // guest: apply a deferred layout snapshot once the local generation finished
		domains.LayerModifierSync.Update(); // guest: apply the host's layer modifier once the local generation finished
		domains.CarriedInventoryReporter.Update(); // both sides: register/report the carried inventory with instance ids once the local generation finished
		domains.ItemWorldSync.FlushPendingDrop(); // a drop that was not thrown reports at end of frame (one drop = one report)
		domains.EntityEventSync.FlushPendingDrops(); // a destructive trap's building-death drops fold into the same event after the hold window
		domains.BlockBreakSync.FlushPendingBlockBreak(); // a break's drops fold in one frame after the break — the break + drops go out as ONE message
		using (latency.Measure("ItemPosition"))
		{
			if (domains.Session.Role == SessionRole.Host && domains.Session.SessionActive)
			{
				domains.ItemPositionAuthority.Update(); // the host's physics is the single position authority
			}
			else
			{
				domains.ItemPositionFollow.Update(); // the guest copies simulate locally (ground-layer isolation), soft-corrected by the host's stream
			}
		}

		using (latency.Measure("WorldEvent"))
		{
			domains.WorldEventSync.Update();
		}
		domains.GeyserStateSync.Update(); // host/solo: capture + broadcast the geysers' liquid types once the generation finished
		domains.RadiationLineSync.Update(); // host: publish the authoritative radiation-line state (active + timeGone)
		domains.Spectator.Update(); // every client: a dead player's own camera, from this client's own facts
		domains.EntitySpawnSync.Update(); // the creation channel's deferred reports (a geyser's type, after its child Start) and carried-data applications
		using (latency.Measure("Fluid"))
		{
			domains.FluidSync.Update(); // host: stream the members' fluid viewports (10 Hz diff + 1 Hz full)
		}

		using (latency.Measure("Trader"))
		{
			domains.TradeSync.Update(); // host: the 5 s trader-state fallback broadcast
		}
		domains.BlockBreakSync.Update(); // expire break records without a consuming drops report
		using (latency.Measure("Renderer"))
		{
			domains.Renderer.Update(localBody);
		}

		// A carried local body follows the remote carrier's RENDER clone. The
		// renderer must run first so the carrier clone has already received this
		// frame's SessionStatePump interpolation; reading the raw entity buffer
		// there would reintroduce the step/snap the render path intentionally
		// smooths away.
		playerInteraction.UpdateCarriedBody(localBody);

		domains.CharacterRagdollSync.Update(); // flush clone-creation-race ragdoll one-shots after the renderer created clones
		domains.RemoteBackpack.Update();
		domains.RemoteMedical.Update();
		using (latency.Measure("EnemySync"))
		{
			domains.EnemySync.Update(); // host: capture + publish the simulated enemies; guest: (event-driven bind/apply)
		}

		using (latency.Measure("EnemyCombat"))
		{
			domains.EnemyCombat.Update(); // host: enemy combat decisions (target guidance rides the patch callbacks; bite arbitration here)
		}
		domains.TutorialClawSync.Update(); // host: publish the tutorial-claw presentation state (Runtime throttles the 20 Hz fan-out)

		// THE CUT SEAM — the last step of the frame pump. Every domain above has
		// finished this frame's work (the drop/break flushes included), so the
		// kernel is at a committed revision and no frame flush is in flight: the
		// one point where the armed /save cut and the host's deliberate menu return
		// are taken. Nothing else in the adapter may call the save control.
		using (latency.Measure("SaveSeam"))
		{
			domains.SaveCutSeam.Update(domains.Run.IsInWorld);
		}

		if (frameStopwatch != null)
		{
			latency.RecordFrame(frameStopwatch.Elapsed.TotalMilliseconds);
		}

		latency.Flush();

		// The call-identity guard's frame boundary (CallContext.EndFrame): a scope still open here was leaked
		// by a frame that threw between its Prefix and its Postfix, and one line per leak episode is what an
		// operator reads — never one per frame.
		var recovery = CallContext.EndFrame();
		if (recovery is not null)
		{
			domains.Log.LogError(
				"[CallContext] frame boundary: {Recovery} — a game-side throw inside a patched original skips the postfix that owns the release.",
				recovery);
		}
	}
}
