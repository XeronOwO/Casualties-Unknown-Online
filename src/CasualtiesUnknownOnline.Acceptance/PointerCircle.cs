using System.Globalization;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Acceptance;

/// <summary>
/// Walks the staged pointer around a circle, one step per frame, from inside the client.
///
/// <para>
/// The bandage minigame accumulates from the hand's ANGLE around the roll, not from its speed:
/// <c>BandageMinigame.PhysicsUpdate</c> only advances <c>bandageAngle</c> while <c>holdingBandage</c>, and
/// <c>holdingBandage</c> is true when the hand's direction is within about 25 degrees of the roll
/// (<c>Vector2.Dot(GetVectorFromAngle(bandageAngle), handPos.normalized) &gt; 0.9</c>); <c>didWrap</c> is set when the
/// angle passes 360. A straight shake (what <see cref="PointerShake"/> does for the amputation) never holds that
/// direction, and driving it from the batch script costs one websocket round trip per position, so the hand settles at
/// each point and the angle never advances (measured 2026-11-03: the remote bandage minigame started and the limb's skin
/// did not move at all). This component moves the pointer a fixed number of degrees per frame, which is the gesture the
/// minigame is written for.
/// </para>
///
/// <para>
/// <c>press=1</c> makes the component own the attack input too: it goes down on the frame the hand passes the roll's own
/// start (0 degrees) and stays down while the wrap turns, then comes up a little past a full turn. The batch used to do
/// that from outside, one round trip per candidate frame, and the row's own budget was sixty seconds of polling because
/// the press had to land inside a window the script could only sample every forty milliseconds (measured 2026-11-22:
/// `medical-bandage` 66.3 s of case time for a gesture a player makes in about two). Pressing here is the same gesture on
/// the same frames, without the trip. The press stages BOTH the mouse button and the attack key: `holdingBandage` is
/// stamped from the frame the game sees the click START, and a press that only satisfied one of the two queries left the
/// hand unheld (measured 2026-11-22: `handStarted false` while the key was staged down).
/// </para>
/// </summary>
internal sealed class PointerCircle : MonoBehaviour
{
	private static PointerCircle? _running;

	private Vector2 _centre;

	private float _radius;

	private float _degreesPerFrame;

	private float _angle;

	private int _framesLeft;

	private bool _press;

	private bool _pressed;

	private float _pressedAt;

	/// <summary>Starts the circle: centre and radius in the game's pointer space (bottom-left origin), degrees per frame.</summary>
	internal static string Start(float centreX, float centreY, float radius, float degreesPerFrame, int frames, bool press)
	{
		Stop();
		var host = new GameObject("CUO acceptance pointer circle");
		Object.DontDestroyOnLoad(host);
		var circle = host.AddComponent<PointerCircle>();
		circle._centre = new Vector2(centreX, centreY);
		circle._radius = radius;
		circle._degreesPerFrame = degreesPerFrame;
		circle._angle = 0f;
		circle._framesLeft = Mathf.Max(1, frames);
		circle._press = press;
		circle.Step();
		_running = circle;
		return "{\"ok\":true,\"running\":true,\"centre\":[" + centreX.ToString("0.#", CultureInfo.InvariantCulture)
			+ "," + centreY.ToString("0.#", CultureInfo.InvariantCulture) + "],\"radius\":"
			+ radius.ToString("0.#", CultureInfo.InvariantCulture) + ",\"degreesPerFrame\":"
			+ degreesPerFrame.ToString("0.##", CultureInfo.InvariantCulture) + ",\"frames\":" + frames
			+ ",\"press\":" + (press ? "true" : "false") + "}";
	}

	/// <summary>Stops the circle and leaves the pointer wherever it last was.</summary>
	internal static string Stop()
	{
		if (_running == null)
		{
			return "{\"ok\":true,\"running\":false}";
		}

		if (_running._pressed)
		{
			VirtualInput.Release(0);
			VirtualInput.ReleaseKey(KeyCode.Mouse0);
			_running._pressed = false;
		}

		Object.Destroy(_running.gameObject);
		_running = null;
		return "{\"ok\":true,\"running\":false}";
	}

	/// <summary>What the circle has drawn so far: the frames left and the angle it reached.</summary>
	internal static string Report()
	{
		if (_running == null)
		{
			return "{\"ok\":true,\"running\":false}";
		}

		return "{\"ok\":true,\"running\":true,\"angle\":" + _running._angle.ToString("0.#", CultureInfo.InvariantCulture)
			+ ",\"framesLeft\":" + _running._framesLeft
			+ ",\"pressed\":" + (_running._pressed ? "true" : "false")
			+ ",\"pressedAt\":" + _running._pressedAt.ToString("0.#", CultureInfo.InvariantCulture)
			+ ",\"x\":" + _running.LastX.ToString("0.#", CultureInfo.InvariantCulture)
			+ ",\"y\":" + _running.LastY.ToString("0.#", CultureInfo.InvariantCulture) + "}";
	}

	private float LastX { get; set; }

	private float LastY { get; set; }

	private void Update()
	{
		if (_framesLeft <= 0)
		{
			Stop();
			return;
		}

		Step();
	}

	private void Step()
	{
		_angle += _degreesPerFrame;
		var radians = _angle * Mathf.Deg2Rad;
		LastX = _centre.x + Mathf.Cos(radians) * _radius;
		LastY = _centre.y + Mathf.Sin(radians) * _radius;
		VirtualInput.MoveTo(LastX, LastY);
		PressOnPass();
		_framesLeft--;
	}

	/// <summary>
	/// The attack input's window is the roll's own start: the game stamps <c>holdingBandage</c> only on the frame the
	/// click starts while the hand's direction is within about 25 degrees of the roll (0 degrees here), so the press goes
	/// down on the pass and stays down until the wrap has turned past a full circle.
	/// </summary>
	private void PressOnPass()
	{
		if (!_press)
		{
			return;
		}

		var wrapped = _angle % 360f;
		if (!_pressed)
		{
			if (wrapped <= 30f || wrapped >= 330f)
			{
				VirtualInput.Press(0);
				VirtualInput.PressKey(KeyCode.Mouse0);
				_pressed = true;
				_pressedAt = _angle;
			}

			return;
		}

		// A full turn plus the window it started from: the game sets `didWrap` at 360 and stops counting afterwards.
		if (_angle - _pressedAt >= 380f)
		{
			VirtualInput.Release(0);
			VirtualInput.ReleaseKey(KeyCode.Mouse0);
			_pressed = false;
		}
	}
}
