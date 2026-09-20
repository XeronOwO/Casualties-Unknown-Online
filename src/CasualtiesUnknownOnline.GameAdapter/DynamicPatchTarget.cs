using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// One dynamic patch target: a game method whose type is INTERNAL to the game
/// assembly, so it is reached by reflection rather than through a
/// <c>[HarmonyPatch]</c> attribute. The declared table
/// (<see cref="DynamicPatchInstaller.Targets"/>) is the single source for BOTH
/// the installer that binds these methods and the hand-declared contract rows
/// <c>PatchInventory.BuildContracts</c> hands the contract tests and the
/// capability catalog — the two used to be written out separately, which is a
/// silent hook gap waiting to happen (a row nothing binds, or a binding nothing
/// guards).
/// </summary>
/// <param name="TypeName">The game type's name in the game assembly (internal, no compile-time reference).</param>
/// <param name="MethodName">The target method's name.</param>
/// <param name="NonPublic">True for a private target (<c>CrystalUnstable.StartTimer</c> is private).</param>
/// <param name="PatchClass">The patch class whose methods are bound (its simple name; the contract rows carry it with the dynamic pseudo suffix).</param>
/// <param name="Prefix">The prefix method's name, or null when the target takes a postfix only.</param>
/// <param name="Postfix">The postfix method's name, or null when the target takes a prefix only.</param>
/// <param name="MissingMessage">How the miss is worded — the four shapes the installer always logged.</param>
/// <param name="AbortsRemaining">True when a miss stops the install of the remaining targets (the crystal family's original flow).</param>
/// <param name="Reason">The reader-facing consequence ("the fragile-crystal break sync is off").</param>
/// <param name="PatchParameters">The patch methods' name-matched parameter names — the contract rows' fact.</param>
internal sealed record DynamicPatchTarget(
	string TypeName,
	string MethodName,
	bool NonPublic,
	string PatchClass,
	string? Prefix,
	string? Postfix,
	DynamicPatchTarget.MissingMessageShape MissingMessage,
	bool AbortsRemaining,
	string Reason,
	IReadOnlyList<string> PatchParameters)
{
	/// <summary>
	/// The miss wording each target has always logged: <see cref="ByResolution"/>
	/// names the type when the type is gone and type.method when only the method
	/// is, <see cref="TypeOnly"/> always names the type, <see cref="MethodOnly"/>
	/// always names type.method as a method, and <see cref="TypeAndMethod"/>
	/// always names type.method as a target.
	/// </summary>
	internal enum MissingMessageShape
	{
		ByResolution,
		TypeOnly,
		MethodOnly,
		TypeAndMethod,
	}
}
