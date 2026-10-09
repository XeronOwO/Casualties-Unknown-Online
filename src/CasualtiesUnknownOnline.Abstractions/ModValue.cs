using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The typed value a mod carries across a CUO surface — the model that replaces the opaque <c>byte[]</c>
/// envelope everywhere the framework had one (the user's 2026-10-08 ruling: "什么运走存下来，我觉得就不应该
/// 存在字节形式的啊"). The deciding question is not "does it cross a boundary" but WHO DEFINES THE SHAPE: where
/// CUO defines it the contract must be typed, and where the MOD defines it the framework still has to be
/// able to validate it, bound it structurally, log it and show it in the console. An opaque blob means the
/// framework can do none of those and every mod reinvents malformed-payload handling on its own.
///
/// The one place bytes stay legitimate is the explicit <see cref="ModValueKind.Binary"/> leaf: a mod that
/// wants its own compact encoding puts it there, on purpose — a value inside the model, never the API's
/// envelope.
///
/// A value is immutable and copies what it is handed, so it may be shared with the framework, cached and
/// read from two threads; the defensive copies the byte-shaped surfaces needed on every read and write
/// disappear with it. "Immutable" is a statement about the model's own API: no typed path changes a value,
/// and the only way past that is to reach a container's or the binary leaf's backing storage on purpose
/// (which corrupts nothing but the caller's own value). Building a value is total — the framework accepts it when it can be ENCODED inside the
/// framework's budgets, and a refusal names the path inside the model and the budget it broke — so nothing
/// here fails halfway through a mod's own composition.
/// </summary>
public sealed class ModValue : IEquatable<ModValue>
{
	/// <summary>How much of a value <see cref="ToString"/> renders: a log line must not scale with a payload.</summary>
	private const int RenderedLengthCap = 256;

	/// <summary>How deep <see cref="ToString"/> descends before it prints an ellipsis — rendering is a log aid, not a walk of arbitrary depth.</summary>
	private const int RenderedDepthCap = 16;

	private readonly bool _boolean;
	private readonly long _integer;
	private readonly double _number;
	private readonly string _text = string.Empty;
	private readonly byte[]? _binary;
	private readonly IReadOnlyList<ModValue>? _items;
	private readonly IReadOnlyDictionary<string, ModValue>? _fields;

	private ModValue(bool value)
	{
		Kind = ModValueKind.Boolean;
		_boolean = value;
	}

	private ModValue(long value)
	{
		Kind = ModValueKind.Integer;
		_integer = value;
	}

	private ModValue(double value)
	{
		Kind = ModValueKind.Number;
		_number = value;
	}

	private ModValue(string value)
	{
		Kind = ModValueKind.Text;
		_text = value;
	}

	private ModValue(byte[] value)
	{
		Kind = ModValueKind.Binary;
		_binary = value;
	}

	private ModValue(ModValue[] value)
	{
		Kind = ModValueKind.List;
		// A read-only WRAPPER, so the container a caller reads back cannot be
		// cast to the array behind it and written through: immutability is the
		// property the whole model rests on.
		_items = Array.AsReadOnly(value);
	}

	private ModValue(Dictionary<string, ModValue> value)
	{
		Kind = ModValueKind.Map;
		_fields = new ReadOnlyDictionary<string, ModValue>(value);
	}

	/// <summary>Which kind this value is. Every accessor below answers only for its own kind.</summary>
	public ModValueKind Kind { get; }

	/// <summary>The elements of a <see cref="ModValueKind.List"/> value; <c>null</c> for every other kind, so a wrong kind cannot read as an empty list.</summary>
	public IReadOnlyList<ModValue>? Items => _items;

	/// <summary>The fields of a <see cref="ModValueKind.Map"/> value; <c>null</c> for every other kind, so a wrong kind cannot read as an empty map.</summary>
	public IReadOnlyDictionary<string, ModValue>? Fields => _fields;

	/// <summary>A boolean value.</summary>
	public static ModValue Boolean(bool value) => new(value);

	/// <summary>An integer value (a Steam id, a count, an amount).</summary>
	public static ModValue Integer(long value) => new(value);

	/// <summary>A floating point value. It must be finite to travel, but nothing here refuses it: the refusal belongs to the boundary that encodes it.</summary>
	public static ModValue Number(double value) => new(value);

