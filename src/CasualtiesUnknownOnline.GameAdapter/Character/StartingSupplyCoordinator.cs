using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.GameAdapter.WorldGen;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The starting-supplies domain: a player the world has no character for is supplied once
/// per body with the run's <c>startingsupplies</c> setting (S4.3, decision 180).
///
/// It is a coordinator of two things the live game cannot supply itself: the moment a body
/// appears (the pump), and the decision plus the once-per-body rule
/// (<see cref="StartingSupplyPolicy"/>, <see cref="StartingSupplyGrantTracker"/>). The
/// engine-typed acts — creating an item, putting it in a slot — are behind
/// <see cref="IStartingSupplyBehaviour"/> (implemented by
/// <see cref="GameStartingSupplyTarget"/>), so the whole decision path runs in the test
/// host while the Unity half stays in one thin implementation.
///
/// When it acts, and why each edge is chosen:
///
/// - NOT during generation. The game's own first-layer grant runs inside generation
///   (<c>WorldGeneration.WorldPlacePlayer</c>), so a grant of ours in the same window would
///   race the coroutine that is still creating the body's world. The pump's guard is the
///   same <c>IsGenerating</c> edge every other generation-time domain uses.
/// - The body must be the LOCAL body (the run coordinator owns that edge), and the run's
///   baseline must be published: the setting is read from the generation baseline, which is
///   the value this generation was actually built with — the host's, on every client.
/// - A character restore WINS. If a restore is queued for the local body, this player is not
///   new: the world has their character (or is about to hand it over), and the restore's own
///   first pass wipes the slots a frame later, so an extra grant would be created, announced
///   and then destroyed. The queue is the SAME instance the character domain applies from
///   (one queue, two readers), so the two can never disagree, and it is read at the GRANT
///   moment rather than sampled when the body appeared — a restore that lands in between is
///   exactly the event that must cancel the grant.
///
/// Every judged body produces exactly one <see cref="StartingSupplyGrantReport"/> —
/// including the bodies that got nothing — because a mechanism whose silence cannot be told
/// apart from its absence is the failure this stage exists to remove. The one exception is
/// the body that already has a character: that restore is its own reported event (S4.2), and
/// a second line for the same fact would be the noise the account rules exist to remove.
/// </summary>
internal sealed class StartingSupplyCoordinator(
	ISessionControl session,
	IWorldControl world,
	LocalCharacterRestoreQueue restore,
	IWorldSaveControl saves,
	IStartingSupplyPublisher audit,
	IStartingSupplyBehaviour behaviour,
	ILogger<StartingSupplyCoordinator> log)
{
	private readonly ISessionControl _session = session;
	private readonly IWorldControl _world = world;
	private readonly LocalCharacterRestoreQueue _restore = restore;
	private readonly IWorldSaveControl _saves = saves;
	private readonly IStartingSupplyPublisher _audit = audit;
	private readonly IStartingSupplyBehaviour _behaviour = behaviour;
	private readonly ILogger<StartingSupplyCoordinator> _log = log;

	private readonly StartingSupplyGrantTracker _tracker = new();

	/// <summary>
	/// An independent run is taking over: the bodies tracked here belong to a world this
	/// client is leaving, and the next entry is a new-player entry by definition. Called
	/// where every other run-scoped reset happens (<c>RunSaveCoordinator.BeginRun</c>).
	/// </summary>
	internal void Clear() => _tracker.Clear();

	/// <summary>
	/// The pump. One body, one judgement, one account — the judgement is retried on later
	/// frames only while it could not be made at all (a baseline that has not been published
	/// yet), and the first <see cref="StartingSupplyPolicy.Reason"/> that is a verdict is
	/// final for that body.
	/// </summary>
	internal void Update()
	{
		if (_session.Role == SessionRole.Guest && !_session.SessionActive)
		{
			// A guest only ever reaches a world through a session (the run it follows).
			// Without this, a leftover body from a dead session would be judged against a
			// baseline that no longer describes it.
			return;
		}

		var body = _behaviour.LocalBody;
		if (body is null)
		{
			return;
		}

		if (HarmonyTraverse.IsGenerating())
		{
			return; // the world under this body is still being built — see the type remarks
		}

		if (_tracker.WasSupplied(body))
		{
			return; // already judged (or supplied) — a body is a new player at most once
		}

		var baseline = _world.WorldParams;
		var decision = StartingSupplyPolicy.Decide(
			_restore.HasPending || _restore.WipePending,
			baseline,
			StartingSupplyPolicy.SettingOf(baseline),
			_saves.RestoredGeneration);

		switch (decision.Reason)
		{
			case StartingSupplyPolicy.Reason.NoBaseline:
				// Not a verdict: nothing describes this generation yet. The body stays
				// unjudged so a later frame can decide it.
				return;

			case StartingSupplyPolicy.Reason.CharacterRestored:
				// The world has this player's character, and that restore is its own
				// reported event (see the type remarks). The body is marked all the same:
				// re-deciding it every frame while the restore's two passes run would be
				// work whose only possible outcome is the same verdict.
				_tracker.MarkSupplied(body);
				_log.LogInformation(
					"Character restore pending for this body — the world has a character for this player, so no starting supplies are granted (decision 180).");
				return;

			case StartingSupplyPolicy.Reason.AlreadyOwned:
				_tracker.MarkSupplied(body);
				Report(new StartingSupplyGrantReport(
					StartingSupplyGrantReport.Disposition.AlreadyOwned, decision.Setting, [], []));
				return;

			case StartingSupplyPolicy.Reason.Disabled:
				_tracker.MarkSupplied(body);
				Report(new StartingSupplyGrantReport(
					StartingSupplyGrantReport.Disposition.Disabled, decision.Setting, [], []));
				return;

			default:
				Grant(body, decision);
				return;
		}
	}

	/// <summary>
	/// Create and place the decided plan, then account for the result. The body is marked
	/// supplied BEFORE the items are created: a creation that throws must not leave the body
	/// retryable, or the next frame would hand the player a second set of the items that did
	/// land.
	/// </summary>
	private void Grant(object body, StartingSupplyPolicy.Decision decision)
	{
		_tracker.MarkSupplied(body);

		var placed = new List<string>(decision.Plan.Count);
		var unplaced = new List<string>(decision.Plan.Count);
		foreach (var entry in decision.Plan)
		{
			var item = _behaviour.Create(entry.ItemId);
			if (item is null || !_behaviour.TryPlace(body, item, entry.Slot))
			{
				unplaced.Add(entry.ItemId);
				continue;
			}

			placed.Add(entry.ItemId);
		}

		Report(new StartingSupplyGrantReport(
			StartingSupplyGrantReport.Disposition.Granted, decision.Setting, placed, unplaced));
	}

	/// <summary>
	/// One account, two surfaces (decision 179): the log line always, and the Runtime's
	/// report for the console. The report travels through
	/// <see cref="IStartingSupplyPublisher"/> rather than being printed here because the
	/// adapter owns no player-facing surface — the Runtime does.
	/// </summary>
	private void Report(StartingSupplyGrantReport report)
	{
		if (report.Outcome == StartingSupplyGrantReport.Disposition.Granted && !report.Complete)
		{
			_log.LogWarning("Starting supplies for a new player: {Account}", report.Describe());
		}
		else
		{
			_log.LogInformation("Starting supplies for a new player: {Account}", report.Describe());
		}

		_audit.Publish(report);
	}
}
