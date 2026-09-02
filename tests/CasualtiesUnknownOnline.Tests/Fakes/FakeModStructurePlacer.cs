using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.Mods;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// A recording fake for the Runtime → Game Adapter structure placement seam. It
/// lets mod structure-placement tests verify the permission/session/policy layer
/// without loading the game assembly and without touching Unity.
/// </summary>
internal sealed class FakeModStructurePlacer : IModStructurePlacer
{
	private readonly List<(string StructureId, int OriginX, int OriginY)> _calls = [];

	public bool Result { get; set; } = true;

	public IReadOnlyList<(string StructureId, int OriginX, int OriginY)> Calls => _calls;

	public bool TryPlaceStructure(string structureId, int originX, int originY)
	{
		_calls.Add((structureId, originX, originY));
		return Result;
	}
}