	/// <summary>A text value. A null argument is a programming error, not an empty string.</summary>
	public static ModValue Text(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));

	/// <summary>The binary leaf. The bytes are copied, so a later write to the caller's array cannot change this value.</summary>
	public static ModValue Binary(ReadOnlyMemory<byte> value) => new(value.ToArray());

	/// <summary>An ordered list of values. A null argument is a programming error; a null ARRAY means an empty list, the same "null collection means none" rule the rest of the mod surface follows.</summary>
	public static ModValue List(params ModValue[] items) => new(CopyItems(items));

	/// <summary>An unordered map. A null or empty field name and a repeated field name are programming errors.</summary>
	public static ModValue Map(params (string Key, ModValue Value)[] fields) => new(CopyFields(fields));

	/// <summary>Reads a boolean; false for any other kind.</summary>
	public bool TryGetBoolean(out bool value)
	{
		value = _boolean;
		return Kind == ModValueKind.Boolean;
	}

	/// <summary>Reads an integer; false for any other kind (a <see cref="ModValueKind.Number"/> is not rounded into one).</summary>
	public bool TryGetInteger(out long value)
	{
		value = _integer;
		return Kind == ModValueKind.Integer;
	}

	/// <summary>Reads a number; an integer reads as its exact value, because an integer IS a number.</summary>
	public bool TryGetNumber(out double value)
	{
		value = Kind switch
		{
			ModValueKind.Number => _number,
			ModValueKind.Integer => _integer,
			_ => 0d,
		};

		return Kind is ModValueKind.Number or ModValueKind.Integer;
	}

	/// <summary>Reads text; false for any other kind.</summary>
	public bool TryGetText(out string value)
	{
		value = _text;
		return Kind == ModValueKind.Text;
	}

	/// <summary>Reads the binary leaf; false for any other kind. The memory is this value's own copy, and no typed path changes those bytes — a caller that deliberately reaches the backing array through <c>MemoryMarshal</c> is outside that guarantee, as with any <see cref="ReadOnlyMemory{T}"/> over an array.</summary>
	public bool TryGetBinary(out ReadOnlyMemory<byte> value)
	{
		value = _binary;
		return Kind == ModValueKind.Binary;
	}

	/// <summary>Reads one field of a <see cref="ModValueKind.Map"/> value; false for any other kind and for a field that is not there.</summary>
	public bool TryGetField(string name, out ModValue value)
	{
		value = null!;
		return name is not null && _fields is not null && _fields.TryGetValue(name, out value!);
	}

	/// <summary>Structural equality: same kind, same contents. A map compares by field, so the order its entries were written in is not part of a value.</summary>
	public bool Equals(ModValue? other)
	{
		if (other is null || other.Kind != Kind)
		{
			return false;
		}

		return Kind switch
		{
			ModValueKind.Boolean => _boolean == other._boolean,
			ModValueKind.Integer => _integer == other._integer,
			ModValueKind.Number => _number.Equals(other._number),
			ModValueKind.Text => string.Equals(_text, other._text, StringComparison.Ordinal),
			ModValueKind.Binary => _binary!.AsSpan().SequenceEqual(other._binary),
			ModValueKind.List => _items!.SequenceEqual(other._items!),
			ModValueKind.Map => FieldsEqual(other._fields!),
			_ => false,
		};
	}

	/// <inheritdoc />
	public override bool Equals(object? obj) => Equals(obj as ModValue);

	/// <inheritdoc />
	public override int GetHashCode()
	{
		var hash = (int)Kind + 1;
		switch (Kind)
		{
			case ModValueKind.Boolean:
				return hash * 31 + (_boolean ? 1 : 0);
			case ModValueKind.Integer:
				return hash * 31 + _integer.GetHashCode();
			case ModValueKind.Number:
				return hash * 31 + _number.GetHashCode();
			case ModValueKind.Text:
				return hash * 31 + StringComparer.Ordinal.GetHashCode(_text);
			case ModValueKind.Binary:
				return _binary!.Aggregate(hash, (current, b) => (current * 31) + b);
			case ModValueKind.List:
				return _items!.Aggregate(hash, (current, item) => (current * 31) + item.GetHashCode());
			case ModValueKind.Map:
				// Order-independent, because a map is: the field hashes are combined commutatively.
				var fields = 0;
				foreach (var pair in _fields!)
				{
					fields += (StringComparer.Ordinal.GetHashCode(pair.Key) * 397) ^ pair.Value.GetHashCode();
				}

				return (hash * 31) + fields;
			default:
				return hash;
		}
	}

	/// <summary>Two values are equal when they are structurally equal — the shape a mod wants when it compares a value it received with one it built.</summary>
	public static bool operator ==(ModValue? left, ModValue? right) =>
		left is null ? right is null : left.Equals(right);

	/// <summary>See <see cref="operator ==(ModValue?, ModValue?)"/>.</summary>
	public static bool operator !=(ModValue? left, ModValue? right) => !(left == right);

	/// <summary>A bounded, log-safe rendering (JSON-shaped). It truncates rather than growing with the payload, and descends at most <see cref="RenderedDepthCap"/> levels.</summary>
	public override string ToString()
	{
		var builder = new StringBuilder();
		Render(builder, 0);
		if (builder.Length <= RenderedLengthCap)
		{
			return builder.ToString();
		}

		builder.Length = RenderedLengthCap - 1;
		return builder.Append('…').ToString();
	}

	private static ModValue[] CopyItems(ModValue[]? items)
	{
		if (items is null || items.Length == 0)
		{
			return [];
		}

		var copy = new ModValue[items.Length];
		for (var i = 0; i < items.Length; i++)
		{
			copy[i] = items[i] ?? throw new ArgumentNullException(nameof(items), "A list element cannot be null; the model has no null value.");
		}

		return copy;
	}

	private static Dictionary<string, ModValue> CopyFields((string Key, ModValue Value)[]? fields)
	{
		var copy = new Dictionary<string, ModValue>(StringComparer.Ordinal);
		foreach (var (key, value) in fields ?? [])
		{
			if (string.IsNullOrEmpty(key))
			{
				throw new ArgumentException("A map field name cannot be null or empty.", nameof(fields));
			}

			if (!copy.ContainsKey(key))
			{
				copy[key] = value ?? throw new ArgumentNullException(nameof(fields), "A map field value cannot be null; the model has no null value.");
				continue;
			}

			throw new ArgumentException($"The map carries the field '{key}' twice.", nameof(fields));
		}

		return copy;
	}

	private bool FieldsEqual(IReadOnlyDictionary<string, ModValue> other)
	{
		if (_fields!.Count != other.Count)
		{
			return false;
		}

		foreach (var pair in _fields)
		{
			if (!other.TryGetValue(pair.Key, out var value) || !pair.Value.Equals(value))
			{
				return false;
			}
		}

		return true;
	}

	private void Render(StringBuilder builder, int depth)
	{
		if (builder.Length >= RenderedLengthCap)
		{
			return;
		}

		if (depth > RenderedDepthCap)
		{
			builder.Append('…');
			return;
		}

		switch (Kind)
		{
			case ModValueKind.Boolean:
				builder.Append(_boolean ? "true" : "false");
				break;
			case ModValueKind.Integer:
				builder.Append(_integer.ToString(CultureInfo.InvariantCulture));
				break;
			case ModValueKind.Number:
				builder.Append(_number.ToString("R", CultureInfo.InvariantCulture));
				break;
			case ModValueKind.Text:
				AppendText(builder, _text);
				break;
			case ModValueKind.Binary:
				builder.Append('<').Append(_binary!.Length).Append(" bytes>");
				break;
			case ModValueKind.List:
				RenderItems(builder, depth);
				break;
			case ModValueKind.Map:
				RenderFields(builder, depth);
				break;
		}
	}

	private void RenderItems(StringBuilder builder, int depth)
	{
		builder.Append('[');
		for (var i = 0; i < _items!.Count; i++)
		{
			if (i > 0)
			{
				builder.Append(", ");
			}

			_items[i].Render(builder, depth + 1);
		}

		builder.Append(']');
	}

	private void RenderFields(StringBuilder builder, int depth)
	{
		builder.Append('{');
		var first = true;
		foreach (var pair in _fields!)
		{
			if (!first)
			{
				builder.Append(", ");
			}

			first = false;
			AppendText(builder, pair.Key);
			builder.Append(": ");
			pair.Value.Render(builder, depth + 1);
		}

		builder.Append('}');
	}

	private static void AppendText(StringBuilder builder, string text)
	{
		builder.Append('"');
		foreach (var character in text)
		{
			switch (character)
			{
				case '"':
					builder.Append("\\\"");
					break;
				case '\\':
					builder.Append("\\\\");
					break;
				case '\n':
					builder.Append("\\n");
					break;
				case '\r':
					builder.Append("\\r");
					break;
				case '\t':
					builder.Append("\\t");
					break;
				default:
					if (char.IsControl(character))
					{
						builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
					}
					else
					{
						builder.Append(character);
					}

					break;
			}
		}

		builder.Append('"');
	}
}
