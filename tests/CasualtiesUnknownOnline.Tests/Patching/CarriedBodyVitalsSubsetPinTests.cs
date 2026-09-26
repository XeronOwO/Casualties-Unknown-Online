using System;
using System.IO;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The mechanism pin for a dead or unconscious carried body's own client: the
/// whole native <c>Body.Update</c> stays skipped — the carry relation owns that
/// body's pose, its physics, its ground contact and its sounds — but the VITALS
/// stages of the pass must actually run, in the game's own order, with the
/// rigidbodies frozen on both sides of them, because the body is still a body
/// and the game advances those stages for every local body. The limb half of the
/// same family is pinned here too: only a remote clone's <c>Limb.Update</c> is
/// skipped, so a carried corpse's wound/infection state keeps advancing.
///
/// It reads the two adapter patch files as text and lives in its OWN class on
/// purpose: the rule's matrix (<c>CarriedBodySimulationTests</c>) references the
/// Runtime rule, while this pin compiles against the Game Adapter's patch files
/// and therefore fails there for the real reason — the red is reproducible on a
/// tree whose <c>BodyUpdatePatch</c> is at HEAD, not a missing-type compile
/// error. Comment-only lines are dropped and CRLF is folded to LF before
/// matching, so a comment naming an API can neither satisfy nor break a pin and
/// the pin does not depend on a file's line endings.
///
/// What the matchers prove: the call site is the rule-gated block and nothing
/// else (guard, freeze, subset call, freeze); that block sits on the SKIPPED
/// branch (the prefix's single whole-pass <c>return true;</c> precedes the gate,
/// and the presentation path's single <c>return false;</c> follows it); the
/// subset METHOD body is exactly the five stage calls and nothing else, once
/// each, in the game's own order; no stage string occurs anywhere else in the
/// file (so a call hoisted into the proxy path is caught); each stage's
/// <c>MethodInfo</c> is resolved from the API it is named after; the four
/// excluded stage names appear nowhere outside comments; the limb gate asks the
/// limb rule; and the physics step still skips the pinned body. The
/// counter-examples — the pre-fix body with no stage at all, a hand-rolled
/// guard, one freeze instead of two, a dropped or duplicated or re-ordered
/// stage, <c>HandleVisuals</c> pulled into the subset, a stage hoisted out of the
/// method, the subset block moved into the whole-pass branch, a whole-pass branch
/// that no longer returns, a stage resolved from the wrong API, an excluded stage
/// pulled in, the pre-fix limb gate, a limb gate that asks the carry state, a
/// physics step that no longer skips the pinned body — are asserted rejected
/// below.
///
/// What the matchers cannot see: whether the prefix runs at all, and an early
/// <c>return;</c> inserted in the prefix BEFORE the gate — both are runtime
/// facts, and the real proof is the user's two-client session. That residual is
/// declared in the cycle's self-check rather than papered over.
/// </summary>
public class CarriedBodyVitalsSubsetPinTests
{
	[Fact]
	public void ThePinnedCarriedBodyRunsExactlyTheVitalsStagesInOrder()
	{
		Assert.True(
			TrySubsetBlock(StrippedPatchSource(), out _),
			$"the vitals-subset gate `{SubsetGate}` is not in BodyUpdatePatch.cs");
		Assert.True(VitalsSubsetShapeHolds(StrippedPatchSource()), VitalsSubsetExpectation);
	}

	[Fact]
	public void TheLimbPassRunsForEveryLocalBodyAndSkipsOnlyAClone()
	{
		Assert.True(
			LimbGateIsRuled(StrippedFamilySource()),
			"Limb.Update must run for every LOCAL body — a carried corpse's wound/infection state keeps advancing — and be skipped only for a remote clone (CarriedBodySimulation.RunsLimbSimulation)");
	}

	[Fact]
	public void ThePhysicsStepStillSkipsThePinnedCarriedBody()
	{
		Assert.True(
			PhysicsStepIsRuled(StrippedFamilySource()),
			"Body.FixedUpdate must still be skipped for the pinned carried body: the carry relation owns its physics, and running the game's own integrator under a teleported root is the twitch family this rule exists to remove");
	}

	// --- the matchers' negative samples: the rejected shapes, and (where the shape
	// is the live text itself) the proof that the sample really differs from it, so
	// a text drift cannot silently turn a sample into a tautology ---

