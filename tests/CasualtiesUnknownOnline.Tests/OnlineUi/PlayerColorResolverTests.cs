using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.OnlineUi;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.OnlineUi;

/// <summary>
/// The palette the marker colours come from: the automatic per-SteamId assignment, and — since ticket
/// online-ui-art-and-controls-overhaul (S3) — the picker's own presets, offered as colour blocks under
/// the names this class exposes. The picker reads the names and the colours from here rather than keeping
/// a list of its own, so the couple of facts that hold the two halves together live here too.
/// </summary>
public sealed class PlayerColorResolverTests
{
	[Fact]
	public void SameSteamId_AlwaysResolvesToSameColor()
	{
		var first = PlayerColorResolver.Resolve(123456789UL);
		var second = PlayerColorResolver.Resolve(123456789UL);

		Assert.Equal(first.R, second.R);
		Assert.Equal(first.G, second.G);
		Assert.Equal(first.B, second.B);
		Assert.Equal(first.A, second.A);
	}

	[Fact]
	public void ResolvedColors_AreOpaqueAndInValidFloatRange()
	{
		foreach (var steamId in SteamIds())
		{
			var color = PlayerColorResolver.Resolve(steamId);
			Assert.InRange(color.R, 0f, 1f);
			Assert.InRange(color.G, 0f, 1f);
			Assert.InRange(color.B, 0f, 1f);
			Assert.Equal(1f, color.A);
		}
	}

	[Fact]
	public void ResolvedColors_CoverAtLeastFourDistinctPaletteEntries()
	{
		var colors = new HashSet<(float R, float G, float B)>();
		foreach (var steamId in SteamIds())
		{
			var color = PlayerColorResolver.Resolve(steamId);
			colors.Add((color.R, color.G, color.B));
		}

		Assert.True(colors.Count >= 4, $"expected a useful spread of teammate colors, got {colors.Count}");
	}

	[Fact]
	public void Palette_ExposesEverySelectableColorForThePicker() =>
		Assert.Equal(8, PlayerColorResolver.PaletteValues.Count);

	[Fact]
	public void PaletteNames_MatchTheColoursTheyAreOfferedUnder() =>
		Assert.Equal(PlayerColorResolver.PaletteValues.Count, PlayerColorResolver.PaletteNames.Count);

	/// <summary>
	/// Every name is the tail of a catalogue key, and the picker renders it through
	/// <c>ctx.T($"prefs.color.{name}")</c>. A palette entry added without its two labels would otherwise
	/// show a raw key at the player, which no gate can see (an interpolated key space is not a key).
	/// </summary>
	[Fact]
	public void EveryPaletteName_HasItsCatalogueLabel()
	{
		foreach (var name in PlayerColorResolver.PaletteNames)
		{
			var key = $"prefs.color.{name}";
			Assert.True(LocalizationCatalog.English.ContainsKey(key), $"the palette colour `{name}` has no English label ({key})");
			Assert.True(LocalizationCatalog.Chinese.ContainsKey(key), $"the palette colour `{name}` has no Chinese label ({key})");
		}
	}

	/// <summary>The picker tells the stored colour from the palette's by its TEXT, so two entries that
	/// share one would make the current entry unnameable.</summary>
	[Fact]
	public void NoTwoPaletteEntries_ShareTheirHexText()
	{
		var texts = new HashSet<string>(StringComparer.Ordinal);
		foreach (var color in PlayerColorResolver.PaletteValues)
		{
			Assert.True(texts.Add(color.ToHexString()), $"two palette entries carry {color.ToHexString()}, so the picker could not say which one is current");
		}
	}

	/// <summary>What the picker stores for a swatch is this text, and what it compares a stored colour
	/// against is the same text: the round trip has to be exact.</summary>
	[Fact]
	public void EveryPaletteColor_RoundTripsThroughItsOwnText()
	{
		foreach (var color in PlayerColorResolver.PaletteValues)
		{
			var text = color.ToHexString();
			Assert.True(PlayerColorValue.TryParseHex(text, out var parsed), $"the palette colour {text} must parse back");
			Assert.Equal(text, parsed.ToHexString());
		}
	}

	private static IEnumerable<ulong> SteamIds()
	{
		for (ulong i = 1; i <= 64; i++)
		{
			yield return i * 0x100000001B3UL; // a few identity-space-like values
		}
	}
}
