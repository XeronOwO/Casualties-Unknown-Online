using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// An earthquake began (the host's, broadcast when ITS timer fires — quake
/// timing is synced to the host so every side shakes together). Guests show
/// the effect (Duration drives the intensity ramp) and re-align their own
/// timer to NextDelay, so the next quake fires on all sides at the same
/// moment again. Every side still breaks its own nearby region — the regions
/// UNION via the air-write relay (SetBlock(0) is idempotent, overlaps count
/// once, user mandate).
/// </summary>
[ProtoContract]
public sealed class EarthquakeStartMsg
{
	[ProtoMember(1)]
	public float Duration { get; set; } // the host's earthquakeTime (3-25 s)

	[ProtoMember(2)]
	public float NextDelay { get; set; } // the host's new earthquakeDelay (600-1750 s × run setting) — guests re-align to it
}
