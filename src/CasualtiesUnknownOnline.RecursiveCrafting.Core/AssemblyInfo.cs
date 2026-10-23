using System.Runtime.CompilerServices;

// The resolver's types are public for the plug-in half to consume; the model is the mod's own and carries no
// CUO dependency on purpose (this assembly references nothing at all).
[assembly: InternalsVisibleTo("CasualtiesUnknownOnline.Tests")]
