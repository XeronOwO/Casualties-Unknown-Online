using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// One native inventory intent, keyed by authoritative instance ids. The
/// viewer's client produces it from the game's own drag pipeline (the release
/// window captures the mutation call the native branch made), the host
/// validates permission, membership, ownership and the destination body and
/// forwards it, and the owner's client replays the native call on the real
/// objects so the game's own guards, animations and sounds are the single
/// implementation. The same payload rides both hops: the request carries it
/// guest → host, the forward carries it host → owner.
/// </summary>
[ProtoContract]
public sealed class RemoteInventoryIntentMsg
{
	[ProtoMember(1)]
	public RemoteInventoryIntentKind Kind { get; set; }

	/// <summary>The SteamId of the player whose carried inventory the intent operates on.</summary>
	[ProtoMember(2)]
	public ulong OwnerSteamId { get; set; }

	/// <summary>The stable instance id of the item the native branch acted on (0 = unbound, never operable).</summary>
	[ProtoMember(3)]
	public ulong ItemInstanceId { get; set; }

	/// <summary>The destination container instance id for <see cref="RemoteInventoryIntentKind.MoveIntoContainer"/>; 0 when not used.</summary>
	[ProtoMember(4)]
	public ulong TargetContainerInstanceId { get; set; }

	private int _slotSelection;

	/// <summary>
	/// Wire representation of the destination body slot. Zero means "no slot
	/// operand"; a non-negative value is stored as <c>slotIndex + 1</c> so slot 0
	/// — a hand — is not omitted by protobuf's default-zero rule. The encoding is
	/// the one <c>PlayerHealRequestMsg.LimbSelection</c> already carries.
	/// </summary>
	[ProtoMember(5)]
	public int SlotSelection
	{
		get => _slotSelection;
		set => _slotSelection = value;
	}

	/// <summary>The destination body-slot index for <see cref="RemoteInventoryIntentKind.PickUpToSlot"/> and <see cref="RemoteInventoryIntentKind.SwapSlots"/>; -1 when not used.</summary>
	public int TargetSlotIndex
	{
		get => _slotSelection <= 0 ? -1 : _slotSelection - 1;
		set => _slotSelection = value >= 0 ? value + 1 : 0;
	}

	/// <summary>The destination body for <see cref="RemoteInventoryIntentKind.TransferToBody"/> — the requester's own SteamId; 0 when not used.</summary>
	[ProtoMember(6)]
	public ulong TargetBodySteamId { get; set; }

	private int _limbSelection;

	/// <summary>
	/// Wire representation of the target limb. Zero means "no limb operand"; a
	/// non-negative value is stored as <c>limbIndex + 1</c> so limb 0 is not
	/// omitted by protobuf's default-zero rule.
	/// </summary>
	[ProtoMember(7)]
	public int LimbSelection
	{
		get => _limbSelection;
		set => _limbSelection = value;
	}

	/// <summary>The target limb for <see cref="RemoteInventoryIntentKind.ApplyToLimb"/>; -1 = the landed flow's most-injured auto pick.</summary>
	public int TargetLimbIndex
	{
		get => _limbSelection <= 0 ? -1 : _limbSelection - 1;
		set => _limbSelection = value >= 0 ? value + 1 : 0;
	}

	/// <summary>
	/// The liquid quantity <see cref="RemoteInventoryIntentKind.Drain"/> removes,
	/// in the container's own units (<c>WaterContainerItem.Drain</c>). Zero is a
	/// legal value — the native tick still runs on a frame whose delta time made
	/// the amount zero — and it needs no <c>value + 1</c> encoding: unlike a slot
	/// or a limb index there is no "no operand" state to tell it apart from, so
	/// protobuf's default-zero rule round-trips it unchanged.
	/// </summary>
	[ProtoMember(8)]
	public float Amount { get; set; }

	/// <summary>
	/// The second item operand of the two-item kinds: the hit item the native
	/// branch read for <see cref="RemoteInventoryIntentKind.CombineItems"/>
	/// (<c>Body.CombineItems(target, item)</c>, where the target is the receiver)
	/// and the receiving item of
	/// <see cref="RemoteInventoryIntentKind.LoadBattery"/>. 0 when the kind takes
	/// one item only.
	/// </summary>
	[ProtoMember(9)]
	public ulong TargetItemInstanceId { get; set; }

	/// <summary>
	/// The trader's world position for
	/// <see cref="RemoteInventoryIntentKind.GiveToTrader"/> — the position key the
	/// trade domain already uses (<c>TraderSwingMsg.Position</c>) — or null when the
	/// kind takes no trader operand. The member is nullable on purpose: an absent
	/// operand must be tellable apart from a trader that really stands at (0,0).
	/// </summary>
	[ProtoMember(10)]
	public NetVector2Msg? TargetTraderPosition { get; set; }
}