	[Fact]
	public void ThePinRejectsThePreFixPatchThatRunsNoVitalsStage()
	{
		// HEAD's BodyUpdatePatch: the whole pass is skipped for the pinned body and
		// nothing replaces it — the frozen-vitals defect this cycle fixes
		var source = StrippedPatchSource();
		Assert.True(TrySubsetBlock(source, out var block), "the live source must carry the vitals-subset block");
		var preFix = source.Replace(block, string.Empty);
		Assert.NotEqual(source, preFix);
		Assert.False(VitalsSubsetShapeHolds(preFix));
	}

	[Fact]
	public void ThePinRejectsASubsetThatIsNotRuled()
	{
		// the adapter deciding by itself instead of asking the rule — the one-owner
		// contract every body patch of this family follows
		var mutated = MutateSubsetBlock(SubsetGate, "if (RunsForAnySkippedBody(");
		Assert.False(VitalsSubsetShapeHolds(mutated));
	}

	[Fact]
	public void ThePinRejectsASubsetThatFreezesOnlyAfterwards()
	{
		// the leading freeze removed: the stages run on rigidbodies that the
		// placement may not have frozen yet this frame
		var mutated = MutateSubsetBlock(
			"\t\t\tFreezeRigidbodies(__instance);\n\t\t\tRunVitalsSubset(__instance);",
			"\t\t\tRunVitalsSubset(__instance);");
		Assert.False(VitalsSubsetShapeHolds(mutated));
	}

	[Fact]
	public void ThePinRejectsASubsetThatFreezesOnlyBefore()
	{
		// the trailing freeze removed: Ragdoll() re-enables limb physics whenever it
		// does run (Body.cs:1723, and its second call site inside HandleBody is not
		// behind the standing guard), and a limb simulating under a root the
		// placement teleports every frame is the twitch family this cycle removes
		var mutated = MutateSubsetBlock(
			"\t\t\tRunVitalsSubset(__instance);\n\t\t\tFreezeRigidbodies(__instance);",
			"\t\t\tRunVitalsSubset(__instance);");
		Assert.False(VitalsSubsetShapeHolds(mutated));
	}

	[Fact]
	public void ThePinRejectsASubsetThatAlsoInvokesAnExcludedStage()
	{
		// HandlePhysics pulled into the subset: its ragdoll stand timer would
		// eventually stand a body the carry relation keeps pinned
		var mutated = MutateSubsetBlock(
			"\t\t\tRunVitalsSubset(__instance);",
			"\t\t\tRunVitalsSubset(__instance);\n\t\t\tHandlePhysicsMethod.Invoke(body, []);");
		Assert.False(VitalsSubsetShapeHolds(mutated));
	}

	[Fact]
	public void ThePinRejectsASubsetThatAlsoInvokesHandleVisuals()
	{
		// The one excluded stage whose violation is a real regression: the pinned
		// path's own presentation call folded into the "vitals" half. It is caught by
		// the subset METHOD's exact body, not by a file-wide name scan — the name
		// legitimately appears in the proxy path, so a bare-name scan cannot be used
		// here.
		var mutated = Mutate(StrippedPatchSource(), PeriodicChecksStage, PeriodicChecksStage + " HandleVisualsMethod.Invoke(body, [painkillers]);");
		Assert.False(VitalsSubsetShapeHolds(mutated));
	}

	[Fact]
	public void ThePinRejectsAStageThatIsDropped()
	{
		var mutated = Mutate(StrippedPatchSource(), PeriodicChecksStage, string.Empty);
		Assert.False(VitalsSubsetShapeHolds(mutated));
	}

	[Fact]
	public void ThePinRejectsAStageThatRunsTwice()
	{
		// the census half of the matcher: two radiation stages in one frame is not
		// this shape
		var mutated = Mutate(StrippedPatchSource(), RadiationStage, RadiationStage + " " + RadiationStage);
		Assert.False(VitalsSubsetShapeHolds(mutated));
	}

	[Fact]
	public void ThePinRejectsStageCallsHoistedOutOfTheSubsetMethod()
	{
		// the call still occurs exactly ONCE in the file, but no longer inside the
		// subset method — hoisted into the proxy path it would run for every proxied
		// body every frame while the pinned body got nothing
		var live = StrippedPatchSource();
		var mutated = live.Replace(PeriodicChecksStage, string.Empty).Replace(ProxyPathAnchor, ProxyPathAnchor + " " + PeriodicChecksStage);
		Assert.NotEqual(live, mutated);
		Assert.False(VitalsSubsetShapeHolds(mutated));
	}

