using System;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → member: the host's host-rule values, ABSOLUTE — one field per rule in
/// <see cref="HostRuleSet"/>, the whole set every time, never a delta. A member that
/// missed one of these is put right by the next one, so the world-entry group, the
/// in-session repair group and a re-announcement all converge without bookkeeping.
///
/// <para>
/// It carries the rules whose values a member's client reads (the census decides
/// which, <c>HostRuleCensus</c>): its screen, its console and its own local gestures.
/// A rule only the host executes is not sent at all, and a rule that is not
/// classified does not reach this message by being forgotten.
/// </para>
///
/// <para>
/// Protobuf omits a field at its default, so an absent member reads back as the
/// default value — which is correct here precisely because the message is absolute:
/// "false" and "0" are values a host may hold, and the receiver writes every field it
/// receives over its whole stored set rather than merging what happened to arrive.
/// </para>
/// </summary>
[ProtoContract]
public sealed class HostRulesMsg
{
	[ProtoMember(1)]
	public bool PvpEnabled { get; set; }

	[ProtoMember(2)]
	public bool AutoContinue { get; set; }

	[ProtoMember(3)]
	public bool AllowLateJoin { get; set; }

	/// <summary>The three-way remote-backpack access level, as its enum value (allow / unconscious_or_dead / deny).</summary>
	[ProtoMember(4)]
	public int RemoteBackpackAccess { get; set; }

	[ProtoMember(5)]
	public bool AllowMemberCheatVerbs { get; set; }

	[ProtoMember(6)]
	public bool WidenRunSettings { get; set; }

	[ProtoMember(7)]
	public float PiggybackWeightMultiplier { get; set; }

	/// <summary>The three-way parity level, as its enum value (allow / warn / require).</summary>
	[ProtoMember(8)]
	public int NativeBindingParity { get; set; }

	[ProtoMember(9)]
	public bool SaveInventory { get; set; }

	[ProtoMember(10)]
	public bool ReviveFromTrader { get; set; }

	[ProtoMember(11)]
	public bool ReviveOnNextLevel { get; set; }

	[ProtoMember(12)]
	public bool Permadeath { get; set; }

	[ProtoMember(13)]
	public bool RadiationLineStopsAboveTheFloor { get; set; }

	/// <summary>The wire form of one rule set (the sender's mapping).</summary>
	public static HostRulesMsg From(HostRuleSet set) => new()
	{
		PvpEnabled = set.PvpEnabled,
		AutoContinue = set.AutoContinue,
		AllowLateJoin = set.AllowLateJoin,
		RemoteBackpackAccess = (int)set.RemoteBackpackAccess,
		AllowMemberCheatVerbs = set.AllowMemberCheatVerbs,
		WidenRunSettings = set.WidenRunSettings,
		PiggybackWeightMultiplier = set.PiggybackWeightMultiplier,
		NativeBindingParity = (int)set.NativeBindingParity,
		SaveInventory = set.SaveInventory,
		ReviveFromTrader = set.ReviveFromTrader,
		ReviveOnNextLevel = set.ReviveOnNextLevel,
		Permadeath = set.Permadeath,
		RadiationLineStopsAboveTheFloor = set.RadiationLineStopsAboveTheFloor,
	};

	/// <summary>The rule set this message carries (the receiver's mapping).</summary>
	public HostRuleSet ToSet() => new(
		PvpEnabled,
		AutoContinue,
		AllowLateJoin,
		(RemoteBackpackAccess)RemoteBackpackAccess,
		AllowMemberCheatVerbs,
		WidenRunSettings,
		PiggybackWeightMultiplier,
		(NativeBindingParity)NativeBindingParity,
		SaveInventory,
		ReviveFromTrader,
		ReviveOnNextLevel,
		Permadeath,
		RadiationLineStopsAboveTheFloor);

	/// <summary>
	/// Why this announcement cannot be applied, or null when it is well formed. The
	/// fields that carry a CLOSED set are checked: a level this build does not know is
	/// refused WHOLE rather than clamped, because the receiver keeps the set it holds (or
	/// none) and names the value — a member that quietly read a neighbouring level would
	/// render the wrong rule, and both the Admin summary and the member card index the
	/// level straight into their label list.
	/// </summary>
	public string? Malformed()
	{
		if (!Enum.IsDefined(typeof(NativeBindingParity), NativeBindingParity))
		{
			return $"NativeBindingParity={NativeBindingParity} is not a level this build knows (allow / warn / require)";
		}

		return Enum.IsDefined(typeof(RemoteBackpackAccess), RemoteBackpackAccess)
			? null
			: $"RemoteBackpackAccess={RemoteBackpackAccess} is not a level this build knows (allow / unconscious_or_dead / deny)";
	}
}
