using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>One captured native call, as the owner must replay it.</summary>
internal readonly record struct RemoteDragIntent(
	RemoteInventoryIntentKind Kind,
	ulong ItemInstanceId,
	ulong TargetContainerInstanceId,
	int TargetSlotIndex,
	ulong TargetBodySteamId,
	int TargetLimbIndex);
