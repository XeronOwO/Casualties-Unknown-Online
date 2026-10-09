using CasualtiesUnknownOnline.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The framework's own encoding of <see cref="ModValue"/> — the ONE place that decides whether a mod's value
/// is acceptable, because accepting a value IS being able to encode it inside these budgets. There is no
/// second validator to drift: every surface that takes a value (the message channel, the declared packets,
/// and the stores the next stage moves) calls <see cref="TryEncode"/> and reports its refusal.
///
/// The shape is canonical and self-describing: one tag byte per value, fixed-width little-endian lengths and
/// containers length-prefixed, text as strict UTF-8 (a value either round-trips exactly or it does not
/// travel). Nothing here is part of the mod-visible contract — a mod never sees these bytes, which is the
/// point of the model.
///
/// Budgets bound the WALK, not the shape: the depth is checked before the encoder descends, the node count
/// is counted as it goes, and a length read from a payload is checked against the bytes actually left before
/// anything is allocated — so a malformed or hostile payload is refused with a named path instead of being
/// recursed into.
///
/// A refusal is a message that BEGINS with the path of the value that failed (`$.targets[3].hp: a number must
/// be finite to travel`), built on the way out of the walk so the successful path allocates nothing, and the
/// caller logs it with the surface's own context (mod id, packet id, key).
/// </summary>
internal static class ModValueCodec
{
	/// <summary>How deep a value may nest. CUO's own travelling contracts are two levels; a mod's own structure has room and a runaway one is still refused.</summary>
	internal const int MaxDepth = 8;

	/// <summary>How many values one encoded value may carry, containers included.</summary>
	internal const int MaxValues = 4096;

	/// <summary>How many entries one list or map may carry.</summary>
	internal const int MaxEntries = 1024;

	/// <summary>How many UTF-8 bytes one text value may encode to.</summary>
	internal const int MaxTextBytes = 16 * 1024;

	/// <summary>How many UTF-8 bytes one map field name may encode to.</summary>
	internal const int MaxKeyBytes = 256;

	/// <summary>How many bytes the binary leaf may carry.</summary>
	internal const int MaxBinaryBytes = 32 * 1024;

	private const byte TagFalse = 0x00;
	private const byte TagTrue = 0x01;
	private const byte TagInteger = 0x02;
	private const byte TagNumber = 0x03;
	private const byte TagText = 0x04;
	private const byte TagBinary = 0x05;
	private const byte TagList = 0x06;
	private const byte TagMap = 0x07;

	/// <summary>UTF-8 with the strict fallbacks: an unpaired surrogate is a refusal, not a replacement character that silently changes the value.</summary>
	private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

	/// <summary>
	/// Encode one value into <paramref name="encoded"/>, or refuse it by naming the path and the budget it
	/// broke. <paramref name="maxBytes"/> is the calling surface's own cap (the wire's 64 KiB rail), enforced
	/// while writing rather than after.
	/// </summary>
	internal static bool TryEncode(ModValue? value, int maxBytes, out byte[] encoded, out string? refusal)
	{
		encoded = [];
		refusal = null;

		if (value is null)
		{
			refusal = "$: the value is null";
			return false;
		}

		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
		var values = 0;
		if (!TryWrite(value, writer, stream, maxBytes, 0, ref values, out var inner))
		{
			refusal = "$" + inner;
			return false;
		}

		encoded = stream.ToArray();
		return true;
	}

	/// <summary>
	/// Decode one value, enforcing the same budgets <see cref="TryEncode"/> enforces and refusing anything
	/// left over — a payload that is not exactly one value is not a value.
	/// </summary>
	internal static bool TryDecode(byte[]? payload, int maxBytes, out ModValue? value, out string? refusal)
	{
		value = null;
		refusal = null;

		if (payload is null)
		{
			refusal = "$: the payload is null";
			return false;
		}

		if (payload.Length > maxBytes)
		{
			refusal = $"$: the payload is {payload.Length} bytes, the cap is {maxBytes}";
			return false;
		}

		var index = 0;
		var values = 0;
		if (!TryRead(payload, ref index, 0, ref values, out value, out var inner))
		{
			refusal = "$" + inner;
			value = null;
			return false;
		}

		var trailing = payload.Length - index;
		if (trailing != 0)
		{
			refusal = $"$: the payload carries {trailing} bytes after the value";
			value = null;
			return false;
		}

		return true;
	}

