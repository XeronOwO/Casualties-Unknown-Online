using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// ONE hostile trader swing presentation (TraderScript.Swing's
/// <c>attackAnimation</c> instantiation + swing sound). The acting side's
/// local trader already ran the swing against that side's local player; this
/// event lets the other members replay the same animation on their
/// same-position trader. Star semantics: guest → host report, host fires the
/// event and relays to the other members (source excluded); host → guest relay
/// fires the replay. One swing = one message; a lost one-shot visual is
/// acceptable presentation degradation.
/// </summary>
[ProtoContract]
public sealed class TraderSwingMsg
{
	/// <summary>The trader's world position (the position key used by the trade domain).</summary>
	[ProtoMember(1)]
	public NetVector2Msg Position { get; set; } = new();

	/// <summary>The normalized world-space direction from the trader's torso to the attacked player.</summary>
	[ProtoMember(2)]
	public NetVector2Msg Direction { get; set; } = new();

	/// <summary>The Resources name of the attack-animation prefab the source instantiated (empty when unavailable — the receiver falls back to its own local trader's field).</summary>
	[ProtoMember(3)]
	public string Prefab { get; set; } = "";
}
