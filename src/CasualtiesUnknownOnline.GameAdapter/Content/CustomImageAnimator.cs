using UnityEngine;
using UnityEngine.UI;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// GameAdapter-side frame driver for vanilla moodle UI images. It is attached
/// to an <see cref="Image"/> by the moodle row patch when the mod authored a
/// frame animation; the component never crosses Abstractions and is purely a
/// local presentation helper.
/// </summary>
internal sealed class CustomImageAnimator : MonoBehaviour
{
	private Image? _image;
	private Sprite[] _frames = [];
	private float _framesPerSecond;
	private bool _loop;
	private float _time;
	private bool _running;

	internal void SetAnimation(Sprite[] frames, float framesPerSecond, bool loop)
	{
		if (_image == null) // Unity object — ==
		{
			_image = GetComponent<Image>();
		}

		if (_image == null || frames == null || frames.Length == 0) // Unity object — ==
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

		_time += Time.unscaledDeltaTime;
		ApplyFrame(ResolveFrameIndex());
	}

	private void ApplyFrame(int index)
	{
		if (_image == null || _frames.Length == 0) // Unity object — ==
		{
			return;
		}

		var frame = _frames[index];
		if (frame != null) // Unity object — ==
		{
			_image.sprite = frame;
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