	// ---- Writing ----

	/// <summary>Writes one value. The refusal it returns is relative to the value it was given; every caller prefixes its own segment (see the class note).</summary>
	private static bool TryWrite(ModValue value, BinaryWriter writer, MemoryStream stream, int maxBytes, int depth, ref int values, out string? refusal)
	{
		refusal = null;
		if (depth > MaxDepth)
		{
			refusal = $": the value nests deeper than {MaxDepth} levels";
			return false;
		}

		if (++values > MaxValues)
		{
			refusal = $": the value carries more than {MaxValues} values";
			return false;
		}

		switch (value.Kind)
		{
			case ModValueKind.Boolean:
				value.TryGetBoolean(out var boolean);
				writer.Write(boolean ? TagTrue : TagFalse);
				return Fits(stream, maxBytes, out refusal);
			case ModValueKind.Integer:
				value.TryGetInteger(out var integer);
				writer.Write(TagInteger);
				writer.Write(integer);
				return Fits(stream, maxBytes, out refusal);
			case ModValueKind.Number:
				value.TryGetNumber(out var number);
				if (double.IsNaN(number) || double.IsInfinity(number))
				{
					refusal = ": a number must be finite to travel";
					return false;
				}

				writer.Write(TagNumber);
				writer.Write(number);
				return Fits(stream, maxBytes, out refusal);
			case ModValueKind.Text:
				value.TryGetText(out var text);
				return TryWriteText(writer, stream, maxBytes, text, MaxTextBytes, "text", out refusal);
			case ModValueKind.Binary:
				value.TryGetBinary(out var binary);
				if (binary.Length > MaxBinaryBytes)
				{
					refusal = $": a {binary.Length}-byte binary leaf exceeds the {MaxBinaryBytes}-byte cap";
					return false;
				}

				writer.Write(TagBinary);
				writer.Write(binary.Length);
				// The model's binary leaf is its own copy, and net48's BinaryWriter has no span overload.
				writer.Write(binary.ToArray());
				return Fits(stream, maxBytes, out refusal);
			case ModValueKind.List:
				return TryWriteList(value.Items!, writer, stream, maxBytes, depth, ref values, out refusal);
			case ModValueKind.Map:
				return TryWriteMap(value.Fields!, writer, stream, maxBytes, depth, ref values, out refusal);
			default:
				refusal = $": {value.Kind} is not a kind this framework can encode";
				return false;
		}
	}

	private static bool TryWriteText(BinaryWriter writer, MemoryStream stream, int maxBytes, string text, int cap, string what, out string? refusal)
	{
		refusal = null;
		if (!TryEncodeText(text, cap, what, out var bytes, out refusal))
		{
			return false;
		}

		writer.Write(TagText);
		writer.Write(bytes.Length);
		writer.Write(bytes);
		return Fits(stream, maxBytes, out refusal);
	}

	/// <summary>
	/// A map's field name is NOT a tagged value: it is the length-prefixed UTF-8
	/// of the name, so the reader knows a name can only be text and the tag would
	/// only be a byte nothing reads.
	/// </summary>
	private static bool TryWriteKey(BinaryWriter writer, string key, out string? refusal)
	{
		if (!TryEncodeText(key, MaxKeyBytes, $"field name '{Sanitize(key)}'", out var bytes, out refusal))
		{
			return false;
		}

		writer.Write(bytes.Length);
		writer.Write(bytes);
		return true;
	}

	private static bool TryEncodeText(string text, int cap, string what, out byte[] bytes, out string? refusal)
	{
		bytes = [];
		refusal = null;
		try
		{
			bytes = StrictUtf8.GetBytes(text);
		}
		catch (EncoderFallbackException)
		{
			refusal = $": the {what} is not valid UTF-16 (an unpaired surrogate cannot travel)";
			return false;
		}

		if (bytes.Length > cap)
		{
			refusal = $": the {what} is {bytes.Length} bytes encoded, the cap is {cap}";
			return false;
		}

		return true;
	}

