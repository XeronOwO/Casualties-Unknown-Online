using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → requester: the host's answer to one <see cref="WorldSummonRequestMsg"/>. The member's verb is applied
/// on the host, so the operator's own console has nothing to print until this arrives — and a refusal has to
/// arrive there too, or a closed permission would look exactly like a typo that did nothing.
///
/// <para>
/// <see cref="Answer"/> is the operator-facing sentence: it shares the host's REASON wording with the line the
/// host logs, but it is not that line — the log carries the actor and the <c>[Summon]</c> prefix, while the answer
/// carries the verdict, the created object and the position it really used. The created object itself does not
/// travel here: an entity or an animal rides its own creation path, and a loose item entity rides the item
/// domain's spawn row — the same row the give ladder's drop makes — so the member's scene materializes it from
/// that report and not from this answer.
/// </para>
/// </summary>
[ProtoContract]
public sealed class WorldSummonResultMsg
{
	/// <summary>The prefab the request named (echoed even when it never resolved).</summary>
	[ProtoMember(1)]
	public string PrefabId { get; set; } = string.Empty;

	/// <summary>The created ITEM entity's instance id, or 0 when nothing was created or the created object is an entity (entities carry no instance id).</summary>
	[ProtoMember(2)]
	public ulong ItemInstanceId { get; set; }

	/// <summary>True when the object was created; false when the answer is a refusal.</summary>
	[ProtoMember(3)]
	public bool Applied { get; set; }

	/// <summary>The sentence the member's console prints; the reason wording is the host's, the sentence is not the log line.</summary>
	[ProtoMember(4)]
	public string Answer { get; set; } = string.Empty;

	/// <summary>
	/// The answer as catalogue PARTS, so the member's console prints it in the member's own language — one line per part. Empty
	/// when the shape was not converted (the console then prints <see cref="Answer"/>).
	/// </summary>
	[ProtoMember(5)]
	public List<ConsoleAnswerPartMsg> Parts { get; set; } = [];
}