	[Fact]
	public void ThePinRejectsTheStagesInTheWrongOrder()
	{
		// the game's own order (Body.cs:2574-2591) with two stages swapped: the
		// method's exact body rejects it, and a stage that reads what an earlier stage
		// wrote would run against a stale value
		var live = StrippedPatchSource();
		var body = live.IndexOf(BodyStage, StringComparison.Ordinal);
		var temperature = live.IndexOf(TemperatureStage, StringComparison.Ordinal);
		Assert.True(body > 0 && temperature > body, "the live subset must run the circulation stage before the temperature stage");
		var mutated = live.Remove(body, BodyStage.Length).Insert(temperature - BodyStage.Length + TemperatureStage.Length, BodyStage);
		Assert.NotEqual(live, mutated);
		Assert.False(VitalsSubsetShapeHolds(mutated));
	}

	[Fact]
	public void ThePinRejectsAStageResolvedFromTheWrongApi()
	{
		// the MethodInfo named HandleBody resolved from Body.HandleBodyTemperature:
		// every required statement is still present and the stages would call the
		// wrong native methods
		var mutated = Mutate(
			StrippedPatchSource(),
			"\"HandleBody\"",
			"\"HandleBodyTemperature\"");
		Assert.False(VitalsSubsetShapeHolds(mutated));
	}

	[Fact]
	public void ThePinRejectsASubsetBlockMovedIntoTheWholePassBranch()
	{
		// the subset would then run for the bodies that already run the whole native
		// pass and never for the pinned body — the defect this cycle fixes, inverted
		var live = StrippedPatchSource();
		Assert.True(TrySubsetBlock(live, out var block), "the live source must carry the vitals-subset block");
		var without = live.Replace(block, string.Empty);
		var at = WholePassReturnIndex(without);
		var mutated = without.Insert(at, block + "\n\n");
		Assert.NotEqual(live, mutated);
		Assert.False(VitalsSubsetShapeHolds(mutated));
	}

	[Fact]
	public void ThePinRejectsAWholePassBranchThatNoLongerReturns()
	{
		// the whole-pass branch's return removed: a local body would fall through into
		// the pinned presentation path instead of running the native pass
		var live = StrippedPatchSource();
		var at = WholePassReturnIndex(live);
		var mutated = live.Remove(at, ReturnTrue.Length).Insert(at, "var neverReturns = true;");
		Assert.NotEqual(live, mutated);
		Assert.False(VitalsSubsetShapeHolds(mutated));
	}

	[Fact]
	public void ThePinRejectsThePreFixLimbGate()
	{
		// HEAD's limb gate: the whole-pass rule with the carried body's own
		// alive/conscious inputs, so a carried corpse's limbs simulated nothing
		var live = StrippedFamilySource();
		var start = live.IndexOf(LimbPrefixAnchor, StringComparison.Ordinal);
		Assert.True(start > 0, $"the limb prefix anchor `{LimbPrefixAnchor}` was not found in BodyPatches.cs");
		var end = live.IndexOf("\n\t}", start, StringComparison.Ordinal);
		Assert.True(end > 0, "the limb patch class has no closing brace at one tab");
		var preFix = live.Substring(0, start) + PreFixLimbPrefix + live.Substring(end);
		Assert.NotEqual(live, preFix);
		Assert.False(LimbGateIsRuled(preFix));
	}

	[Fact]
	public void ThePinRejectsALimbGateThatAsksTheCarryState()
	{
		// the limb decision taken from the carry relation again: a carried corpse's
		// limbs stop simulating, which is exactly the defect
		var mutated = Mutate(StrippedFamilySource(), "CarriedBodySimulation.RunsLimbSimulation(", "CarriedBodySimulation.SkipsNativeSimulation(");
		Assert.False(LimbGateIsRuled(mutated));
	}

