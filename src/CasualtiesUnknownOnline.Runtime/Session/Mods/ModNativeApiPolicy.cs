using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The pure native-API policy: operation-id shape rails and the safe
/// argument/result value surface. The surface is deliberately bounded — a mod
/// may pass/receive null, strings, numeric primitives, capped byte/primitive
/// arrays, and framework DTO types such as <see cref="IModNativeLocalPlayerState"/>.
/// Unity/game-assembly objects and arbitrary object graphs are rejected before
/// and after the Game Adapter seam.
/// </summary>
public static class ModNativeApiPolicy
{
	/// <summary>Maximum operation-id length.</summary>
	public const int MaxOperationLength = 128;

	/// <summary>Maximum number of arguments per native operation call.</summary>
	public const int MaxArguments = 16;

	/// <summary>Maximum string length for an argument value.</summary>
	public const int MaxStringLength = 4096;

	/// <summary>Maximum byte-array length for an argument/result value.</summary>
	public const int MaxByteArrayLength = 64 * 1024;

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

		return value switch
		{
			string s => s.Length <= MaxStringLength,
			bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => true,
			byte[] bytes => bytes.Length <= MaxByteArrayLength,
			IModNativeLocalPlayerState => true,
			bool[] a => a.Length <= MaxArrayLength,
			sbyte[] a => a.Length <= MaxArrayLength,
			short[] a => a.Length <= MaxArrayLength,
			ushort[] a => a.Length <= MaxArrayLength,
			int[] a => a.Length <= MaxArrayLength,
			uint[] a => a.Length <= MaxArrayLength,
			long[] a => a.Length <= MaxArrayLength,
			ulong[] a => a.Length <= MaxArrayLength,
			float[] a => a.Length <= MaxArrayLength,
			double[] a => a.Length <= MaxArrayLength,
			decimal[] a => a.Length <= MaxArrayLength,
			string[] a => a.Length <= MaxArrayLength,
			_ => false
		};
	}
}
