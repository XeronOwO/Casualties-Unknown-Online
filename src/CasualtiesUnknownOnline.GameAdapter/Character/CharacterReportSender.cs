using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Sends a character snapshot on the wire: a guest reports its own body to the host, the host broadcasts its own to every guest, and every snapshot is stamped with
/// the layer its position belongs to so a receiver still in another layer refuses it.
/// </summary>
/// <remarks>
/// Its own type because the wire decision (report or broadcast, throttled or immediate, stamped how) is one rule set that both the 1 Hz report and the immediate
/// re-report after an inventory move go through; keeping it next to the capture would let one of them grow its own copy of the stamp.
/// </remarks>
internal sealed class CharacterReportSender(
	ISessionControl session,
	IWorldControl world,
	ICharacterDataControl characterData,
	ILogger log)
{
	/// <summary>
	/// Send one snapshot. <paramref name="throttled"/> is the 1 Hz report (the only one that logs the per-send counts and a missing-native-fields warning); an
	/// immediate report after an inventory move stays silent so a burst of moves does not flood the log.
	/// </summary>
	internal void Send(CharacterDataMsg data, bool throttled, string? nativeFailure = null)
	{
		// The position in this snapshot belongs to the layer that captured it — stamp it so a
		// receiver still in another layer refuses to apply it.
		data.Generation = world.CurrentGeneration;

		if (session.Role == SessionRole.Host)
		{
			// Host → guests: their clones of the host render its carried items.
			if (throttled)
			{
				log.LogDebug("[CloneRender] host broadcasting char data ({Count} items).", data.Items.Count);
				LogMissingNativeFields(nativeFailure);
			}

			characterData.BroadcastHostCharacterData(data);
		}
		else
		{
			if (throttled)
			{
				log.LogDebug("[CloneRender] guest reporting char data ({Count} items).", data.Items.Count);
				LogMissingNativeFields(nativeFailure);
			}

			characterData.ReportCharacterData(data);
		}
	}

	/// <summary>Name the layer a snapshot's position belongs to — the capturing client's current world generation.</summary>
	internal CharacterDataMsg Stamp(CharacterDataMsg data)
	{
		data.Generation = world.CurrentGeneration;
		return data;
	}

	/// <summary>
	/// The snapshot this report just built carries no native character fields: say WHY, because the reason decides what the player loses — a missing camera or wound
	/// window is a live-scene problem, while a body without its happiness window is a data problem. Only the throttled (1 Hz) reports log it, so a scene that cannot
	/// be read does not flood the log from every inventory move.
	/// </summary>
	private void LogMissingNativeFields(string? nativeFailure)
	{
		if (nativeFailure is not null)
		{
			log.LogWarning(
				"Character snapshot without native character fields ({Missing}); the snapshot still reconnects the character, but a restore of it keeps the game's defaults for those fields.",
				nativeFailure);
		}
	}
}