	[Fact]
	public void ThePinRejectsAPhysicsStepThatRunsForThePinnedBody()
	{
		// the physics step let through for the pinned body: the game's integrator
		// would move a body whose transform the carry placement writes every frame
		var mutated = Mutate(StrippedFamilySource(), "CarriedBodySimulation.SkipsNativeSimulation(", "CarriedBodySimulation.RunsLimbSimulation(");
		Assert.False(PhysicsStepIsRuled(mutated));
	}

	private const string SubsetGate = "if (CarriedBodySimulation.RunsVitalsSubset(";
	private const string SkipsGate = "if (!CarriedBodySimulation.SkipsNativeSimulation(";
	private const string ReturnTrue = "return true;";
	private const string ReturnFalse = "return false;";
	private const string VariableUpdatesStage = "HandleVariableUpdatesMethod.Invoke(body, []);";
	private const string BodyStage = "HandleBodyMethod.Invoke(body, [painkillers]);";
	private const string TemperatureStage = "HandleBodyTemperatureMethod.Invoke(body, [painkillers]);";
	private const string RadiationStage = "HandleRadiationSicknessMethod.Invoke(body, []);";
	private const string PeriodicChecksStage = "HandlePeriodicChecksMethod.Invoke(body, []);";
	private const string ProxyPathAnchor = "UpdateGrounded(__instance);";
	private const string PrefixAnchor = "private static bool Prefix(Body __instance)";
	private const string SubsetMethodAnchor = "private static void RunVitalsSubset(Body body)";
	private const string LimbPrefixAnchor = "private static bool Prefix(Limb __instance)";
	private const string PhysicsPrefixAnchor = "private static bool Prefix(Body __instance) =>";

	private static readonly string[] Stages =
	[
		VariableUpdatesStage,
		BodyStage,
		TemperatureStage,
		RadiationStage,
		PeriodicChecksStage,
	];

	private static readonly (string Field, string Api)[] StageApis =
	[
		("HandleVariableUpdatesMethod", "HandleVariableUpdates"),
		("HandleBodyMethod", "HandleBody"),
		("HandleBodyTemperatureMethod", "HandleBodyTemperature"),
		("HandleRadiationSicknessMethod", "HandleRadiationSickness"),
		("HandlePeriodicChecksMethod", "HandlePeriodicChecks"),
	];

	/// <summary>
	/// The stages the pinned pose, physics, ground contact and sounds must not get:
	/// their bare names must not appear in the patch file outside comments, which
	/// also catches a sixth MethodInfo being resolved for the subset.
	/// <c>HandleVisuals</c> is deliberately NOT here — it legitimately appears in the
	/// proxy path, so pulling it into the subset is caught by
	/// <see cref="ExpectedSubsetMethod"/> instead.
	/// </summary>
	private static readonly string[] ExcludedStages =
	[
		"HandlePhysics",
		"HandleGroundedState",
		"HandleSounds",
		"HandleDogWaterShaking",
	];

	private static readonly string[] MemberBoundaries =
	[
		"\n\t[HarmonyPatch",
		"\n\t\t[HarmonyPatch",
		"\n\tprivate ",
		"\n\t\tprivate ",
		"\n\tinternal ",
		"\n\t\tinternal ",
	];

	/// <summary>
	/// The ruled call site verbatim, whitespace-normalized: the rule-gated guard,
	/// then the frozen rigidbodies around the subset call, nothing else.
	/// </summary>
	private const string ExpectedSubsetBlock = """
		if (CarriedBodySimulation.RunsVitalsSubset( isRemoteClone, isCarried, __instance.alive, __instance.conscious)) { FreezeRigidbodies(__instance); RunVitalsSubset(__instance); FreezeRigidbodies(__instance); }
		""";

	/// <summary>
	/// The subset method's ENTIRE body, whitespace-normalized: the five stage calls
	/// and nothing else, in the game's own order.
	/// </summary>
	private const string ExpectedSubsetMethod = """
		private static void RunVitalsSubset(Body body) { var painkillers = body.GetComponent<Painkillers>(); HandleVariableUpdatesMethod.Invoke(body, []); HandleBodyMethod.Invoke(body, [painkillers]); HandleBodyTemperatureMethod.Invoke(body, [painkillers]); HandleRadiationSicknessMethod.Invoke(body, []); HandlePeriodicChecksMethod.Invoke(body, []); }
		""";

