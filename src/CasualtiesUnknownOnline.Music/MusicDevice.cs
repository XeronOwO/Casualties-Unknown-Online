using System;
using System.IO;
using CasualtiesUnknownOnline.BulkTransfer.Core.Audio;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace CasualtiesUnknownOnline.Music;

/// <summary>
/// The local playback device: one hidden GameObject carrying an AudioSource, and the clip the WAV decode
/// produced. It owns only the engine side — what to play and when is the caller's decision — so the sharing
/// stage adds the transfer and the shared start instant without touching this.
/// </summary>
internal sealed class MusicDevice : IDisposable
{
	private GameObject? _host;
	private AudioSource? _source;
	private AudioClip? _clip;

	/// <summary>Decode a WAV file from disk and play it. Returns null on success, or the reason it did not play.</summary>
	internal string? PlayFile(string path, float volume = 1f)
	{
		if (!File.Exists(path))
		{
			return $"no file at {path}";
		}

		byte[] bytes;
		try
		{
			bytes = File.ReadAllBytes(path);
		}
		catch (Exception exception)
		{
			return $"the file could not be read: {exception.GetType().Name}: {exception.Message}";
		}

		return PlayBytes(Path.GetFileName(path), bytes, volume);
	}

	/// <summary>
	/// Decode WAV bytes and play them from <paramref name="offsetSeconds"/> (a shared start that has already
	/// passed starts where the group is, not at zero). Returns null on success, or the reason it did not play.
	/// </summary>
	internal string? PlayBytes(string name, byte[] bytes, float volume = 1f, double offsetSeconds = 0)
	{
		var wav = WavFile.Decode(bytes);
		if (!wav.Decoded)
		{
			return $"not playable: {wav.Refusal}";
		}

		if (offsetSeconds >= wav.Seconds)
		{
			return $"the track already ended (the group is at {offsetSeconds:0.0}s of {wav.Seconds:0.0}s)";
		}

		EnsureHost();
		Stop();
		_clip = AudioClip.Create(name, wav.Samples.Length / wav.Channels, wav.Channels, wav.SampleRate, stream: false);
		if (!_clip.SetData(wav.Samples, 0))
		{
			return "the clip refused its samples";
		}

		_source!.clip = _clip;
		_source.volume = Mathf.Clamp01(volume);
		_source.Play();
		if (offsetSeconds > 0.01d && offsetSeconds < wav.Seconds)
		{
			_source.time = (float)offsetSeconds;
		}

		Report(name, wav, offsetSeconds);
		return null;
	}

	/// <summary>True while a clip is playing.</summary>
	internal bool Playing => _source != null && _source.isPlaying;

	/// <summary>The playing clip's position in seconds (0 when nothing plays).</summary>
	internal float Position => _source != null && _source.clip != null ? _source.time : 0f;

	/// <summary>The playing clip's length in seconds (0 when nothing plays).</summary>
	internal float Length => _source != null && _source.clip != null ? _source.clip.length : 0f;

	/// <summary>Stop and release the clip.</summary>
	internal void Stop()
	{
		if (_source != null)
		{
			_source.Stop();
			_source.clip = null;
		}

		if (_clip != null)
		{
			UnityObject.Destroy(_clip);
			_clip = null;
		}
	}

	private void EnsureHost()
	{
		if (_host != null)
		{
			return;
		}

		_host = new GameObject("CUO Music");
		UnityObject.DontDestroyOnLoad(_host);
		_source = _host.AddComponent<AudioSource>();
		_source.playOnAwake = false;
		_source.spatialBlend = 0f;
	}

	private static void Report(string name, WavFile wav, double offsetSeconds) =>
		MusicGate.Log?.LogInformation(
			offsetSeconds > 0.01d
				? $"[Music] playing {name} from {offsetSeconds:0.00}s: {wav.Seconds:0.0}s at {wav.SampleRate} Hz, {wav.Channels} channel(s)."
				: $"[Music] playing {name}: {wav.Seconds:0.0}s at {wav.SampleRate} Hz, {wav.Channels} channel(s).");

	/// <inheritdoc />
	public void Dispose()
	{
		Stop();
		if (_host != null)
		{
			UnityObject.Destroy(_host);
			_host = null;
		}
	}
}
