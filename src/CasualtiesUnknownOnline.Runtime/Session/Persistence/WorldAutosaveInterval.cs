using System;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// When the interval autosave is due. The clock is the world's WRITE history, not a
/// stopwatch: the interval restarts whenever a cut REACHES the writer — a committed cut of
/// any trigger, or an attempt the writer refused — so a player who saves by hand, crosses a
/// layer or deliberately leaves the world does not get an extra archive a minute later, and a
/// world that CANNOT be written (a read-only folder, a vanished drive) is retried once per
/// interval instead of once per frame.
///
/// The window is opened by entering a world (<see cref="Restart"/>), never by the
/// process: a host that spends twenty minutes in the main menu and then starts a run
/// gets its first autosave one interval later, not on the run's first frame.
/// </summary>
internal sealed class WorldAutosaveInterval
{
	private DateTime? _lastWriteUtc;

	/// <summary>True = a world is being written right now: without one the interval is never due.</summary>
	internal bool Armed => _lastWriteUtc is not null;

	/// <summary>
	/// True = the interval has elapsed since the last committed cut of this world.
	/// A disabled policy is never due, whatever the window says.
	/// </summary>
	internal bool IsDue(DateTime nowUtc, bool enabled, TimeSpan interval) =>
		enabled
		&& interval > TimeSpan.Zero
		&& _lastWriteUtc is { } last
		&& nowUtc - last >= interval;

	/// <summary>A world was entered at <paramref name="utc"/>: this is the instant the first interval counts from.</summary>
	internal void Restart(DateTime utc) => _lastWriteUtc = utc;

	/// <summary>The world was written at <paramref name="utc"/>: the next interval starts here.</summary>
	internal void NoteCutTaken(DateTime utc) => _lastWriteUtc = utc;

	/// <summary>No world is owned (a tutorial entry, a new run that could not be created): the interval stands down.</summary>
	internal void StandDown() => _lastWriteUtc = null;
}
