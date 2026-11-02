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
/// </summary>
internal sealed class PointerCircle : MonoBehaviour
{
	private static PointerCircle? _running;

	private Vector2 _centre;

	private float _radius;

	private float _degreesPerFrame;

	private float _angle;

	private int _framesLeft;

	/// <summary>Starts the circle: centre and radius in the game's pointer space (bottom-left origin), degrees per frame.</summary>
	internal static string Start(float centreX, float centreY, float radius, float degreesPerFrame, int frames)
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
		circle.Step();
		_running = circle;
		return "{\"ok\":true,\"running\":true,\"centre\":[" + centreX.ToString("0.#", CultureInfo.InvariantCulture)
			+ "," + centreY.ToString("0.#", CultureInfo.InvariantCulture) + "],\"radius\":"
			+ radius.ToString("0.#", CultureInfo.InvariantCulture) + ",\"degreesPerFrame\":"
			+ degreesPerFrame.ToString("0.##", CultureInfo.InvariantCulture) + ",\"frames\":" + frames + "}";
	}

	/// <summary>Stops the circle and leaves the pointer wherever it last was.</summary>
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

	/// <summary>What the circle has drawn so far: the frames left and the angle it reached.</summary>
	internal static string Report()
	{
		if (_running == null)
		{
			return "{\"ok\":true,\"running\":false}";
		}

		return "{\"ok\":true,\"running\":true,\"angle\":" + _running._angle.ToString("0.#", CultureInfo.InvariantCulture)
			+ ",\"framesLeft\":" + _running._framesLeft
			+ ",\"x\":" + _running.LastX.ToString("0.#", CultureInfo.InvariantCulture)
			+ ",\"y\":" + _running.LastY.ToString("0.#", CultureInfo.InvariantCulture) + "}";
	}

	private float LastX { get; set; }

	private float LastY { get; set; }

	private void Update()
	{
		if (_framesLeft <= 0)
		{
			Object.Destroy(gameObject);
			_running = null;
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
		_framesLeft--;
	}
}
