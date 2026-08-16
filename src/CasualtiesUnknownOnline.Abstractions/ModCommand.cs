using System;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// A command definition a mod registers via <see cref="IModCommands.Register"/>
/// (typically in <see cref="ICuoMod.Bind"/>). The handler runs ONLY on the
/// host's copy of the mod, on the Unity main thread, and must be fast — CUO
/// does not host background work for mod commands.
/// </summary>
public sealed class ModCommand(string name, Func<IModCommandContext, string?> handler, string? description = null, bool isHostAction = false)
{
	public string Name { get; } = name;

	public string? Description { get; } = description;

	/// <summary>
	/// True for a command that mutates host-authoritative state. Registering it
	/// additionally requires <see cref="ModPermission.ExecuteHostAction"/>.
	/// </summary>
	public bool IsHostAction { get; } = isHostAction;

	/// <summary>The command body. Return the output text (null = no output); throwing turns the result into a failure.</summary>
	public Func<IModCommandContext, string?> Handler { get; } = handler;
}
