using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The radiation line as one table: reading the live line's descent and writing
/// a decided state onto it. The line is a world singleton
/// (<c>RadiationLine.line</c>) whose active flag is public and whose descent is a
/// private field, so both sides' apply paths must agree on the exact types —
/// this is that one place.
///
/// The state is authored on the host (the game's own layer timer or the straggler
/// pressure rule) and every write is ABSOLUTE: the decided state replaces the
/// live one, whether it came from a remote broadcast (guest) or from a restored
/// cut (host, where the line was just regenerated inactive and would otherwise
/// silently overwrite the restored value on the next publish).
/// </summary>
internal static class RadiationLineTable
{
	/// <summary>The live line, or null before the world object exists (Unity object — ==).</summary>
	internal static RadiationLine? Live => RadiationLine.line;

	/// <summary>The line's descent is a private field (<c>RadiationLine.cs</c>) — read through Traverse with the exact float type.</summary>
	internal static float ReadTimeGone(RadiationLine line) =>
		Traverse.Create(line).Field("timeGone").GetValue<float>();

	internal static void WriteTimeGone(RadiationLine line, float value) =>
		Traverse.Create(line).Field("timeGone").SetValue(value);

	/// <summary>
	/// Write a decided absolute state onto the live line. Returns false when there
	/// is no line to write (a world that never had one) — the caller names that,
	/// it is not a silent no-op with a success report.
	/// </summary>
	internal static bool Apply(RadiationLineStateMsg state)
	{
		var line = Live;
		if (line == null) // Unity object — ==
		{
			return false;
		}

		if (state.Active)
		{
			line.active = true;
			WriteTimeGone(line, state.TimeGone);
			return true;
		}

		if (WorldGeneration.world != null) // Unity object — ==
		{
			line.Deactivate();
		}
		else
		{
			// Without a world the native Deactivate (which walks the world's
			// players and cells) is not safe — the flag and the descent are the
			// whole observable state here.
			line.active = false;
			WriteTimeGone(line, 0f);
		}

		return true;
	}
}
