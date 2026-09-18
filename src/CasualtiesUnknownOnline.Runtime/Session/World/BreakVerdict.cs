namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>The host's verdict on a guest's break report — what the adapter does with it.</summary>
internal enum Verdict
{
	/// <summary>The sender's applied air-write proved first-writer: register and relay the drops.</summary>
	Fresh,

	/// <summary>This sender's break for this cell was already accepted and the relay is being re-sent (the guest's fallback, or a lost acknowledgement): re-register idempotently and re-relay, never refuse.</summary>
	Repeat,

	/// <summary>Not this sender's break: roll the drops back on the reporter (first-writer-wins).</summary>
	Refused,
}
