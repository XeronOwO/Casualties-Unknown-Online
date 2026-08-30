using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Protocol.Wire;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Guest-side item snapshot stream receiver: applies the event-version gate
/// (kernel global revision + per-payload sequence) and forwards fresh full-table
/// keyframes to the snapshot service callbacks. Split out of
/// <see cref="ItemService"/> so the item coordinator stays a facade and the
/// unreliable-snapshot ordering policy is independently testable.
/// </summary>
internal sealed class ItemSnapshotStreamReceiver(
	ISessionControl session,
	ItemKernelAuthority authority,
	ILogger log,
	Action<IReadOnlyList<WorldItem>, int, byte[]?> onItemSnapshot,
	Action<IReadOnlyList<WorldItem>, int, byte[]?> onWorldItemsSnapshot)
{
	private readonly ISessionControl _session = session;
	private readonly ItemKernelAuthority _authority = authority;
	private readonly ILogger _log = log;
	private readonly Action<IReadOnlyList<WorldItem>, int, byte[]?> _onItemSnapshot = onItemSnapshot;
	private readonly Action<IReadOnlyList<WorldItem>, int, byte[]?> _onWorldItemsSnapshot = onWorldItemsSnapshot;
	private uint _lastItemSnapshotSeq;
	private uint _lastWorldItemsSnapshotSeq;

	public void Handle(ulong hostSteamId, WirePayloadType payloadType, WireStateStream stream)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		switch (payloadType)
		{
			case WirePayloadType.ItemSnapshotStream:
				if (!IsFreshSnapshot(stream, ref _lastItemSnapshotSeq))
				{
					return;
				}

				_onItemSnapshot(
					[.. stream.ItemStates.Select(WireItemStateMapper.ToWorldItem)],
					stream.LayerModifierIndex,
					stream.LayerModifierRandomState);
				break;
			case WirePayloadType.WorldItemsSnapshotStream:
				if (!IsFreshSnapshot(stream, ref _lastWorldItemsSnapshotSeq))
				{
					return;
				}

				_onWorldItemsSnapshot(
					[.. stream.ItemStates.Select(WireItemStateMapper.ToWorldItem)],
					stream.LayerModifierIndex,
					stream.LayerModifierRandomState);
				break;
		}
	}

	public void Reset()
	{
		_lastItemSnapshotSeq = 0;
		_lastWorldItemsSnapshotSeq = 0;
	}

	/// <summary>
	/// Event-version gate for the unreliable item snapshot keyframe family.
	/// A full snapshot must never roll back a newer committed kernel event:
	/// <see cref="WireStateStream.BaseGlobalRevision"/> is compared against the
	/// guest's applied kernel revision, and <see cref="WireStateStream.Seq"/>
	/// orders snapshots among themselves for the duplicate/out-of-order case.
	/// A zero <see cref="WireStateStream.Seq"/> is accepted as a legacy
	/// unsequenced frame (the production host always assigns one).
	/// </summary>
	private bool IsFreshSnapshot(WireStateStream stream, ref uint lastSeq)
	{
		var currentRevision = _authority.CurrentGlobalRevision;
		if (stream.BaseGlobalRevision < currentRevision)
		{
			_log.LogDebug("[ItemSnapshot] dropped stale snapshot seq {Seq} (kernel {Base}, current {Current}).",
				stream.Seq, stream.BaseGlobalRevision, currentRevision);
			return false;
		}

		if (stream.Seq != 0 && stream.Seq <= lastSeq)
		{
			_log.LogDebug("[ItemSnapshot] dropped duplicate/out-of-order snapshot seq {Seq} (last {Last}).",
				stream.Seq, lastSeq);
			return false;
		}

		if (stream.Seq != 0)
		{
			lastSeq = stream.Seq;
		}

		return true;
	}
}
