using System;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The pure native-API policy: operation-id shape rails and the safe
/// argument/result value surface. The surface is deliberately bounded — a mod
/// may pass/receive null, strings, numeric primitives, <see cref="ModValue"/>
/// (the framework's typed data model, which carries the binary leaf where bytes
/// really are the value), capped primitive arrays, and framework DTO types such
/// as <see cref="IModNativeLocalPlayerState"/>. Unity/game-assembly objects and
/// arbitrary object graphs are rejected before and after the Game Adapter seam.
/// </summary>
public static class ModNativeApiPolicy
{
	/// <summary>Maximum operation-id length.</summary>
	public const int MaxOperationLength = 128;

	/// <summary>Maximum number of arguments per native operation call.</summary>
	public const int MaxArguments = 16;

	/// <summary>Maximum string length for an argument value.</summary>
	public const int MaxStringLength = 4096;

	/// <summary>Maximum ENCODED size for a <see cref="ModValue"/> argument/result value; the model's own structural budgets bound the shape inside it.</summary>
	public const int MaxValueBytes = 64 * 1024;

	/// <summary>Maximum element count for a primitive-array argument/result.</summary>
	public const int MaxArrayLength = 1024;

	/// <summary>
	/// True when the operation id is non-empty, at most <see cref="MaxOperationLength"/>
	/// characters, and uses only lowercase/uppercase ASCII letters, digits,
	/// dot, underscore or hyphen (the stable dotted id shape used by the Mod API).
	/// </summary>
	public static bool IsValidOperation(string operation)
	{
		if (string.IsNullOrEmpty(operation) || operation.Length > MaxOperationLength)
		{
			return false;
		}

		foreach (var c in operation)
		{
			var isAsciiLetterOrDigit = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
			if (!isAsciiLetterOrDigit && c is not ('.' or '_' or '-'))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>True when every argument satisfies <see cref="IsSafeValue"/> and the argument count is within <see cref="MaxArguments"/>.</summary>
	public static bool IsValidArguments(object?[] arguments)
	{
		if (arguments is null || arguments.Length > MaxArguments)
		{
			return false;
		}

		foreach (var value in arguments)
		{
			if (!IsSafeValue(value))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>True when the value is inside the safe native-API value surface.</summary>
	public static bool IsSafeResult(object? value) => IsSafeValue(value);

	private static bool IsSafeValue(object? value)
	{
		if (value is null)
		{
			return true;
		}

		if (value is Array array)
		{
			return IsSafeArray(array);
		}

		return value switch
		{
			string s => s.Length <= MaxStringLength,
			bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => true,
			ModValue modValue => ModValueCodec.TryEncode(modValue, MaxValueBytes, out _, out _),
			IModNativeLocalPlayerState => true,
			_ => false
		};
	}

	/// <summary>
	/// The admitted array shapes, decided by RANK and ELEMENT TYPE rather than by
	/// a list of <c>T[]</c> type patterns — because on this stack a signed and an
	/// unsigned array of the same width are NOT distinguishable by pattern:
	/// measured on net48, <c>value is sbyte[]</c> is true for a <c>byte[]</c> and
	/// the other way round (the same holds for <c>short[]</c>/<c>ushort[]</c>,
	/// <c>int[]</c>/<c>uint[]</c>, <c>long[]</c>/<c>ulong[]</c>; every other
	/// element type is exact). A pattern list therefore cannot state the one rule
	/// this surface exists to state now — that the model's binary leaf replaced
	/// the raw byte array — so the element type is compared explicitly and
	/// <c>byte</c> is simply not in the admitted list.
	/// </summary>
	private static bool IsSafeArray(Array array)
	{
		if (array.Rank != 1 || array.Length > MaxArrayLength)
		{
			return false;
		}

		var element = array.GetType().GetElementType();
		return element == typeof(bool)
			|| element == typeof(sbyte)
			|| element == typeof(short)
			|| element == typeof(ushort)
			|| element == typeof(int)
			|| element == typeof(uint)
			|| element == typeof(long)
			|| element == typeof(ulong)
			|| element == typeof(float)
			|| element == typeof(double)
			|| element == typeof(decimal)
			|| element == typeof(string);
	}
}
