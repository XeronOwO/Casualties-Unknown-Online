using System;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// A match stage over a predicate — the shared stub for the completion tests.
/// A predicate that throws is how the catalog's stage isolation is exercised, so
/// the stub deliberately adds nothing on top of the interface.
/// </summary>
internal sealed class StubMatchStage(Func<ResourceLocationEntry, string, bool> matches) : IResourceLocationMatchStage
{
	public bool Matches(ResourceLocationEntry entry, string prefix) => matches(entry, prefix);
}
