using System.Runtime.CompilerServices;

// The transfer capability is a library a mod consumes; its own tests are the first reader (the mod half lands
// with the music-player stage of the ticket).
[assembly: InternalsVisibleTo("CasualtiesUnknownOnline.Tests")]
