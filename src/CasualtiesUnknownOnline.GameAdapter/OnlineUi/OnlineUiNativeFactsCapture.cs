using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using TMPro;
using UnityEngine.UI;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// The adapter's implementation of <see cref="IOnlineUiNativeFactsQuery"/> — S1's read-only probe. One
/// call finds the game's canvas, materializes the CUO host and the game's own settings rows under it,
/// reads the four unknowns and hands back a plain <see cref="OnlineUiNativeFacts"/>; the Runtime folds,
/// formats and tests that value, this class only reads.
///
/// <para>
/// The host is kept between attempts, because two of the facts appear at different moments: the canvas
/// and the rows exist from the menu on, while <c>PlayerCamera.uiScale</c> needs a rendered game. It is
/// disposed the moment a complete reading lands — the reading IS the result, and CUO leaves no scene
/// object behind — and again when the adapter is disposed. Every path returns a reading, including a
/// failure: this runs inside a frame callback and must not throw at it.
/// </para>
/// </summary>
internal sealed class OnlineUiNativeFactsCapture : IOnlineUiNativeFactsQuery, IDisposable
{
	private OnlineUiNativeSurfaceHost? _host;
	private OnlineUiNativeFacts _reading = OnlineUiNativeFacts.Unavailable("the probe has not run yet");
	private bool _finished;

	/// <summary>The tree the sweep names as the source of the chrome styles it found.</summary>
	internal const string CanvasSource = "game canvas";

	public OnlineUiNativeFacts Capture()
	{
		if (_finished)
		{
			return _reading;
		}

		try
		{
			_reading = Probe();
		}
		catch (Exception exception)
		{
			// A game update that changed a prefab's shape must show up as a reading, never as an
			// exception out of a Unity frame callback.
			Dispose();
			_reading = OnlineUiNativeFacts.Unavailable($"the probe threw {exception.GetType().Name}: {exception.Message}");
		}

		return _reading;
	}

	private OnlineUiNativeFacts Probe()
	{
		var parent = OnlineUiNativeSurfaceHost.FindMainCanvasTransform();
		if (parent == null)
		{
			return OnlineUiNativeFacts.Unavailable("the game has no main canvas yet");
		}

		// A scene change destroys CUO's canvas with the game's (Unity takes the children with the
		// parent), and the cached rows are destroyed objects from that moment on: rebuild the host
		// instead of dereferencing them. Unity object — ==.
		if (_host != null && _host.RootTransform == null)
		{
			Dispose();
		}

		_host ??= OnlineUiNativeSurfaceHost.TryCreate();
		if (_host == null)
		{
			return OnlineUiNativeFacts.Unavailable("CUO's canvas could not be created under the game's canvas");
		}

		var (rowStyles, rowText, note) = ReadRows(_host);

		var images = new List<OnlineUiNativeImageStyle>();
		var texts = new List<OnlineUiNativeTextStyle>();
		if (OnlineUiNativeStyleReader.Sweep(parent, _host.RootTransform, CanvasSource, images, texts))
		{
			note ??= $"the canvas sweep stopped at {OnlineUiNativeStyleReader.MaxCandidates} objects";
		}

		var reading = new OnlineUiNativeFacts(
			OnlineUiNativeSurfaceHost.PathOf(parent),
			OnlineUiNativeStyleCensus.CollapseImageStyles(rowStyles),
			OnlineUiNativeStyleCensus.CollapseImageStyles(images),
			rowText ?? FirstLiveText(texts),
			ReadUiScale(),
			note);

		if (reading.IsComplete)
		{
			_finished = true;
			Dispose();
		}

		return reading;
	}

	/// <summary>
	/// The game's rows, read exactly where the game puts them: child 0 is the label and child 1 the
	/// control (the game's own settings screen builds them that way). A row whose shape moved is
	/// reported through <c>Note</c> instead of guessed at.
	/// </summary>
	private static (List<OnlineUiNativeImageStyle> Styles, OnlineUiNativeTextStyle? Text, string? Note) ReadRows(
		OnlineUiNativeSurfaceHost host)
	{
		var styles = new List<OnlineUiNativeImageStyle>();
		OnlineUiNativeTextStyle? text = null;
		string? note = null;

		if (host.Rows.Count < OnlineUiNativeSurfaceHost.RowPrefabPaths.Length)
		{
			note = $"only {host.Rows.Count} of the game's {OnlineUiNativeSurfaceHost.RowPrefabPaths.Length} settings rows loaded";
		}

		foreach (var row in host.Rows)
		{
			var source = row.name;
			var transform = row.transform;
			if (transform.childCount < 2)
			{
				note ??= $"row {source} has {transform.childCount} children — the game's settings row layout moved";
				continue;
			}

			var control = OnlineUiNativeStyleReader.TryReadImage(transform.GetChild(1).GetComponent<Image>(), source)
				?? OnlineUiNativeStyleReader.TryReadImage(row.GetComponent<Image>(), source);
			if (control is null)
			{
				note ??= $"row {source} carries no Image on its control child";
			}
			else
			{
				styles.Add(control);
			}

			text ??= OnlineUiNativeStyleReader.TryReadText(transform.GetChild(0).GetComponent<TMP_Text>(), source);
		}

		return (styles, text, note);
	}

	/// <summary>The most common text style on the live canvas, as the font fallback when the game's
	/// settings rows carry none.</summary>
	private static OnlineUiNativeTextStyle? FirstLiveText(IEnumerable<OnlineUiNativeTextStyle> texts)
	{
		foreach (var text in OnlineUiNativeStyleCensus.CollapseTextStyles(texts, cap: 1))
		{
			return text;
		}

		return null;
	}

	/// <summary>
	/// <c>PlayerCamera.uiScale</c> is the game's own canvas scale, and it is the fact that needs a
	/// rendered game — so a menu-time attempt reports it as missing and the retry policy keeps asking.
	/// </summary>
	private static float? ReadUiScale()
	{
		// Unity objects — ==
		if (PlayerCamera.main == null || PlayerCamera.main.mainCanvas == null)
		{
			return null;
		}

		return PlayerCamera.uiScale;
	}

	public void Dispose()
	{
		_host?.Dispose();
		_host = null;
	}
}
