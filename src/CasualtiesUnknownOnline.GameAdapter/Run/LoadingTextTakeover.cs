namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// The decision half of the native loading-text takeover, without any Unity dependency: WHETHER the session's
/// wait line owns the game's own loading text, and WHICH text must be handed back when it stops owning it.
///
/// <para>
/// The rule is "capture once, restore once": the first write remembers what the game had put there (its last
/// generation phase), later writes on the same takeover leave that memory alone, and the release puts the
/// remembered text back. A release with nothing captured is a no-op, so a gate that never got a surface cannot
/// overwrite the game's text on its way out.
/// </para>
///
/// <para>
/// This split exists because the Unity side of the takeover is five lines of field access, while the rule that
/// decides what the player's screen returns to is the part that can be wrong — and that part is a case here.
/// </para>
/// </summary>
internal sealed class LoadingTextTakeover
{
	private string? _captured;

	/// <summary>True while the session's wait line owns the game's loading text.</summary>
	internal bool IsActive { get; private set; }

	/// <summary>The text the game had before the takeover, or null when nothing was captured.</summary>
	internal string? Captured => _captured;

	/// <summary>
	/// Take the line over, answering the text that must be remembered: the caller passes what the game's field
	/// holds right now, and only the FIRST call while inactive captures it. A second takeover while the line is
	/// already ours keeps the original capture, so a wait line that changes every second (the countdown) does not
	/// lose the game's own phase.
	/// </summary>
	internal string? Take(string gameText)
	{
		if (!IsActive)
		{
			_captured = gameText;
			IsActive = true;
		}

		return _captured;
	}

	/// <summary>
	/// Hand the line back, answering the text to restore (null = nothing was captured, so the caller writes
	/// nothing). Clears the takeover so the next wait captures afresh — the game may have written a new phase in
	/// between.
	/// </summary>
	internal string? Release()
	{
		if (!IsActive)
		{
			return null;
		}

		var restored = _captured;
		_captured = null;
		IsActive = false;
		return restored;
	}
}
