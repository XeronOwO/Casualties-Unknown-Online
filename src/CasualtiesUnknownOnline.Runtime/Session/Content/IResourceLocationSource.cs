using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Content;

/// <summary>
/// One contributor to the resource-location vocabulary. Runtime registers its
/// own built-in and mod-content sources; the Game Adapter registers the vanilla
/// game-content source (it is the only layer that may read game tables). The
/// catalog merges sources by canonical id, so a source never needs to know
/// about the others.
/// </summary>
public interface IResourceLocationSource
{
	/// <summary>A snapshot of the entries this source contributes (copy — safe to hold).</summary>
	IReadOnlyList<ResourceLocationEntry> Entries { get; }
}
