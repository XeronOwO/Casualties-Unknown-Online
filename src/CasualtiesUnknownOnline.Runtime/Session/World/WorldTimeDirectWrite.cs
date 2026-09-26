using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The game's own DIRECT <c>Time.timeScale</c> writes: a field write, so there is
/// no <c>PlayerCamera.SetTimeScale</c> call for <see cref="WorldTimeScaleCall"/>
/// to classify and no native flag to read — the pump can only read the live clock
/// back and decide what the drift means. Four writers exist (censused in this
/// cycle's self-check): the earthquake start (<c>WorldGeneration.cs:870</c>, the
/// host's alone — a guest's own quake timer is frozen by
/// <c>WorldGenerationUpdatePatch</c>), the console's <c>timescale</c> command
/// (<c>ConsoleScript.cs:815</c>), a scene reload (<c>WorldGeneration.cs:1036</c>)
/// and the run start (<c>PreRunScript.cs:64</c>); the last two write while the
/// pump owns no world (the main menu, and a reload the start gate covers), so
/// <c>WorldTimeSync.Update</c> returns before this rule is consulted.
///
/// The owner ruled on 2026-09-26 that vanilla stands here: the game resets the
/// clock at an earthquake and CUO does not suppress that — the host ADOPTS the
/// world's own write as the shared speed instead. <see cref="Verdict"/> therefore
/// has no suppression member on purpose, and the adoption is the recorded
/// behaviour rather than a defect to repair. The two sides differ only in who owns
/// the clock: the host's live clock IS the shared clock, so its drift becomes the
/// world's new speed; a guest's is not, so the same drift is put back. Pure: no
/// Unity, no clock, no session.
/// </summary>
public static class WorldTimeDirectWrite
{
	/// <summary>What this side must do about the live clock having moved without a routed call.</summary>
	public enum Verdict
	{
		/// <summary>Nothing to do: the live value is not a domain speed (the mapping decides what one is — see <see cref="WorldTimeSpeedScale.FromTimeScale"/>) or already is the applied speed.</summary>
		None,

		/// <summary>Host: the world's own writer owns the shared clock — adopt the value as the standing request and broadcast it (vanilla: an earthquake's reset is never suppressed).</summary>
		Adopt,

		/// <summary>Guest: the shared clock is the host's — this screen is put back on the applied speed.</summary>
		Restore,
	}

	/// <summary>
	/// The verdict for one observed drift. The VALUE alone cannot say who wrote it —
	/// the quake start and the console's command write the same numbers — so the
	/// rule is deliberately writer-blind: every domain-speed drift is the world's
	/// own write, and the side decides who owns the clock. <paramref name="liveClock"/>
	/// is the live <c>Time.timeScale</c> already mapped by
	/// <see cref="WorldTimeSpeedScale.FromTimeScale"/>, which answers null for
	/// everything that is not a domain speed.
	/// </summary>
	public static Verdict Classify(WorldTimeSpeed? liveClock, WorldTimeSpeed appliedSpeed, bool isHost)
	{
		if (liveClock is null || liveClock == appliedSpeed)
		{
			return Verdict.None;
		}

		return isHost ? Verdict.Adopt : Verdict.Restore;
	}
}
