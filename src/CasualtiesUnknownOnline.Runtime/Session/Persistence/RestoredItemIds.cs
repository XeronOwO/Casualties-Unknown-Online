using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Persistence;

/// <summary>
/// The instance ids ONE archive re-issued, read out of both of its carriers. It is a
/// type of its own because the two readings are the whole of a fact the restore needs
/// before it does anything else (<see cref="IRestoredItemIdSeed"/>), and because the
/// relation between them is exactly what must not be ASSUMED:
/// <list type="bullet">
/// <item>the kernel checkpoint's item rows — world, carried, contained and worn items,
/// i.e. everything the kernel knows,</item>
/// <item>every decoded character's carried items, whose <c>InstanceId</c> the live 1 Hz
/// report wrote while the kernel's separate carried-registration cadence had not caught
/// up: inside that window the character file is an id's ONLY carrier.</item>
/// </list>
///
/// A character's container contents count as much as the container: the restore re-stamps
/// the whole tree onto the rebuilt items, so a nested id is as live as its parent's.
/// </summary>
internal static class RestoredItemIds
{
	/// <summary>
	/// Every id the archive re-issued, with each carrier's size for the line the restore logs.
	/// The union is deliberately a LIST and not a set: the rule behind it keeps a per-space
	/// maximum anyway, and collapsing duplicates here would hide how much of the archive each
	/// carrier accounts for — the number that shows a carrier has stopped covering what it used to.
	/// </summary>
	internal static (List<ulong> Ids, int ItemRows, int Carried) Of(
		GameCheckpoint checkpoint,
		IReadOnlyList<SavedCharacter> characters)
	{
		var ids = new List<ulong>(checkpoint.Items.Count);
		foreach (var item in checkpoint.Items)
		{
			ids.Add(item.Identity.InstanceId);
		}

		var itemRows = ids.Count;
		var carried = 0;
		foreach (var character in characters)
		{
			carried += Collect(character.Character.Items, ids);
		}

		return (ids, itemRows, carried);
	}

	/// <summary>One character's carried ids, a container's contents included — recursively, because the restore's own re-stamp is recursive (<c>ItemStateCodec.RestoreContents</c>).</summary>
	private static int Collect(IReadOnlyList<CharacterItemMsg> items, List<ulong> ids)
	{
		var collected = 0;
		foreach (var item in items)
		{
			ids.Add(item.InstanceId);
			collected++;
			collected += Collect(item.Contents, ids);
		}

		return collected;
	}
}
