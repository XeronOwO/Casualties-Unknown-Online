using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The shipped definition a mod builds: the shape in the constructor and the body as a delegate, so a command is one object a mod can declare at bind time.
/// </summary>
/// <remarks>
/// The framework validates the name and the aliases, asks <see cref="Permission"/> before running anything, and renders every keyed sentence on the client
/// that reads it. A definition that answers with text instead of a key still prints, but only the keyed form is localised — the same rule every built-in
/// follows.
/// </remarks>
public sealed class ConsoleCommandDefinition(
	string name,
	string description,
	Func<ICommandInvocation, ConsoleAnswer> handler,
	CommandPermission permission = CommandPermission.Anyone,
	CommandScope scope = CommandScope.Local,
	IReadOnlyList<ICommandParameter>? parameters = null,
	IReadOnlyList<string>? aliases = null,
	string? descriptionKey = null,
	string? usage = null) : ICommandDefinition
{
	public string Name { get; } = name;

	public IReadOnlyList<string> Aliases { get; } = aliases is null ? [] : [.. aliases];

	public string Description { get; } = description;

	public string? DescriptionKey { get; } = descriptionKey;

	public string Usage { get; } = usage ?? "/" + name;

	public CommandPermission Permission { get; } = permission;

	public CommandScope Scope { get; } = scope;

	public IReadOnlyList<ICommandParameter> Parameters { get; } = parameters is null ? [] : [.. parameters];

	public ConsoleAnswer Invoke(ICommandInvocation invocation) => handler(invocation);
}
