using System.Globalization;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Acceptance;

/// <summary>
/// Shakes the staged pointer between two positions ON CONSECUTIVE FRAMES, which is what a minigame that accumulates from the hand's
/// FRAME-TO-FRAME velocity needs.
///
/// <para>
/// Driving such a minigame from the batch script does not work: every staged position costs one websocket round trip (tens of
/// frames), so the game's hand settles at each position and the velocity it reads stays near zero. Measured 2026-11-02 on the
/// amputation minigame: hundreds of scripted sweeps took the limb's skin and muscle to zero and left <c>cutProgress</c> (which
/// accumulates <c>|handVelocity.x| * 5E-05</c> per physics frame) nowhere near ending. This component does the same movement from
/// inside the client, one toggle per frame, and the game sees the velocity a held mouse would produce.
/// </para>
/// </summary>
internal sealed class PointerShake : MonoBehaviour
{
	private static PointerShake? _running;

	private Vector2 _first;

	private Vector2 _second;

	private int _framesPerSide;

	private int _frames;

	private bool _atFirst;

	private float _driftPerToggle;

	/// <summary>Starts shaking between two points in the game's own pointer space (bottom-left origin).</summary>
	/// <param name="driftPerToggle">
	/// How far the whole shake slides DOWN the screen on every toggle. The amputation minigame needs it: its cutting band is
	/// <c>handPos.y &lt;= maxHandPos + 10</c> and <c>maxHandPos</c> is <c>Lerp(120, -380, cutProgress)</c> (AmputationMinigame.cs:36-50), so the
	/// hand has to follow the cut downwards or the band passes above it and the progress stalls (measured 2026-11-02: the hand sat at
	/// -163 while the band had fallen to -175, and cutProgress stuck at 0.59).
	/// </param>
	internal static string Start(float firstX, float firstY, float secondX, float secondY, int framesPerSide, float driftPerToggle)
	{
		Stop();
		var host = new GameObject("CUO acceptance pointer shake");
		Object.DontDestroyOnLoad(host);
		var shake = host.AddComponent<PointerShake>();
		shake._first = new Vector2(firstX, firstY);
		shake._second = new Vector2(secondX, secondY);
		shake._framesPerSide = Mathf.Max(1, framesPerSide);
		shake._driftPerToggle = driftPerToggle;
		shake._atFirst = true;
		VirtualInput.MoveTo(firstX, firstY);
		_running = shake;
		return "{\"ok\":true,\"running\":true,\"first\":[" + firstX.ToString("0.#", CultureInfo.InvariantCulture)
			+ "," + firstY.ToString("0.#", CultureInfo.InvariantCulture) + "],\"second\":[" + secondX.ToString("0.#", CultureInfo.InvariantCulture)
			+ "," + secondY.ToString("0.#", CultureInfo.InvariantCulture) + "],\"framesPerSide\":" + framesPerSide
			+ ",\"drift\":" + driftPerToggle.ToString("0.#", CultureInfo.InvariantCulture) + "}";
	}

	/// <summary>Stops the shake and leaves the pointer wherever it last was.</summary>
	internal static string Stop()
	{
		if (_running == null)
		{
			return "{\"ok\":true,\"running\":false}";
		}

		Object.Destroy(_running.gameObject);
		_running = null;
		return "{\"ok\":true,\"running\":false}";
	}

	private void Update()
	{
		_frames++;
		if (_frames < _framesPerSide)
		{
			return;
		}

		_frames = 0;
		_atFirst = !_atFirst;
		if (_driftPerToggle != 0f)
		{
			_first.y -= _driftPerToggle;
			_second.y -= _driftPerToggle;
		}

		var target = _atFirst ? _first : _second;
		VirtualInput.MoveTo(target.x, target.y);
	}
}
