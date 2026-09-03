using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// Small GameAdapter-side frame driver for custom item sprite renderers. It is
/// attached to a <see cref="SpriteRenderer"/> by <see cref="CustomItemVisualState"/>
/// when the mod authored a frame animation; the component never crosses
/// Abstractions and is purely a local presentation helper.
/// </summary>
internal sealed class CustomSpriteAnimator : MonoBehaviour
{
	private SpriteRenderer? _renderer;
	private Sprite[] _frames = [];
	private float _framesPerSecond;
	private bool _loop;
	private float _time;
	private bool _running;

	internal void SetAnimation(Sprite[] frames, float framesPerSecond, bool loop)
	{
		if (_renderer == null) // Unity object — ==
		{
			_renderer = GetComponent<SpriteRenderer>();
		}

		if (_renderer == null || frames == null || frames.Length == 0) // Unity object — ==
		{
			StopAnimation();
			return;
		}

		_frames = frames;
		_framesPerSecond = framesPerSecond > 0f ? framesPerSecond : 0f;
		_loop = loop;
		_time = 0f;
		_running = true;
		ApplyFrame(0);
	}

	internal void StopAnimation()
	{
		_running = false;
		_frames = [];
	}

	private void Update()
	{
		if (!_running || _frames.Length <= 1 || _framesPerSecond <= 0f)
		{
			return;
		}

		_time += Time.deltaTime;
		ApplyFrame(ResolveFrameIndex());
	}

	private void ApplyFrame(int index)
	{
		if (_renderer == null || _frames.Length == 0) // Unity object — ==
		{
			return;
		}

		var frame = _frames[index];
		if (frame != null) // Unity object — ==
		{
			_renderer.sprite = frame;
		}
	}

	private int ResolveFrameIndex()
	{
		if (_frames.Length <= 1 || _framesPerSecond <= 0f)
		{
			return 0;
		}

		var frameIndex = Mathf.FloorToInt(_time * _framesPerSecond);
		if (_loop)
		{
			frameIndex %= _frames.Length;
		}
		else
		{
			frameIndex = Mathf.Min(frameIndex, _frames.Length - 1);
		}

		return Mathf.Clamp(frameIndex, 0, _frames.Length - 1);
	}
}
