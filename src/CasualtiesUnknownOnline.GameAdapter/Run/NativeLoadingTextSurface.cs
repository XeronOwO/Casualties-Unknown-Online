using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// The Unity half of the wait line's takeover: writes the session's wait text into the game's OWN loading-screen
/// text (<c>WorldGeneration.loadingText</c>, the bottom-right line the player already reads during generation) and
/// hands it back when the wait ends.
///
/// <para>
/// Why this surface: the loading screen the game shows during a wait is the player's whole view, and its own line
/// is where they look. CUO drew a panel over that line instead, which read as CUO's text on top of a stale phase.
/// The rule that decides capture and hand-back lives in <see cref="LoadingTextTakeover"/>; this class only finds
/// the field and writes it.
/// </para>
///
/// <para>
/// Nothing here is fatal: without a world or without the field (the menu, a build that renamed it) every call
/// answers false and the caller keeps its own overlay, so the wait is still described.
/// </para>
/// </summary>
internal sealed class NativeLoadingTextSurface(ILogger<NativeLoadingTextSurface> log)
{
	private readonly ILogger<NativeLoadingTextSurface> _log = log;
	private readonly LoadingTextTakeover _takeover = new();

	/// <summary>True while this surface is carrying the session's wait line.</summary>
	internal bool IsActive => _takeover.IsActive;

	/// <summary>
	/// Show <paramref name="line"/> on the game's loading text, answering whether the surface took it. The first
	/// write of a takeover remembers the game's own text; later writes replace only the visible line.
	/// </summary>
	internal bool TryShow(string line)
	{
		if (WorldGeneration.world == null) // Unity object — the menu has no world, so no loading text
		{
			return false;
		}

		var field = WorldGeneration.world.loadingText;
		if (field == null) // Unity object — a build without the surface
		{
			return false;
		}

		var first = !_takeover.IsActive;
		var captured = _takeover.Take(field.text ?? string.Empty);
		field.text = line;
		if (first)
		{
			_log.LogInformation("[Gate] the wait line now owns the game's loading text; the game's own text ({Captured}) is kept for the hand-back.", captured);
		}

		return true;
	}

	/// <summary>
	/// Hand the loading text back to the game: restore the text the takeover captured, once. A no-op when this
	/// surface never took it, so a released gate cannot overwrite the game's line.
	/// </summary>
	internal void Release()
	{
		var restored = _takeover.Release();
		if (restored is null)
		{
			return;
		}

		if (WorldGeneration.world == null) // Unity object — the scene left; the field died with it
		{
			return;
		}

		var field = WorldGeneration.world.loadingText;
		if (field == null) // Unity object
		{
			return;
		}

		field.text = restored;
		_log.LogInformation("[Gate] the loading text is handed back to the game ({Restored}).", restored);
	}
}
