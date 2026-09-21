using System.Runtime.CompilerServices;

// One product, two assemblies: the plug-in half applies the game patch to the
// crafting search box, this half owns the reading table, the matcher and the CUO
// completion seam. They share internals the way CUO's own Runtime/GameAdapter
// split does, and the test project sees the same seam (the switch and the stage
// are exercised through it). The public surface of this artifact is deliberately
// almost nothing: CUO's discovery requires the [CuoMod] entry point to be public,
// and everything else is implementation.
[assembly: InternalsVisibleTo("CasualtiesUnknownOnline.PinyinSearch")]
[assembly: InternalsVisibleTo("CasualtiesUnknownOnline.Tests")]
