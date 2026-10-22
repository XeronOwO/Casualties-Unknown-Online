using Microsoft.Extensions.Logging;

using UnityTime = UnityEngine.Time;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The player-facing answer a remote-backpack gesture gets when the host's access
/// rule refuses it. Drawn through the game's own alert popup with the game's own
/// red — the native language for "you cannot do that" — and written to the log in
/// the same sentence, so a run reads exactly what the player saw.
///
/// <para>
/// The same sentence is throttled: a while-dragging refusal arrives every frame, and a
/// repeated popup would keep the game's six-second alert alive forever. The window is
/// longer than that alert's own life, so a held gesture shows the line once and lets it
/// fade.
/// </para>
/// </summary>
internal sealed class RemoteBackpackNotice(ILogger log)
{
	private const float RepeatWindowSeconds = 7f;

	private readonly ILogger _log = log;
	private string _lastText = "";
	private float _lastShownAt = float.NegativeInfinity;

	/// <summary>Show one already-localized refusal, at most once per window for the same sentence.</summary>
	internal void Show(string text)
	{
		var now = UnityTime.unscaledTime;
		if (text == _lastText && now - _lastShownAt < RepeatWindowSeconds)
		{
			return;
		}

		_lastText = text;
		_lastShownAt = now;
		_log.LogInformation("[BackpackView] {Reason}", text);
		var camera = PlayerCamera.main;
		if (camera != null) // Unity object — ==
		{
			camera.DoAlert($"<color=red>{text}</color>", important: false);
		}
	}
}