	private const string VitalsSubsetExpectation =
		"the pinned carried body's prefix must gate the vitals subset on CarriedBodySimulation.RunsVitalsSubset(isRemoteClone, isCarried, alive, conscious) on the branch the whole native pass has already returned from, freeze the rigidbodies, and call a subset method whose entire body is the five vital-sign stages of Body.Update in the game's own order (HandleVariableUpdates, HandleBody, HandleBodyTemperature, HandleRadiationSickness, HandlePeriodicChecks) exactly once each with each MethodInfo resolved from its own API, then freeze again — never HandlePhysics, HandleGroundedState, HandleVisuals, HandleSounds or HandleDogWaterShaking";

	/// <summary>The pre-fix limb gate body: the whole-pass rule with the carried inputs (HEAD's shape).</summary>
	private const string PreFixLimbPrefix = """
		private static bool Prefix(Limb __instance)
		{
			var body = __instance.body; // Unity object — ==
			return !CarriedBodySimulation.SkipsNativeSimulation(
				__instance.GetComponentInParent<RemoteBodyDriver>() != null,
				CarriedBodyDriver.IsCarrying(body),
				body != null && body.alive,
				body != null && body.conscious);
		}
		""";

	private static bool VitalsSubsetShapeHolds(string source)
	{
		// The call site: the rule-gated block and nothing else.
		if (!TrySubsetBlock(source, out var block) || Normalize(block) != ExpectedSubsetBlock)
		{
			return false;
		}

		// The call site sits on the SKIPPED branch: the prefix has exactly one
		// whole-pass return and exactly one presentation-path return, and the gate
		// sits BETWEEN them — after the branch that runs the whole native pass has
		// returned, before the pinned presentation path's own return. Moving the
		// subset into the whole-pass branch (where it would run for the bodies that
		// already run the whole native pass) or below the presentation return (where
		// it would never run) breaks this.
		var prefix = Normalize(MemberBlock(source, PrefixAnchor));
		if (!prefix.Contains(SkipsGate, StringComparison.Ordinal)
			|| Count(prefix, ReturnTrue) != 1
			|| Count(prefix, ReturnFalse) != 1)
		{
			return false;
		}

		var wholePassReturn = prefix.IndexOf(ReturnTrue, StringComparison.Ordinal);
		var subsetGate = prefix.IndexOf(SubsetGate, StringComparison.Ordinal);
		var presentationReturn = prefix.IndexOf(ReturnFalse, StringComparison.Ordinal);
		if (wholePassReturn >= subsetGate || subsetGate >= presentationReturn)
		{
			return false;
		}

		// The subset METHOD is exactly the five stages, once each, in the game's own
		// order: an extra statement (HandleVisuals pulled in), a dropped, duplicated
		// or re-ordered stage is not this shape.
		if (Normalize(MemberBlock(source, SubsetMethodAnchor)) != ExpectedSubsetMethod)
		{
			return false;
		}

		var flat = Normalize(source);

		// ... and no stage string lives anywhere else in the file, so a call hoisted
		// out of the method (into the proxy path, where it would run for every
		// proxied body every frame) is caught too.
		foreach (var stage in Stages)
		{
			if (Count(flat, stage) != 1)
			{
				return false;
			}
		}

		// Each stage's MethodInfo comes from the API it is named after.
		foreach (var (field, api) in StageApis)
		{
			if (!flat.Contains($"{field} = AccessTools.Method(typeof(Body), \"{api}\")", StringComparison.Ordinal))
			{
				return false;
			}
		}

		// The stages the pinned pose and physics must NOT get.
		foreach (var excluded in ExcludedStages)
		{
			if (flat.Contains(excluded, StringComparison.Ordinal))
			{
				return false;
			}
		}

		return true;
	}

	private static bool LimbGateIsRuled(string strippedSource)
	{
		var block = Normalize(MemberBlock(strippedSource, LimbPrefixAnchor));
		return block.Contains("CarriedBodySimulation.RunsLimbSimulation(", StringComparison.Ordinal)
			&& block.Contains("GetComponentInParent<RemoteBodyDriver>() != null", StringComparison.Ordinal)
			&& !block.Contains("IsCarrying", StringComparison.Ordinal)
			&& !block.Contains("SkipsNativeSimulation", StringComparison.Ordinal);
	}

