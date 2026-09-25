using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>One captured native call, as the owner must replay it.</summary>
internal readonly record struct RemoteDragIntent(
	RemoteInventoryIntentKind Kind,
	ulong ItemInstanceId,
	ulong TargetContainerInstanceId,
	int TargetSlotIndex,
	ulong TargetBodySteamId,
	int TargetLimbIndex)
{
	/// <summary>The drained amount for <see cref="RemoteInventoryIntentKind.Drain"/> — that kind's operand; 0 for every other kind.</summary>
	internal float Amount { get; init; }

	/// <summary>
	/// The second item operand of the two-item kinds
	/// (<see cref="RemoteInventoryIntentKind.CombineItems"/> — the receiver — and
	/// <see cref="RemoteInventoryIntentKind.LoadBattery"/> — the receiving item);
	/// 0 for every kind that takes one item only.
	/// </summary>
	internal ulong TargetItemInstanceId { get; init; }

	/// <summary>
	/// The trader's world position for
	/// <see cref="RemoteInventoryIntentKind.GiveToTrader"/>; null for every kind
	/// that takes no trader operand, which is every other kind.
	/// </summary>
	internal NetVector2Msg? TargetTraderPosition { get; init; }
}
