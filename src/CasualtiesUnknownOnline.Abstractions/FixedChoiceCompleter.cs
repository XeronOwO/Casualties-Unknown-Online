using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The fixed word set a parameter accepts — the "presets" a player picks from (`on`/`off`, a mode name, a rule's allowed values). It filters on the partial
/// token, keeping the declared order so the same list is offered every time.
/// </summary>
public sealed class FixedChoiceCompleter(params string[] words) : IArgumentCompleter
{
	private readonly string[] _words = words ?? [];

	public IReadOnlyList<CommandCompletion> Complete(ICompletionContext context) =>
	[
		.. _words
			.Where(word => word.StartsWith(context.Partial, StringComparison.OrdinalIgnoreCase))
			.Select(word => new CommandCompletion(word))
	];
}