	private static bool PhysicsStepIsRuled(string strippedSource)
	{
		var block = Normalize(MemberBlock(strippedSource, PhysicsPrefixAnchor));
		return block.Contains("CarriedBodySimulation.SkipsNativeSimulation(", StringComparison.Ordinal)
			&& block.Contains("CarriedBodyDriver.IsCarrying(__instance)", StringComparison.Ordinal);
	}

	/// <summary>
	/// The index of the whole-pass branch's own <c>return true;</c> inside the
	/// prefix — the statement the subset gate must sit after. Located through the
	/// prefix on purpose: the file has other <c>return true;</c> statements.
	/// </summary>
	private static int WholePassReturnIndex(string strippedSource)
	{
		var start = strippedSource.IndexOf(PrefixAnchor, StringComparison.Ordinal);
		Assert.True(start > 0, $"the pin's anchor `{PrefixAnchor}` was not found");
		var at = strippedSource.IndexOf(ReturnTrue, start, StringComparison.Ordinal);
		Assert.True(at > 0, "the whole-pass branch has no `return true;` inside the prefix");
		return at;
	}

	private static bool TrySubsetBlock(string strippedSource, out string block)
	{
		var start = strippedSource.IndexOf(SubsetGate, StringComparison.Ordinal);
		if (start < 0)
		{
			block = string.Empty;
			return false;
		}

		var end = strippedSource.IndexOf("\n\t\t}", start, StringComparison.Ordinal);
		if (end < 0)
		{
			block = string.Empty;
			return false;
		}

		block = strippedSource.Substring(start, end - start + "\n\t\t}".Length);
		return true;
	}

	/// <summary>
	/// The stripped source with one textual edge of the vitals-subset block replaced
	/// — the block is multi-line in the file, so the samples are taken on the same
	/// comment-stripped, LF-normalized copy the matcher reads.
	/// </summary>
	private static string MutateSubsetBlock(string from, string to)
	{
		var source = StrippedPatchSource();
		Assert.True(TrySubsetBlock(source, out var block), "the live source must carry the vitals-subset block");
		var mutated = source.Replace(block, Mutate(block, from, to));
		Assert.NotEqual(source, mutated);
		return mutated;
	}

	private static string MemberBlock(string strippedSource, string anchor)
	{
		var start = strippedSource.IndexOf(anchor, StringComparison.Ordinal);
		Assert.True(start > 0, $"the pin's anchor `{anchor}` was not found");
		var tail = strippedSource.Substring(start);
		var end = tail.Length;
		foreach (var boundary in MemberBoundaries)
		{
			var at = tail.IndexOf(boundary, 1, StringComparison.Ordinal);
			if (at > 0 && at < end)
			{
				end = at;
			}
		}

		return tail.Substring(0, end);
	}

	private static string Mutate(string body, string from, string to)
	{
		Assert.True(body.Contains(from, StringComparison.Ordinal), $"the mutation anchor `{from}` is not in the live body — the sample would silently no-op");
		var mutated = body.Replace(from, to);
		Assert.NotEqual(body, mutated);
		return mutated;
	}

	private static int Count(string text, string needle)
	{
		var count = 0;
		for (var at = text.IndexOf(needle, StringComparison.Ordinal); at >= 0; at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
		{
			count++;
		}

		return count;
	}

	/// <summary>Comment-only lines dropped and CRLF folded to LF, so the pin is line-ending agnostic.</summary>
	private static string StripCommentLines(string source)
	{
		var lines = source.Replace("\r\n", "\n").Split('\n');
		var kept = Array.FindAll(lines, line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal));
		return string.Join("\n", kept);
	}

	private static string Normalize(string text) =>
		string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

	private static string StrippedPatchSource() => StripCommentLines(ReadSource("Patches", "BodyUpdatePatch.cs"));

	private static string StrippedFamilySource() => StripCommentLines(ReadSource("Patches", "BodyPatches.cs"));

	private static string ReadSource(params string[] parts)
	{
		var path = Path.Combine(FindRepositoryRoot(), "src", "CasualtiesUnknownOnline.GameAdapter");
		foreach (var part in parts)
		{
			path = Path.Combine(path, part);
		}

		return File.ReadAllText(path);
	}

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CasualtiesUnknownOnline.slnx")))
		{
			directory = directory.Parent;
		}

		if (directory is null)
		{
			throw new InvalidOperationException("could not locate repository root (CasualtiesUnknownOnline.slnx)");
		}

		return directory.FullName;
	}
}
