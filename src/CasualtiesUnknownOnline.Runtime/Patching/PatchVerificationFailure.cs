namespace CasualtiesUnknownOnline.Runtime.Patching;

/// <summary>
/// One hook that did NOT land, as a fact rather than a log line: the patch
/// class a reader recognizes, the identity the adapter's capability catalog
/// keys on, the human-readable violation, and whether the install gate counts
/// it. The runtime verification (PatchInventory.VerifyMissing) and the adapter
/// capability probe consume the same rows, so the gate's verdict and the
/// per-capability report can never be built from two different measurements.
///
/// Stage 1 keeps the all-or-nothing install rule: the attributed patch
/// contracts block (exactly the failures VerifyMissing used to return as text),
/// while the probe-only rows — the hand-declared dynamic targets and the
/// declared game members — are reported with <see cref="BlocksInstall"/> false.
/// Stage 2 splits the blocking decision by the capability's Required/Optional
/// class instead of changing where the facts come from.
/// </summary>
internal sealed class PatchVerificationFailure
{
	internal PatchVerificationFailure(string patchClass, string patchClassType, string detail, bool blocksInstall)
	{
		PatchClass = patchClass;
		PatchClassType = patchClassType;
		Detail = detail;
		BlocksInstall = blocksInstall;
	}

	/// <summary>The patch class the log and the report name — a simple class name, or the hand-declared dynamic rows' <c>"(dynamic)"</c> pseudo name.</summary>
	internal string PatchClass { get; }

	/// <summary>
	/// The capability lookup key: a patch class's full name (the CLR spelling —
	/// <c>Outer+Inner</c> for nested classes), or the pseudo name of a dynamic
	/// row. Full rather than simple because the assembly's 205 contracts share
	/// only 201 simple names.
	/// </summary>
	internal string PatchClassType { get; }

	/// <summary>The violation text — the exact wording the install log prints, so a reader can match the two.</summary>
	internal string Detail { get; }

	/// <summary>True when this failure is on the install gate (the attributed patch contracts); see the class remarks.</summary>
	internal bool BlocksInstall { get; }
}
