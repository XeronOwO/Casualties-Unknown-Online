using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Shared index lookup for a carried item instance inside a character snapshot.
/// Several medical operation services resolve the same slot by instance id;
/// keeping the tiny lookup in one place avoids a 5x copy and gives the family a
/// single source of truth.
/// </summary>
internal static class PlayerItemIndex
{
	internal static int Find(CharacterDataMsg data, ulong itemInstanceId)
	{
		for (var i = 0; i < data.Items.Count; i++)
		{
			if (data.Items[i].InstanceId == itemInstanceId)
			{
				return i;
			}
		}

		return -1;
	}
}
