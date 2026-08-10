using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → guest: every geyser's liquid type (1/2). The type is rolled ONCE per
/// geyser at generation time in GeyserScript.Start (GeyserScript.cs:12) from
/// the PUBLIC random stream — not the isolated generation stream (Start runs
/// in the coroutine's yield gaps, WorldGenRandomIsolation wraps the generator's
/// own consumption only) — so each side's copy may roll a different type. The
/// host's roll is the authority: sent on world entry (like the keypad codes)
/// and re-sent on the 60 s snapshot cycle (idempotent same-value SetValue on
/// the guest side). With the type bound as a generation-time initial condition,
/// the GeyserActivated event carries no liquidType — its Extra stays 0.
/// </summary>
[ProtoContract]
public sealed class GeyserStateSnapshotMsg
{
	[ProtoMember(1)]
	public List<GeyserStateEntryMsg> Geysers { get; set; } = [];
}
