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
}
