using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// ONE crafting operation's complete terminal state — the "one operation = one
/// report" rule: the game splits a craft across several method calls (consume
/// materials → spawn products → first-craft bonus), and per-call reports
/// materialized ghost intermediates (a consumed material reported as "dropped").
/// The sender merges everything into this single frame; the receiver applies
/// per entry and relays the WHOLE report (never decomposed into per-entry
/// broadcasts).
///
/// Direction: guest → host report; host → guest broadcast relay (source
/// excluded). The first-craft bonus and failure-branch injuries deliberately
/// ride the 1 Hz CharacterData snapshot instead of this frame.
/// </summary>
[ProtoContract]
public sealed class CraftReportMsg
{
	[ProtoMember(1)]
	public CraftOperationKind Kind { get; set; } // Craft = 0 — the wire default, omission transparent; diagnostic only

	[ProtoMember(2)]
	public List<CraftEntryMsg> Entries { get; set; } = []; // consumed (Destroyed) and changed (Changed) items, post-state digests

	[ProtoMember(3)]
	public List<CharacterItemMsg> Products { get; set; } = []; // the crafted items entering the crafter's inventory (full captures, slot rides SlotIndex)

	/// <summary>Who performed the operation — STAMPED BY THE HOST before relaying (the transport sender is the trusted fact; the report side leaves it 0): the relay excludes the source, so the receivers need the id to update the right clone's fact table.</summary>
	[ProtoMember(4)]
	public ulong OwnerSteamId { get; set; }
}
