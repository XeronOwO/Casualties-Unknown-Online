using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// A mod's own list, computed from the whole context — which is what lets argument N depend on N-1 (a key's values, a member's own items). The delegate runs
/// inside a frame, so it must be cheap; the console isolates a throw as "this argument offered nothing".
/// </summary>
public sealed class DelegateCompleter(Func<ICompletionContext, IReadOnlyList<CommandCompletion>> complete) : IArgumentCompleter
{
	public IReadOnlyList<CommandCompletion> Complete(ICompletionContext context) => complete(context);
}