	private static bool TryWriteList(IReadOnlyList<ModValue> items, BinaryWriter writer, MemoryStream stream, int maxBytes, int depth, ref int values, out string? refusal)
	{
		refusal = null;
		if (items.Count > MaxEntries)
		{
			refusal = $": a list of {items.Count} elements exceeds the {MaxEntries}-element cap";
			return false;
		}

		writer.Write(TagList);
		writer.Write(items.Count);
		for (var i = 0; i < items.Count; i++)
		{
			if (!TryWrite(items[i], writer, stream, maxBytes, depth + 1, ref values, out var child))
			{
				refusal = $"[{i}]{child}";
				return false;
			}
		}

		return Fits(stream, maxBytes, out refusal);
	}

	private static bool TryWriteMap(IReadOnlyDictionary<string, ModValue> fields, BinaryWriter writer, MemoryStream stream, int maxBytes, int depth, ref int values, out string? refusal)
	{
		refusal = null;
		if (fields.Count > MaxEntries)
		{
			refusal = $": a map of {fields.Count} fields exceeds the {MaxEntries}-field cap";
			return false;
		}

		writer.Write(TagMap);
		writer.Write(fields.Count);
		foreach (var pair in fields)
		{
			if (!TryWriteKey(writer, pair.Key, out var keyRefusal))
			{
				refusal = $".{Sanitize(pair.Key)}{keyRefusal}";
				return false;
			}

			if (!TryWrite(pair.Value, writer, stream, maxBytes, depth + 1, ref values, out var child))
			{
				refusal = $".{Sanitize(pair.Key)}{child}";
				return false;
			}
		}

		return Fits(stream, maxBytes, out refusal);
	}

	private static bool Fits(MemoryStream stream, int maxBytes, out string? refusal)
	{
		refusal = null;
		if (stream.Length <= maxBytes)
		{
			return true;
		}

		refusal = $": the encoded value is {stream.Length} bytes, the cap is {maxBytes}";
		return false;
	}

	// ---- Reading ----

	/// <summary>Reads one value. The refusal it returns is relative to the value it read; every caller prefixes its own segment (see the class note).</summary>
	private static bool TryRead(byte[] payload, ref int index, int depth, ref int values, out ModValue? value, out string? refusal)
	{
		value = null;
		refusal = null;
		if (depth > MaxDepth)
		{
			refusal = $": the payload nests deeper than {MaxDepth} levels";
			return false;
		}

		if (++values > MaxValues)
		{
			refusal = $": the payload carries more than {MaxValues} values";
			return false;
		}

		if (!TryReadByte(payload, ref index, out var tag))
		{
			refusal = ": the payload ends before a value";
			return false;
		}

		switch (tag)
		{
			case TagFalse:
				value = ModValue.Boolean(false);
				return true;
			case TagTrue:
				value = ModValue.Boolean(true);
				return true;
			case TagInteger:
				if (!TryReadInt64(payload, ref index, out var integer))
				{
					refusal = ": the payload ends inside an integer";
					return false;
				}

				value = ModValue.Integer(integer);
				return true;
			case TagNumber:
				if (!TryReadInt64(payload, ref index, out var bits))
				{
					refusal = ": the payload ends inside a number";
					return false;
				}

				var number = BitConverter.Int64BitsToDouble(bits);
				if (double.IsNaN(number) || double.IsInfinity(number))
				{
					refusal = ": a number must be finite to travel";
					return false;
				}

				value = ModValue.Number(number);
				return true;
			case TagText:
				if (!TryReadText(payload, ref index, MaxTextBytes, "text", out var text, out refusal))
				{
					return false;
				}

				value = ModValue.Text(text);
				return true;
			case TagBinary:
				if (!TryReadBytes(payload, ref index, MaxBinaryBytes, "binary leaf", out var bytes, out refusal))
				{
					return false;
				}

				value = ModValue.Binary(bytes);
				return true;
			case TagList:
				return TryReadList(payload, ref index, depth, ref values, out value, out refusal);
			case TagMap:
				return TryReadMap(payload, ref index, depth, ref values, out value, out refusal);
			default:
				refusal = $": 0x{tag:x2} is not a value tag";
				return false;
		}
	}

	private static bool TryReadList(byte[] payload, ref int index, int depth, ref int values, out ModValue? value, out string? refusal)
	{
		value = null;
		if (!TryReadCount(payload, ref index, "a list", out var count, out refusal))
		{
			return false;
		}

		var items = new ModValue[count];
		for (var i = 0; i < count; i++)
		{
			if (!TryRead(payload, ref index, depth + 1, ref values, out var item, out var child))
			{
				refusal = $"[{i}]{child}";
				return false;
			}

			items[i] = item!;
		}

		value = ModValue.List(items);
		return true;
	}

