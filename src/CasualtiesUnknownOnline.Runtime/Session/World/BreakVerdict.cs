namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>The host's verdict on a guest's break report — what the adapter does with it.</summary>
internal enum Verdict
{
	/// <summary>The sender's applied air-write proved first-writer: register and relay the drops.</summary>
	Fresh,

	/// <summary>This sender's break for this cell was already accepted and the relay is being re-sent (the guest's fallback, or a lost acknowledgement): re-register idempotently and re-relay, never refuse.</summary>
	Repeat,

	/// <summary>
	/// The report is provably THIS generation's and names a cell this side still
	/// holds: the sender's air write never arrived (its report was lost with the
	/// drops'), so the standing cell is not evidence of another writer — it is the
	/// shape a lost air write leaves. Register and relay the drops, then apply and
	/// relay the break's air transition (audit gap W1's drop half, closed by the
	/// generation stamp).
	/// </summary>
	LostAirWrite,

	/// <summary>Not this sender's break: roll the drops back on the reporter (first-writer-wins).</summary>
	Refused,
}
