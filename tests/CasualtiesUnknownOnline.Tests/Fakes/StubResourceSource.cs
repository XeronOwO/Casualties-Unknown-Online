using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Content;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// A resource source over a fixed entry list — the shared stub for the
/// completion tests (the catalog's ranking and the mod stage registry).
/// </summary>
internal sealed class StubResourceSource(params ResourceLocationEntry[] entries) : IResourceLocationSource
{
	public IReadOnlyList<ResourceLocationEntry> Entries => entries;
}
