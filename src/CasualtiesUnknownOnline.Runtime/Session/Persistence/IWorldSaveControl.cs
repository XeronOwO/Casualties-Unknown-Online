using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The save system's narrow surface for the Game Adapter and packet handlers —
/// the Runtime owns the repository, the format and the kernel, the adapter only
/// reports the two moments the game itself knows about (a run starts, the host
/// leaves a world) and asks whether a CUO world can be continued.
///
/// The world is not saved by the adapter: the layer-end cut is taken by the
/// Runtime when the kernel commits the layer advance, so a save can never run
/// from a half-applied batch, and every transport role is decided in one place.
/// </summary>
public interface IWorldSaveControl
{
	/// <summary>False when this composition root has no save repository (tests, or a build that opted out).</summary>
	bool IsEnabled { get; }

	/// <summary>
	/// Host: the host clicked start — the run this session plays gets its own
	/// world folder. False = the world could not be created (the run still plays;
	/// it just cannot be saved).
	/// </summary>
	bool TryBeginRun();

	/// <summary>
	/// Host: take one cut now — the deliberate "save and return to menu" path.
	/// <paramref name="hostCharacter"/> is the host's character captured from the
	/// live body at this instant (null = fall back to the last reported snapshot).
	/// False = nothing was written; the reason is logged and returned.
	/// </summary>
	bool TryCaptureMenuReturnCut(CharacterDataMsg? hostCharacter);

	/// <summary>The world this run writes into ("" before a run started); the Runtime owns it, the adapter only reports it in logs.</summary>
	string CurrentWorldId { get; }

	/// <summary>The world the Continue entry would open (null when none is openable); the adapter logs it and the tests pin the rule.</summary>
	string? ContinueWorldId { get; }

	/// <summary>True = the repository holds at least one world the Continue entry can open.</summary>
	bool HasRestorableWorld { get; }

	/// <summary>
	/// Host: the Continue entry was used. Loads the world the repository resolves
	/// as "the selected one", applies the kernel checkpoint and the host's own
	/// character, and returns whether the run may start. A refusal is never a
	/// silent fallback to the native regenerate path.
	/// </summary>
	bool TryContinue(out WorldContinueOutcome outcome);

	/// <summary>The characters of the last continuation, by player key — the raw material of S4's guest claims.</summary>
	IReadOnlyList<SavedCharacter> PendingCharacters { get; }
}
