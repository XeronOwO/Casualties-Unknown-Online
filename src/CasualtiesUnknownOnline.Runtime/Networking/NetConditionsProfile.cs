namespace CasualtiesUnknownOnline.Runtime.Networking;

/// <summary>
/// A diagnostics-only profile that makes the IP-direct transport behave like a bad network: every
/// outbound payload is delayed by <see cref="LatencyMs"/> ± <see cref="JitterMs"/>, and an UNRELIABLE
/// payload is dropped with <see cref="LossPercent"/>. A reliable payload is never dropped — that
/// would be a bug in the shim, not a test. Off by default, never part of the wire, and never
/// persisted: a test run sets it through <see cref="INetConditionsControl"/> and names the profile in
/// its verdict, so "the item landed at 150 ms and 20% loss" can be told apart from "it landed".
/// </summary>
public readonly record struct NetConditionsProfile(bool Enabled, int LatencyMs, int JitterMs, int LossPercent)
{
	public const int MaxLatencyMs = 5000;
	public const int MaxJitterMs = 2000;

	public static NetConditionsProfile Off => new(false, 0, 0, 0);

	/// <summary>True when the profile changes delivery at all (enabled with latency or loss).</summary>
	public bool Shapes => Enabled && (LatencyMs > 0 || LossPercent > 0);

	public override string ToString() => Enabled
		? $"{LatencyMs} ms +/- {JitterMs} ms, {LossPercent}% loss of unreliable payloads"
		: "off";
}
