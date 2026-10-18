namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>The give claim's verdict: a credited amount and whether the claim had to be clamped (the answer), or a by-name refusal with no credit.</summary>
internal readonly record struct GiveItemVerdict(int Credit, bool ClaimExceeded, string? Refusal, string Detail)
{
	internal static GiveItemVerdict Refused(string reason) => new(0, false, reason, reason);

	internal static GiveItemVerdict Judged(int credit, bool claimExceeded, string detail) => new(credit, claimExceeded, null, detail);
}
