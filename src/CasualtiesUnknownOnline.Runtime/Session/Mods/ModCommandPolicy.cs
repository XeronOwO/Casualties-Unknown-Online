using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The shape rails for the host-command domain (Phase 4b). The framework
/// validates these BEFORE a command handler runs: the host's copy of the mod
/// is still the semantic validator of its parameters, but the wire/protocol
/// shape (name length, argument count/length/total, result size) is framework
/// policy and is refused or clamped here. Pure class — no DI, no session.
/// </summary>
public static class ModCommandPolicy
{
	public const int MaxModIdLength = 128;
	public const int MaxNameLength = 64;
	public const int MaxArgumentCount = 16;
	public const int MaxArgumentLength = 256;
	public const int MaxTotalArgumentLength = 4 * 1024;
	public const int MaxOutputLength = 32 * 1024;
	public const int MaxErrorLength = 4 * 1024;

	/// <summary>A command name is a non-empty, edge-whitespace-free string of at most <see cref="MaxNameLength"/> chars.</summary>
	public static bool IsValidName(string? name) =>
		!string.IsNullOrWhiteSpace(name) && name!.Trim() == name && name.Length <= MaxNameLength;

	/// <summary>The argument shape: count, per-argument and total length caps.</summary>
	public static bool AreArgumentsValid(IReadOnlyList<string>? arguments)
	{
		if (arguments is null || arguments.Count > MaxArgumentCount)
		{
			return false;
		}

		var total = 0;
		foreach (var argument in arguments)
		{
			if (argument is null || argument.Length > MaxArgumentLength)
			{
				return false;
			}

			total += argument.Length;
			if (total > MaxTotalArgumentLength)
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>Validates the decoded request frame's shape (ModId routes the request — an invalid id is unrouteable).</summary>
	/// <summary>A mod id on a command frame: non-empty, edge-whitespace-free, capped.</summary>
	public static bool IsValidModId(string? modId) =>
		!string.IsNullOrWhiteSpace(modId) && modId!.Trim() == modId && modId.Length <= MaxModIdLength;

	public static bool IsValidRequest(ModCommandRequestMsg msg) =>
		IsValidModId(msg.ModId)
		&& IsValidName(msg.Name)
		&& AreArgumentsValid(msg.Arguments);

	/// <summary>Validates a decoded result frame before it can settle a pending callback.</summary>
	public static bool IsValidResult(ModCommandResultMsg msg) =>
		IsValidModId(msg.ModId)
		&& IsValidName(msg.Name)
		&& msg.Output is not null && msg.Output.Length <= MaxOutputLength
		&& msg.Error is not null && msg.Error.Length <= MaxErrorLength;

	/// <summary>Cap the handler's output — over-cap results are truncated, never dropped silently.</summary>
	public static string ClampOutput(string? output) => Clamp(output, MaxOutputLength);

	/// <summary>Cap the failure reason before it travels back to the guest.</summary>
	public static string ClampError(string? error) => Clamp(error, MaxErrorLength);

	private static string Clamp(string? text, int maxLength)
	{
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}

		var value = text!;
		return value.Length <= maxLength ? value : value.Substring(0, maxLength);
	}
}
