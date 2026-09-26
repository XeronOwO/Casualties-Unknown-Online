using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// Reads the game's own UI styles as plain values: an <c>Image</c>'s sprite, slice mode,
/// pixels-per-unit multiplier, 9-slice border and colour, and a TMP text's font asset, size and
/// colour. Everything it produces is a <see cref="OnlineUiNativeImageStyle"/> or
/// <see cref="OnlineUiNativeTextStyle"/> the Runtime can fold, format and test — nothing here decides
/// what a reading means, and nothing here mutates what it reads.
/// </summary>
internal static class OnlineUiNativeStyleReader
{
	/// <summary>How many objects one live-canvas sweep may visit. The sweep walks the hierarchy itself
	/// rather than asking Unity for a whole array, so the bound applies before anything is allocated:
	/// one crowded screen must not turn a diagnostic into a stall.</summary>
	internal const int MaxCandidates = 512;

	/// <summary>One image's style, or null when there is no image (a game update that moved it).</summary>
	internal static OnlineUiNativeImageStyle? TryReadImage(Image? image, string source)
	{
		if (image == null)
		{
			return null;
		}

		var sprite = image.sprite;
		// Unity object — == (an Image with no sprite is a plain coloured rectangle, which is a fact too)
		var border = sprite == null ? Vector4.zero : sprite.border;
		return new OnlineUiNativeImageStyle(
			source,
			OnlineUiNativeSurfaceHost.PathOf(image.transform),
			sprite == null ? string.Empty : sprite.name,
			image.type.ToString(),
			image.pixelsPerUnitMultiplier,
			border.x,
			border.y,
			border.z,
			border.w,
			Rgba(image.color),
			1);
	}

	/// <summary>One text's style, or null when the text carries no font asset yet (a text that never
	/// rendered) — the probe reports the font as missing rather than inventing one.</summary>
	internal static OnlineUiNativeTextStyle? TryReadText(TMP_Text? text, string source)
	{
		if (text == null)
		{
			return null;
		}

		// Unity object — ==
		var font = text.font;
		if (font == null)
		{
			return null;
		}

		return new OnlineUiNativeTextStyle(
			source,
			OnlineUiNativeSurfaceHost.PathOf(text.transform),
			font.name,
			text.fontSize,
			Rgba(text.color),
			1);
	}

	/// <summary>
	/// Walks a live subtree and collects every image and text style it finds, bounded by
	/// <see cref="MaxCandidates"/> and skipping <paramref name="exclude"/> (CUO's own probe canvas is a
	/// child of the game's canvas, so a sweep that did not skip it would report CUO's rows as the
	/// game's chrome). Returns TRUE when the bound stopped it, so the reading can say the census is a
	/// sample rather than presenting it as the whole canvas.
	/// </summary>
	internal static bool Sweep(
		Transform root,
		Transform? exclude,
		string source,
		List<OnlineUiNativeImageStyle> images,
		List<OnlineUiNativeTextStyle> texts)
	{
		var pending = new Stack<Transform>();
		pending.Push(root);
		var visited = 0;
		while (pending.Count > 0 && visited < MaxCandidates)
		{
			var current = pending.Pop();
			if (current == exclude)
			{
				continue;
			}

			visited++;
			if (TryReadImage(current.GetComponent<Image>(), source) is { } image)
			{
				images.Add(image);
			}

			if (TryReadText(current.GetComponent<TMP_Text>(), source) is { } text)
			{
				texts.Add(text);
			}

			for (var index = 0; index < current.childCount; index++)
			{
				pending.Push(current.GetChild(index));
			}
		}

		return pending.Count > 0;
	}

	private static OnlineUiNativeRgba Rgba(Color color) => new(color.r, color.g, color.b, color.a);
}
