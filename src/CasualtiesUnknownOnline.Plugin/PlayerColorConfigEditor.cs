using System;
using BepInEx.Configuration;
using CasualtiesUnknownOnline.Runtime.OnlineUi;

namespace CasualtiesUnknownOnline;

/// <summary>
/// Owns the BepInEx config entry for the local player's marker colour (ticket
/// online-ui-art-and-controls-overhaul, S3). The entry carries the colour ITSELF — its hex text in the
/// game's own idiom (<c>#RRGGBB</c>, or <c>#RRGGBBAA</c> when the colour is not opaque) — because an
/// arbitrary colour is not an index into the palette. An empty value means automatic: the stable
/// per-SteamId assignment <see cref="PlayerColorResolver.Resolve"/> derives.
///
/// <para>
/// The runtime identity adapters receive the resolved value through <see cref="PlayerColorValue"/>, so
/// handshakes and roster announcements carry it to peers exactly as a palette choice did — the wire
/// already carries four floats. The entry is bound through the same BepInEx file as every other
/// preference, which is what makes the configuration profiles capture and restore it with no extra
/// wiring of its own.
/// </para>
/// </summary>
internal sealed class PlayerColorConfigEditor
{
	private readonly ConfigFile _config;
	private readonly ConfigEntry<string> _colorHex;

	internal PlayerColorConfigEditor(ConfigFile config, ConfigEntry<string> colorHex)
	{
		_config = config;
		_colorHex = colorHex;
	}

	/// <summary>The stored text: the colour's hex form, or empty when the colour is automatic.</summary>
	internal string StoredHex => _colorHex.Value;

	/// <summary>The configured colour, or null when the colour is automatic — including the case of a
	/// stored value this build cannot read, which <see cref="IsStoredValueReadable"/> tells apart from a
	/// deliberate automatic.</summary>
	internal PlayerColorValue? CurrentColor =>
		PlayerColorValue.TryParseHex(_colorHex.Value, out var color) ? color : null;

	/// <summary>Whether the stored text is empty (automatic) or a colour this build reads. False means the
	/// file carries something else — a hand edit, or a value written by a build whose form changed — which
	/// the composition reports once and then treats as automatic.</summary>
	internal bool IsStoredValueReadable =>
		_colorHex.Value.Trim().Length == 0 || PlayerColorValue.TryParseHex(_colorHex.Value, out _);

	/// <summary>Stores <paramref name="color"/>, or clears the entry back to automatic when it is null.
	/// Selecting the colour that is already chosen writes nothing: a click on the current swatch must not
	/// touch the disk.</summary>
	internal void SetColor(PlayerColorValue? color)
	{
		var hex = color?.ToHexString() ?? "";
		if (string.Equals(_colorHex.Value, hex, StringComparison.Ordinal))
		{
			return;
		}

		_colorHex.Value = hex;
		_config.Save();
	}
}
