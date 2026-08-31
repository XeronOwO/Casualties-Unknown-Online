using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// Marks a built-in console command handler method. The console registry scans
/// the owning type at startup and builds the immutable route table from these
/// attributes, so command metadata lives beside the handler instead of in a
/// hard-coded registration list.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
internal sealed class ConsoleCommandAttribute(
	string name,
	string description,
	CommandPermission permission,
	string usage,
	params CommandArgumentKind[] argumentKinds) : Attribute
{
	public string Name { get; } = name;

	public string Description { get; } = description;

	public string Usage { get; } = usage;

	public CommandPermission Permission { get; } = permission;

	public IReadOnlyList<CommandArgumentKind> ArgumentKinds { get; } = argumentKinds;
}
