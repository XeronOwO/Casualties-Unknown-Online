using System.Linq;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The value helpers the mod-surface suites share. A status value in these cases
/// is a list of the integers the case wrote, so the case reads as the value it
/// published; <see cref="OverCap"/> is structurally legal per element and still
/// cannot fit a surface's payload rail.
/// </summary>
internal static class ModValues
{
	/// <summary>A list of the integers a case passed.</summary>
	internal static ModValue Ints(params int[] numbers) =>
		ModValue.List([.. numbers.Select(number => ModValue.Integer(number))]);

	/// <summary>100 texts of 1 KiB: legal per element, about 100 KiB encoded — over every surface's rail.</summary>
	internal static ModValue OverCap()
	{
		var items = new ModValue[100];
		for (var i = 0; i < items.Length; i++)
		{
			items[i] = ModValue.Text(new string('x', 1024));
		}

		return ModValue.List(items);
	}

	/// <summary>
	/// A value whose encoding is EXACTLY the 64 KiB rail, so the rail's inclusive
	/// end is a case rather than a claim. The arithmetic of the encoding: a map is
	/// 1 tag + 4 count, and each entry is a 4-byte length + a 1-byte name + a text
	/// value (1 tag + 4 length + bytes), so four single-letter fields encode
	/// 5 + 4 * (4 + 1 + 5) + the four texts = 45 + 65491 = 65536.
	/// </summary>
	internal static ModValue AtTheRail() =>
		ModValue.Map(
			("a", ModValue.Text(new string('x', 16384))),
			("b", ModValue.Text(new string('x', 16384))),
			("c", ModValue.Text(new string('x', 16384))),
			("d", ModValue.Text(new string('x', 16339))));
}
