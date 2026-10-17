using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → requester: the host's answer to one <see cref="PlayerInventoryGiveRequestMsg"/>.
/// The member's verb is applied on the host, so the operator's own console has
/// nothing to print until this arrives — and a refusal has to arrive there too,
/// or a closed permission would look exactly like a typo that did nothing.
///
/// <para>
/// <see cref="Answer"/> is the operator-facing sentence: it shares the host's
/// REASON wording with the line the host logs, but it is not that line — the log
/// carries the actor, the target and the `[Give]` prefix, while the answer carries
/// the verdict and the reason only. Two voiceings for one reason would be how the
/// two ends stop agreeing; two SENTENCES are not.
/// </para>
/// </summary>
[ProtoContract]
public sealed class PlayerInventoryGiveResultMsg
{
	/// <summary>The body the request named.</summary>
	[ProtoMember(1)]
	public ulong TargetSteamId { get; set; }

	/// <summary>The definition the request named (echoed even when it never resolved).</summary>
	[ProtoMember(2)]
	public string DefinitionId { get; set; } = string.Empty;

	/// <summary>The created item's instance id, or 0 when nothing was created.</summary>
	[ProtoMember(3)]
	public ulong ItemInstanceId { get; set; }

	/// <summary>The slot the item was placed in, or -1 when nothing was placed.</summary>
	[ProtoMember(4)]
	public int SlotIndex { get; set; } = -1;

	/// <summary>True when the item was created and delivered; false when the answer is a refusal.</summary>
	[ProtoMember(5)]
	public bool Applied { get; set; }

	/// <summary>The sentence the member's console prints; the reason wording is the host's, the sentence is not the log line.</summary>
	[ProtoMember(6)]
	public string Answer { get; set; } = string.Empty;
}
