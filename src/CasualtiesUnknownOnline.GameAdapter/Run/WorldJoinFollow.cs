using System;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// The guest's world-join follow: the host's WorldJoin instruction becomes a local run start, with the two rules that make it safe — a duplicate instruction never
/// restarts the run, and a member following a descent does not take the invite (its baseline promise is undone and the follow keeps going).
/// </summary>
/// <remarks>
/// Its own type because the join is a small state machine of its own (wait for the pre-run script, start the run or the tutorial, never twice) that the coordinator
/// only pumps. It reads the run phase and the descent's activity through delegates and writes the phase through three narrow calls, so the phase stays owned by
/// one object while the rules live here.
/// </remarks>
internal sealed class WorldJoinFollow(
	IWorldControl world,
	CharacterDataSync characterData,
	GuestMenuGuard guestMenu,
	Func<RunPhase> phase,
	Func<bool> descentActive,
	Func<bool> inWorld,
	Action setJoinPending,
	Action setIdle,
	Action setStarting,
	ILogger log)
{
	private bool joinIsTutorial;

	/// <summary>
	/// Guest side: the host told us to enter the world (WorldJoin). The menu
	/// may still be loading when it arrives (a right-click "Join Game" launches
	/// a fresh process) — wait for PreRunScript, then start the run. The entry
	/// kind rides in the message (the world params do not exist yet at the
	/// moment the host clicks start, so the biome read would misjudge a
	/// tutorial for a run). Duplicate WorldJoin (a retried handshake answers
	/// each retry with a fresh copy) must not restart the run — a second
	/// StartRun starts a second WaitLoad/LoadScene coroutine over the first,
	/// corrupting the loading flow and diverging the generated world. The
	/// phase machine makes that structural: only Idle accepts a join.
	/// </summary>
	internal void OnJoin(bool isTutorial)
	{
		if (phase() != RunPhase.Idle)
		{
			// Already following a run — duplicate instruction ignored.
			if (descentActive())
			{
				// This is the host's own world-entry invite, sent to members it still reads as out of
				// the world: this member is NOT following it (it is generating the same layer from the
				// descent). The invite's baseline promise dropped the params its generation is using —
				// put them back. The kernel checkpoint that follows is the host's current session state;
				// its world projections are deferred by their own generation guards until this member's
				// generation settles, so it cannot replay into the rebuilding world.
				world.CancelWorldJoinBaselineDrop();
				log.LogInformation("[LayerDescent] ignored the host's world-entry invite while following the descent — the baseline its promise dropped was restored.");
			}

			return;
		}

		joinIsTutorial = isTutorial;
		setJoinPending();
		TryStart();
	}

	internal void TryStart()
	{
		if (inWorld())
		{
			return;
		}

		if (HarmonyTraverse.IsGenerating())
		{
			// This client generates for a reason of its own (a run start, or a producer
			// that owns its descent): the instruction belongs to a layer this side is
			// already leaving. A member's end-of-layer choice is no longer such a
			// reason — it is delegated, and the session re-invites this client.
			setIdle();
			return;
		}

		if (PreRunScript.instance == null) // Unity object — == (menu still loading)
		{
			return; // stay in JoinPending — the pump retries
		}

		setStarting();
		guestMenu.AuthorizeNextStart(); // the gate refuses unauthorised (manual) guest starts

		// This client is starting a run it did NOT restore (the host's, not a CUO continue of
		// its own): a local character restore queued by a refused or abandoned continue on this
		// machine must never land on a body that belongs to someone else's run. Same rule as
		// RunSaveCoordinator.BeginRun applies to a run started here.
		characterData.CancelOwnRunRestore();
		log.LogInformation("World join received — starting {Run} to follow.", joinIsTutorial ? "the tutorial" : "a run");
		if (joinIsTutorial)
		{
			// StartTutorial sets the tutorial flag and nulls runSettings itself
			// (PreRunScript.cs:307-314).
			PreRunScript.instance.StartTutorial();
		}
		else
		{
			PreRunScript.instance.StartRun();
		}
	}
}
