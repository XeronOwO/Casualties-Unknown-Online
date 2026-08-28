using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The world-item snapshot surface (host → guest): the full-table snapshots —
/// one-shot on world entry (late joiner / reconnect) and the periodic keyframe
/// over the unreliable channel (drops are harmless — the next tick overwrites;
/// settled items get their drifted positions re-aligned) — plus the layer
/// modifier projection that rides them. Read-only over the table (injected as
/// a narrow delegate, the table state stays with ItemService — user rule:
/// state belongs to its owner); PublishGeneratedItems stays with the table.
/// Split out of ItemService when the 600-line gate demanded it.
/// </summary>
public sealed class ItemSnapshotService(
	ISessionControl session,
	Func<IReadOnlyCollection<WorldItem>> worldItems,
	IKernelProtocolControl kernelProtocol,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly Func<IReadOnlyCollection<WorldItem>> _worldItems = worldItems;
	private readonly IKernelProtocolControl _kernelProtocol = kernelProtocol;
	private readonly ILogger _log = log;

	/// <summary>
	/// The world's current layer modifier (index into LayerModifier.availableModifiers,
	/// -1 = none), riding the world-item snapshots so a world entry outside a
	/// generation (solo→lobby conversion, mid-session join) still receives the
	/// host's modifier. The values are a projection of the world state — the
	/// adapter refreshes them when a generation finishes (GeneratedItemAuthority).
	/// The modifier itself is applied by the adapter's LayerModifierSync.
	/// </summary>
	public int LayerModifierIndex { get; set; } = -1;

	/// <summary>The random stream state at the entry of the host's modifier
	/// decision (non-null when a modifier was rolled) — rides alongside
	/// <see cref="LayerModifierIndex"/> so the guests replay the decision draws
	/// before the modifier's Initialize (identical world effects, see
	/// WorldItemsSnapshotMsg.LayerModifierRandomState).</summary>
	public byte[]? LayerModifierRandomState { get; set; }

	public event Action<IReadOnlyList<WorldItem>, int, byte[]?>? ItemSnapshotReceived;

	public event Action<IReadOnlyList<ItemSnapshotEntryMsg>, int, byte[]?>? WorldItemsSnapshotReceived;
	/// <summary>Session ended: the layer-modifier projection belongs to the previous world — the next run publishes its own.</summary>
	public void ResetForSessionEnd()
	{
		LayerModifierIndex = -1;
		LayerModifierRandomState = null;
	}


	public void FireItemSnapshotReceived(ulong sender, IReadOnlyList<WorldItem> items, int layerModifierIndex, byte[]? layerModifierRandomState)
	{
		_log.LogInformation("World-item snapshot received ({Count} items).", items.Count);
		ItemSnapshotReceived?.Invoke(items, layerModifierIndex, layerModifierRandomState);
	}

	public void FireWorldItemsSnapshotReceived(ulong sender, IReadOnlyList<ItemSnapshotEntryMsg> items, int layerModifierIndex, byte[]? layerModifierRandomState)
		=> WorldItemsSnapshotReceived?.Invoke(items, layerModifierIndex, layerModifierRandomState);

	public void SendItemSnapshot(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host || _worldItems().Count == 0)
		{
			return;
		}

		_kernelProtocol.SendItemStateStreamTo(
			targetSteamId,
			[.. _worldItems().Select(WireItemStateMapper.ToWire)],
			WirePayloadType.ItemSnapshotStream,
			reliable: true,
			layerModifierIndex: LayerModifierIndex + 1,
			layerModifierRandomState: LayerModifierRandomState);
		_log.LogInformation("Sent world-item snapshot ({Count} items) to {Peer}.", _worldItems().Count, targetSteamId);
	}

	/// <summary>
	/// Host only: periodically re-send the full table over the unreliable
	/// channel — drops are harmless (the next tick overwrites; the receiver
	/// reconciles), and settled items get their drifted positions re-aligned.
	/// </summary>
	public void SendPeriodicItemSnapshot()
	{
		if (_session.Role != SessionRole.Host || _worldItems().Count == 0 || !_session.SessionActive)
		{
			return;
		}

		_kernelProtocol.BroadcastItemStateStream(
			[.. _worldItems().Select(WireItemStateMapper.ToWire)],
			WirePayloadType.ItemSnapshotStream,
			reliable: false,
			layerModifierIndex: LayerModifierIndex + 1,
			layerModifierRandomState: LayerModifierRandomState);
	}
}