	private static bool TryReadMap(byte[] payload, ref int index, int depth, ref int values, out ModValue? value, out string? refusal)
	{
		value = null;
		if (!TryReadCount(payload, ref index, "a map", out var count, out refusal))
		{
			return false;
		}

		var fields = new (string Key, ModValue Value)[count];
		for (var i = 0; i < count; i++)
		{
			if (!TryReadText(payload, ref index, MaxKeyBytes, "field name", out var key, out refusal))
			{
				return false;
			}

			// The model refuses an empty field name, and a payload's bytes are a
			// peer's choice: without this the refusal would leave as an exception
			// thrown through the validator, which is exactly what this codec
			// exists to prevent.
			if (key.Length == 0)
			{
				refusal = $"[{i}]: a map field name must not be empty";
				return false;
			}

			for (var seen = 0; seen < i; seen++)
			{
				if (string.Equals(fields[seen].Key, key, StringComparison.Ordinal))
				{
					refusal = $".{Sanitize(key)}: the payload carries this field twice";
					return false;
				}
			}

			if (!TryRead(payload, ref index, depth + 1, ref values, out var item, out var child))
			{
				refusal = $".{Sanitize(key)}{child}";
				return false;
			}

			fields[i] = (key, item!);
		}

		value = ModValue.Map(fields);
		return true;
	}

	private static bool TryReadCount(byte[] payload, ref int index, string what, out int count, out string? refusal)
	{
		count = 0;
		refusal = null;
		if (!TryReadInt32(payload, ref index, out var raw))
		{
			refusal = $": the payload ends inside {what}'s length";
			return false;
		}

		if (raw < 0 || raw > MaxEntries)
		{
			refusal = $": {what} of {raw} entries exceeds the {MaxEntries}-entry cap";
			return false;
		}

		count = raw;
		return true;
	}

	private static bool TryReadText(byte[] payload, ref int index, int cap, string what, out string text, out string? refusal)
	{
		text = string.Empty;
		if (!TryReadBytes(payload, ref index, cap, what, out var bytes, out refusal))
		{
			return false;
		}

		try
		{
			text = StrictUtf8.GetString(bytes);
			return true;
		}
		catch (DecoderFallbackException)
		{
			refusal = $": the {what} is not valid UTF-8";
			return false;
		}
	}

	private static bool TryReadBytes(byte[] payload, ref int index, int cap, string what, out byte[] bytes, out string? refusal)
	{
		bytes = [];
		refusal = null;
		if (!TryReadInt32(payload, ref index, out var length))
		{
			refusal = $": the payload ends inside the {what}'s length";
			return false;
		}

		var remaining = payload.Length - index;
		if (length < 0 || length > remaining)
		{
			refusal = $": a {what} of {length} bytes is beyond the {remaining} bytes left in the payload";
			return false;
		}

		if (length > cap)
		{
			refusal = $": the {what} is {length} bytes, the cap is {cap}";
			return false;
		}

		bytes = new byte[length];
		Array.Copy(payload, index, bytes, 0, length);
		index += length;
		return true;
	}

	private static bool TryReadByte(byte[] payload, ref int index, out byte value)
	{
		if (index >= payload.Length)
		{
			value = 0;
			return false;
		}

		value = payload[index++];
		return true;
	}

	private static bool TryReadInt32(byte[] payload, ref int index, out int value)
	{
		if (payload.Length - index < 4)
		{
			value = 0;
			return false;
		}

		value = payload[index]
			| (payload[index + 1] << 8)
			| (payload[index + 2] << 16)
			| (payload[index + 3] << 24);
		index += 4;
		return true;
	}

	private static bool TryReadInt64(byte[] payload, ref int index, out long value)
	{
		if (!TryReadInt32(payload, ref index, out var low) || !TryReadInt32(payload, ref index, out var high))
		{
			value = 0;
			return false;
		}

		value = (uint)low | ((long)high << 32);
		return true;
	}

	/// <summary>Renders a mod-authored field name so a refusal cannot forge a log line with it.</summary>
	private static string Sanitize(string name)
	{
		var builder = new StringBuilder(name.Length);
		foreach (var character in name)
		{
			builder.Append(character is >= ' ' and <= '~' ? character : '?');
		}

		return builder.ToString();
	}
}
