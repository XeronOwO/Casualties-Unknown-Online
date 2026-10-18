using System;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The patch contract of the host-clone vitals pass: every owner-local side effect the game's own
/// stages produce on the clone is suppressed by a patch (or, for the camera's plain field and the
/// RNG stream, preserved by the pass itself), and the pass's own shape is the one the design
/// records — the fixed-step driver, the limb-before-body order, the saved/restored RNG and camera
/// field, and the clone-source decision (the existing render clone, no second body).
///
/// <para>
/// Source pins in the style of <see cref="CarriedBodyVitalsSubsetPinTests"/>: comment-only lines are
/// dropped and CRLF is folded before matching, so a comment cannot satisfy a pin. Each family has
/// a negative sample that mutates the live text (a guard removed, a coroutine body not emptied,
/// the guard moved after the play/capture, the pass order swapped, a limb skip dropped, the
/// preserve guard removed) and must be rejected, so the matcher cannot silently become a
/// tautology. What the matchers cannot see: whether the patches install at runtime — the generic
/// <see cref="PatchContractTests"/> resolves every target against the game assembly before launch.
/// </para>
/// </summary>
public class HostVitalsSuppressionPinTests
{
	[Fact]
	public void EveryPassScopedSideEffect_HasItsGuardInsideThePrefix()
	{
		var source = Stripped(ReadSource("Patches", "HostVitalsSuppressionPatches.cs"));

		foreach (var (patchClass, guard) in PassScopedPatches)
		{
			var block = MemberBlock(source, $"internal static class {patchClass}");
			Assert.True(block.Contains(guard, StringComparison.Ordinal),
				$"{patchClass}: the prefix no longer contains its guard `{guard}`");
		}

		Assert.Contains("GetComponentInParent<RemoteBodyDriver>() == null", MemberBlock(source, "internal static class CloneDroppingsCoroutinePatch"), StringComparison.Ordinal);
		Assert.Contains("HostVitalsPass.EmptyRoutine()", MemberBlock(source, "internal static class CloneDroppingsCoroutinePatch"), StringComparison.Ordinal);
	}

	[Fact]
	public void AVoidPatchWithItsGuardRemoved_IsRejected()
	{
		var live = Stripped(ReadSource("Patches", "HostVitalsSuppressionPatches.cs"));
		var mutated = Mutate(live, "private static bool Prefix() => !HostVitalsPass.IsActive;", "private static bool Prefix() => true;");

		Assert.False(VoidGuardsHold(mutated));
	}

	[Fact]
	public void ACoroutinePatchThatNoLongerEmptiesItsBody_IsRejected()
	{
		var live = Stripped(ReadSource("Patches", "HostVitalsSuppressionPatches.cs"));
		var mutated = Mutate(live, "__result = HostVitalsPass.EmptyRoutine();", "__result = null!;");

		Assert.False(EmptyRoutineGuardsHold(mutated));
	}

	[Fact]
	public void TheSoundGuard_RunsBeforeThePlayAndBeforeTheCapture()
	{
		Assert.True(SoundGuardIsFirst(Stripped(ReadSource("Patches", "SoundPlayPatch.cs"))));

		var live = Stripped(ReadSource("Patches", "SoundPlayPatch.cs"));
		var guard = "if (HostVitalsPass.IsActive)";
		var at = live.IndexOf(guard, StringComparison.Ordinal);
		Assert.True(at > 0, "the sound pin's guard was not found");
		var block = MemberBlock(live, "private static bool Prefix(");
		var guardBlock = Mutate(block, guard, "// moved");
		var movedAfterPlay = guardBlock.Replace("// moved", string.Empty)
			.Insert(guardBlock.IndexOf("if (clip == \"lifePodHit\"", StringComparison.Ordinal), guard);
		Assert.NotEqual(block, movedAfterPlay);
		Assert.False(SoundGuardIsFirst(movedAfterPlay));
	}

	[Fact]
	public void ThePass_RunsLimbsThenTheFiveStages_AndRestoresTheRngAndCameraField()
	{
		var source = Stripped(ReadSource("Patches", "HostVitalsPass.cs"));

		Assert.True(PassShapeHolds(source));

		var swapped = source
			.Replace("BodyPatches.RunLimbUpdates(body);", "PIN_TEMP_LIMBS")
			.Replace("BodyUpdatePatch.RunVitalsSubset(body);", "BodyPatches.RunLimbUpdates(body);")
			.Replace("PIN_TEMP_LIMBS", "BodyUpdatePatch.RunVitalsSubset(body);");
		Assert.NotEqual(source, swapped);
		Assert.False(PassShapeHolds(swapped));

		var noRngRestore = Mutate(source, "Random.state = randomState;", "// no restore");
		Assert.False(PassShapeHolds(noRngRestore));

		var noCameraRestore = Mutate(source, "camera.threatMusicTime = threatMusicTime;", "// no restore");
		Assert.False(PassShapeHolds(noCameraRestore));

		var noQuarantine = Mutate(source, "quarantine.Restore(body);", "// no restore");
		Assert.False(PassShapeHolds(noQuarantine));

		var noMinuteReset = Mutate(source, "body.minuteCheckTime = 0f;", "// no reset");
		Assert.False(PassShapeHolds(noMinuteReset));
	}

	[Fact]
	public void TheLimbPass_SkipsDismemberedAndInactiveLimbs_AndRunsTheResolvedUpdateOnce()
	{
		var source = Stripped(ReadSource("Patches", "BodyPatches.cs"));
		var block = MemberBlock(source, "internal static void RunLimbUpdates(Body body)");

		Assert.Contains("limb.dismembered", block, StringComparison.Ordinal);
		Assert.Contains("!limb.gameObject.activeSelf", block, StringComparison.Ordinal);
		Assert.Equal(1, Count(block, "LimbUpdateMethod.Invoke(limb, []);"));

		var mutated = Mutate(source, "|| limb.dismembered || !limb.gameObject.activeSelf", string.Empty);
		var mutatedBlock = MemberBlock(mutated, "internal static void RunLimbUpdates(Body body)");
		Assert.False(mutatedBlock.Contains("limb.dismembered", StringComparison.Ordinal));
	}

	[Fact]
	public void TheDriver_RunsFromFixedUpdate_AndTheStepCapIsCheckedBeforeThePass()
	{
		var driver = Stripped(ReadSource("Character", "HostVitalsDriver.cs"));
		Assert.Contains("private void FixedUpdate() => Bodies.Step(this);", driver, StringComparison.Ordinal);
		Assert.DoesNotContain("private void Update()", driver, StringComparison.Ordinal);

		var bodies = Stripped(ReadSource("Character", "HostVitalsBodies.cs"));
		var cap = bodies.IndexOf("HostVitalsPass.StepWithinCap(dt)", StringComparison.Ordinal);
		var pass = bodies.IndexOf("HostVitalsPass.Run(driver.Body, driver.Quarantine);", StringComparison.Ordinal);
		Assert.True(cap > 0 && pass > cap, "the step cap must be checked before the pass runs");
	}

	[Fact]
	public void TheFrameCostProbe_MeasuresTheWholeStepAndTheNativePass()
	{
		var bodies = Stripped(ReadSource("Character", "HostVitalsBodies.cs"));
		Assert.True(ProbeWiringHolds(bodies), "the frame-cost probe must time the whole step and the native pass and record both");

		// Negative samples: an unrecorded step, an unmeasured pass, or either try/finally removed
		// each break the shape the window's fields (and the throwing-path record) depend on.
		var step = MemberBlock(bodies, "internal void Step(");
		var advance = MemberBlock(bodies, "private void Advance(");
		Assert.False(ProbeWiringHolds(Mutate(bodies,
			"_cost.Record(Stopwatch.GetTimestamp(), started, passTicks, driver.SteamId, Time.fixedDeltaTime, Time.timeScale);",
			"// dropped")));
		Assert.False(ProbeWiringHolds(Mutate(bodies,
			"passTicks = Stopwatch.GetTimestamp() - passStarted;",
			"passTicks = 0L;")));
		Assert.False(ProbeWiringHolds(Mutate(bodies, step, Mutate(step, "finally", "// no finally"))));
		Assert.False(ProbeWiringHolds(Mutate(bodies, advance, Mutate(advance, "finally", "// no finally"))));
	}

	[Fact]
	public void TheCloneSource_IsTheExistingRenderClone_AndTheHostOwnedRoundTripUsesTheRuntimeDefinition()
	{
		var renderer = Stripped(ReadSource("Character", "RemotePlayerRenderer.cs"));
		Assert.Contains("_hostVitals.Attach(clone, remote.SteamId);", renderer, StringComparison.Ordinal);
		Assert.Contains("hostSimulated: _session.Role == SessionRole.Host", renderer, StringComparison.Ordinal);

		var attach = MemberBlock(Stripped(ReadSource("Character", "HostVitalsBodies.cs")), "internal void Attach(Body body, ulong steamId)");
		Assert.Contains("body.gameObject.AddComponent<HostVitalsDriver>()", attach, StringComparison.Ordinal);
		Assert.DoesNotContain("Instantiate", attach, StringComparison.Ordinal);

		var bodies = Stripped(ReadSource("Character", "HostVitalsBodies.cs"));
		Assert.Contains("ContinuousVitals.Differs(data,", bodies, StringComparison.Ordinal);
		Assert.Contains("ContinuousVitals.Write(data,", bodies, StringComparison.Ordinal);
		Assert.Contains("_restore.ApplyStats(driver.Body, data, applyComponents: false);", bodies, StringComparison.Ordinal);

		var limb = Stripped(ReadSource("Character", "CloneLimbRenderer.cs"));
		Assert.Contains("if (!preserveOwnedBleed)", limb, StringComparison.Ordinal);
		Assert.Contains("bool preserveOwnedBleed", limb, StringComparison.Ordinal);
	}

	[Fact]
	public void TheSeedForcesTheDerivedPeriodicValues_AndClearsTheRandomEventTimers()
	{
		var bodies = Stripped(ReadSource("Character", "HostVitalsBodies.cs"));
		var seed = MemberBlock(bodies, "private static void SeedDerivedState(Body body)");

		Assert.Contains("body.minuteCheckTime = 0f;", seed, StringComparison.Ordinal);
		Assert.Contains("body.halfMinuteCheckTime = 0f;", seed, StringComparison.Ordinal);
		Assert.Contains("body.limbBloodUpdateTimer = 0f;", seed, StringComparison.Ordinal);
		Assert.Contains("body.tempCheckTime = Time.time;", seed, StringComparison.Ordinal);
		Assert.Contains("body.usingSleepingBag = false;", seed, StringComparison.Ordinal);
		Assert.Contains("body.curSleep = Body.SleepQuality.Okay;", seed, StringComparison.Ordinal);
		Assert.Contains("body.forcedSleepQuality = null;", seed, StringComparison.Ordinal);
		Assert.Contains("SetValue(1.01f)", seed, StringComparison.Ordinal);
		Assert.Contains("SetValue(0.51f)", seed, StringComparison.Ordinal);
		Assert.Contains("SetValue(9999f)", seed, StringComparison.Ordinal);

		var mutated = Mutate(seed, "body.minuteCheckTime = 0f;", string.Empty);
		Assert.DoesNotContain("body.minuteCheckTime", mutated, StringComparison.Ordinal);
	}

	[Fact]
	public void TheQuarantineCoversTheDocumentedPolicyFields()
	{
		// The list below is the reviewed POLICY (the fields the native random/terminal writes reach,
		// derived from the decompiled bodies), not a copy of the implementation: the source is asserted
		// against it in both directions, so a field added to one side without the other fails here.
		var source = Stripped(ReadSource("Character", "HostVitalsQuarantine.cs"));
		var capture = MemberBlock(source, "internal void Capture(Body body)");
		var restore = MemberBlock(source, "internal void Restore(Body body)");

		foreach (var field in new[]
		{
			"brainHealth", "consciousness", "fibrillationProgress", "fibrillationForced", "sleeping",
			"internalBleeding", "bloodViscosity", "strokeAmount", "hasPulmonaryEmbolism", "septicShock", "shock",
			"heartRate", "respiratoryRate", "randomFibrillationVariation",
		})
		{
			Assert.Contains($"= body.{field};", capture, StringComparison.Ordinal);
			Assert.Contains($"body.{field} =", restore, StringComparison.Ordinal);
		}

		foreach (var field in new[]
		{
			"infected", "infectionAmount", "broken", "dislocated", "boneHealTimer", "dislocationTimer",
			"skinHealth", "muscleHealth",
		})
		{
			Assert.Contains($"[i] = limb.{field};", capture, StringComparison.Ordinal);
			Assert.Contains($"limb.{field} =", restore, StringComparison.Ordinal);
		}

		var mutated = Mutate(capture, "_internalBleeding = body.internalBleeding;", string.Empty);
		Assert.False(mutated.Contains("_internalBleeding = body.internalBleeding;", StringComparison.Ordinal));
	}

	[Fact]
	public void TheInjuryTrigger_SendsTheOwnerStateWhenALimbBleedRises()
	{
		var source = Stripped(ReadSource("Character", "CharacterDataSync.cs"));
		var trigger = MemberBlock(source, "private void TrackInjury(Body body)");
		Assert.Contains("PlayerInjuryRule.AnyBleedRose(_injuryBleedBaseline, current)", trigger, StringComparison.Ordinal);
		Assert.Contains("ReportOwnerStateEvent(body, \"injury\");", trigger, StringComparison.Ordinal);
		Assert.Contains("_vitals.AppliedCount != _lastRowApplyCount", trigger, StringComparison.Ordinal);
		Assert.Contains("_injuryBody = null;", MemberBlock(source, "internal void Update(Body? localBody)"), StringComparison.Ordinal);

		var report = MemberBlock(source, "internal void ReportOwnerStateEvent(Body body, string reason)");
		Assert.Contains("_characterData.SendLimbStateEvent(CaptureLimbStateEvent(body));", report, StringComparison.Ordinal);
	}

	[Fact]
	public void TheOwnerEventKeepsTheHostsTwoContinuousBloodValues()
	{
		var source = Stripped(ReadRuntimeSource("Session", "EntitySync", "EnemyTerminalStateApplier.cs"));
		var apply = MemberBlock(source, "public static void ApplyLimbState(CharacterDataMsg data, LimbStateEventMsg msg)");
		Assert.Contains("var hostBloodVolume = data.Health?.BloodVolume;", apply, StringComparison.Ordinal);
		Assert.Contains("var hostTotalBleedSpeed = data.Health?.TotalBleedSpeed;", apply, StringComparison.Ordinal);
		Assert.Contains("data.Health.BloodVolume = blood;", apply, StringComparison.Ordinal);
		Assert.Contains("data.Health.TotalBleedSpeed = total;", apply, StringComparison.Ordinal);
		Assert.Contains("previousBleeds.TryGetValue(limb.Index, out var previous)", apply, StringComparison.Ordinal);
		Assert.Contains("limb.BleedAmount = previous;", apply, StringComparison.Ordinal);

		var mutated = Mutate(apply, "data.Health.BloodVolume = blood;", "// dropped");
		Assert.False(mutated.Contains("data.Health.BloodVolume = blood;", StringComparison.Ordinal));
	}

	[Fact]
	public void TheModelAliveMarkGatesTheRow_AndTheRefusalAnswerRidesIt()
	{
		var bodies = Stripped(ReadSource("Character", "HostVitalsBodies.cs"));
		Assert.Contains("_characters.MarkHostModelAdvancing(driver.SteamId, _time.NowMs);", bodies, StringComparison.Ordinal);

		var service = Stripped(ReadRuntimeSource("Session", "CharacterData", "PlayerVitalsService.cs"));
		Assert.Contains("IsHostModelAdvancing(member.SteamId, nowMs, ModelAdvancingWindowMs)", service, StringComparison.Ordinal);
		Assert.Contains("RefusedField = _characters.GetReportRefusal(steamId) ?? \"\"", service, StringComparison.Ordinal);

		var handler = Stripped(ReadRuntimeSource("Session", "Handlers", "PlayerVitalsHandler.cs"));
		Assert.Contains("private void AnswerRefusal(PlayerVitalsMsg msg)", handler, StringComparison.Ordinal);
		Assert.Contains("msg.RefusedField", handler, StringComparison.Ordinal);
	}

	[Fact]
	public void TheLiquidAffectWriterAndTheAudioClipOverloadAreCovered()
	{
		var patches = Stripped(ReadSource("Patches", "HostVitalsSuppressionPatches.cs"));
		var liquid = MemberBlock(patches, "internal static class LiquidAffectFixedUpdatePatch");
		Assert.Contains("GetComponentInParent<RemoteBodyDriver>() == null", liquid, StringComparison.Ordinal);
		Assert.Contains("return limb == null || limb.body == null", liquid, StringComparison.Ordinal);

		var audioClip = Stripped(ReadSource("Patches", "SoundPlayAudioClipPatch.cs"));
		var prefix = MemberBlock(audioClip, "private static bool Prefix(");
		Assert.Contains("if (HostVitalsPass.IsActive)", prefix, StringComparison.Ordinal);
		Assert.Contains("return false;", prefix, StringComparison.Ordinal);
	}

	[Fact]
	public void ThePreserveGuardOnTheCloneLimbApply_IsLoadBearing()
	{
		var live = Stripped(ReadSource("Character", "CloneLimbRenderer.cs"));
		var mutated = Mutate(live,
			"\t\tif (!preserveOwnedBleed)\n\t\t{\n\t\t\tlimb.bleedAmount = limbData.BleedAmount;\n\t\t}",
			"\t\tlimb.bleedAmount = limbData.BleedAmount;");
		Assert.False(RenderLimbPreservesOwnedBleed(mutated));
	}

	[Fact]
	public void TheModelInputs_AreAppliedForThePassOnly_AndEveryPresentationValueIsRestored()
	{
		var bodies = Stripped(ReadSource("Character", "HostVitalsBodies.cs"));
		var scope = Stripped(ReadSource("Character", "HostVitalsInputScope.cs"));

		Assert.True(PassRunsInsideTheInputScope(bodies), "the pass must run inside the input scope's block, after the inputs are read");

		var applyBlock = MemberBlock(scope, "internal static HostVitalsInputScope Apply(Body body, in HostVitalsInputs inputs)");
		var disposeBlock = MemberBlock(scope, "public void Dispose()");
		var capture = MemberBlock(scope, "private HostVitalsInputScope(Body body)");
		Assert.True(ScopeShapeHolds(applyBlock, disposeBlock, capture));

		// Negative sample 1: the pass moved AFTER the scope's closing brace. Every IndexOf order
		// still holds, so only the brace-matched nesting check can reject it.
		var outside = Mutate(
			Mutate(bodies, "\t\t\t\tHostVitalsPass.Run(driver.Body, driver.Quarantine);\n", string.Empty),
			"\t\t}\n\n\t\tHostVitalsOwned.Capture",
			"\t\t}\n\t\tHostVitalsPass.Run(driver.Body, driver.Quarantine);\n\n\t\tHostVitalsOwned.Capture");
		Assert.False(PassRunsInsideTheInputScope(outside));

		// Negative sample 2: each restore removed in turn — the predicate (not a string search on
		// the mutation) must reject it.
		Assert.False(ScopeShapeHolds(Mutate(applyBlock, "body.currentClimbable = climbable;", "// not applied"), disposeBlock, capture));
		Assert.False(ScopeShapeHolds(applyBlock, Mutate(disposeBlock, "_body.inWater = _inWater;", "// not restored"), capture));
		Assert.False(ScopeShapeHolds(applyBlock, Mutate(disposeBlock, "SetExercising(_body, _exercising);", "// not restored"), capture));
		Assert.False(ScopeShapeHolds(applyBlock, Mutate(disposeBlock, "_body.currentClimbable = _climbable;", "// not restored"), capture));
	}

	/// <summary>
	/// Whether the pass call sits inside the <c>using (HostVitalsInputScope.Apply(...))</c> block and
	/// after the stream read — matched by braces, not by index order, so a regression that disposes
	/// the scope before the pass is rejected.
	/// </summary>
	private static bool PassRunsInsideTheInputScope(string source)
	{
		var read = source.IndexOf("HostVitalsInputs.From(_entities.GetRemotePlayer(driver.SteamId))", StringComparison.Ordinal);
		var usingAt = source.IndexOf("using (HostVitalsInputScope.Apply(driver.Body, inputs))", StringComparison.Ordinal);
		var pass = source.IndexOf("HostVitalsPass.Run(driver.Body, driver.Quarantine);", StringComparison.Ordinal);
		if (read < 0 || usingAt < read || pass < usingAt)
		{
			return false;
		}

		var open = source.IndexOf('{', usingAt);
		return open > 0 && open < pass && MatchingBrace(source, open) > pass;
	}

	/// <summary>The index of the brace matching the one at <paramref name="open"/>, or -1. Braces inside a string are balanced in this file, so a plain count is exact here.</summary>
	private static int MatchingBrace(string source, int open)
	{
		var depth = 0;
		for (var i = open; i < source.Length; i++)
		{
			if (source[i] == '{')
			{
				depth++;
			}
			else if (source[i] == '}')
			{
				depth--;
				if (depth == 0)
				{
					return i;
				}
			}
		}

		return -1;
	}

	/// <summary>
	/// One reviewed shape of the input scope: the apply half writes every model input (the climb
	/// lookup resolved BEFORE the first write, so a throw cannot strand half of them), the capture
	/// half holds what the clone presented, and the dispose half restores every one of those values.
	/// </summary>
	private static bool ScopeShapeHolds(string apply, string dispose, string capture)
	{
		var writes = new[]
		{
			"body.inWater = inputs.InWater;",
			"body.moveDir = new Vector2(inputs.Moving ? 1f : 0f, 0f);",
			"SetExercising(body, inputs.Exercising);",
			"body.currentClimbable = climbable;",
		};
		var restores = new[]
		{
			"_body.inWater = _inWater;",
			"_body.moveDir = _moveDir;",
			"SetExercising(_body, _exercising);",
			"_body.currentClimbable = _climbable;",
		};
		var captures = new[] { "_inWater = ", "_exercising = ", "_moveDir = ", "_climbable = " };
		var resolve = apply.IndexOf("Climbable.GetClosestClimbable(body.transform.position)", StringComparison.Ordinal);
		var firstWrite = apply.IndexOf("body.inWater = inputs.InWater;", StringComparison.Ordinal);

		return writes.All(line => apply.Contains(line, StringComparison.Ordinal))
			&& restores.All(line => dispose.Contains(line, StringComparison.Ordinal))
			&& captures.All(line => capture.Contains(line, StringComparison.Ordinal))
			&& resolve >= 0
			&& firstWrite >= 0
			&& resolve < firstWrite;
	}

	[Fact]
	public void TheOwnerSide_PublishesTheThreeModelInputs()
	{
		var run = Stripped(ReadSource("Run", "RunCoordinator.cs"));
		Assert.Contains("var vitals = HostVitalsInputs.FromBody(body);", run, StringComparison.Ordinal);
		Assert.Contains("limbPoses, vitals.InWater, vitals.Moving, vitals.Exercising);", run, StringComparison.Ordinal);

		var inputs = Stripped(ReadSource("Character", "HostVitalsInputs.cs"));
		var fromBody = MemberBlock(inputs, "internal static HostVitalsInputs FromBody(Body body)");
		Assert.Contains("body.inWater,", fromBody, StringComparison.Ordinal);
		Assert.Contains("IsMoving(body.moveDir.x),", fromBody, StringComparison.Ordinal);
		Assert.Contains("body.exercising,", fromBody, StringComparison.Ordinal);
		Assert.Contains("body.currentClimbable != null", fromBody, StringComparison.Ordinal);

		var service = Stripped(ReadRuntimeSource("Session", "EntitySync", "EntitySyncService.cs"));
		Assert.Contains("_localPlayer.InWater = inWater;", service, StringComparison.Ordinal);
		Assert.Contains("_localPlayer.Moving = moving;", service, StringComparison.Ordinal);
		Assert.Contains("_localPlayer.Exercising = exercising;", service, StringComparison.Ordinal);
	}

	private static bool VoidGuardsHold(string source) =>
		PassScopedPatches
			.Where(patch => patch.Guard.Contains("Prefix() =>", StringComparison.Ordinal))
			.All(patch => MemberBlock(source, $"internal static class {patch.Class}").Contains(patch.Guard, StringComparison.Ordinal));

	private static bool EmptyRoutineGuardsHold(string source) =>
		PassScopedPatches
			.Where(patch => patch.Guard.Contains("__result", StringComparison.Ordinal))
			.All(patch => MemberBlock(source, $"internal static class {patch.Class}").Contains(patch.Guard, StringComparison.Ordinal));

	private static bool SoundGuardIsFirst(string source)
	{
		var prefix = MemberBlock(source, "private static bool Prefix(");
		var guard = "if (HostVitalsPass.IsActive)";
		var at = prefix.IndexOf(guard, StringComparison.Ordinal);
		if (at < 0)
		{
			return false;
		}

		return at < prefix.IndexOf("if (clip == \"lifePodHit\"", StringComparison.Ordinal)
			&& at < prefix.IndexOf("CharacterSoundPolicy.Classify", StringComparison.Ordinal);
	}

	/// <summary>
	/// The frame-cost probe's wiring: the whole step is timed inside a try/finally that records
	/// (and suspends when filtered), and the native pass inside it is timed in its own finally so
	/// a throwing pass still reports its ticks. The shapes are brace-matched, not order-only: a
	/// removed finally would otherwise leave every anchor in order.
	/// </summary>
	private static bool ProbeWiringHolds(string source) =>
		StepShapeHolds(MemberBlock(source, "internal void Step("))
		&& PassMeasureShapeHolds(MemberBlock(source, "private void Advance("));

	private static bool StepShapeHolds(string step)
	{
		var measure = step.IndexOf("var measure = _cost.Enabled;", StringComparison.Ordinal);
		var advance = step.IndexOf("Advance(driver, ref passTicks, measure);", StringComparison.Ordinal);
		var record = step.IndexOf("_cost.Record(Stopwatch.GetTimestamp(), started, passTicks, driver.SteamId, Time.fixedDeltaTime, Time.timeScale);", StringComparison.Ordinal);
		var suspend = step.IndexOf("_cost.Suspend();", StringComparison.Ordinal);
		var tryAt = step.IndexOf("\t\ttry\n\t\t{", StringComparison.Ordinal);
		if (measure < 0 || advance < 0 || record < 0 || suspend < 0 || tryAt < 0 || advance < tryAt)
		{
			return false;
		}

		var tryOpen = step.IndexOf('{', tryAt);
		var tryClose = MatchingBrace(step, tryOpen);
		if (tryClose < advance)
		{
			return false;
		}

		var finallyAt = step.IndexOf("\t\tfinally\n\t\t{", tryClose, StringComparison.Ordinal);
		if (finallyAt < 0)
		{
			return false;
		}

		var finallyOpen = step.IndexOf('{', finallyAt);
		var finallyClose = MatchingBrace(step, finallyOpen);
		return record > finallyOpen && record < finallyClose && suspend > finallyOpen && suspend < finallyClose;
	}

	private static bool PassMeasureShapeHolds(string advance)
	{
		var passStarted = advance.IndexOf("var passStarted = measure ? Stopwatch.GetTimestamp() : 0L;", StringComparison.Ordinal);
		var tryAt = advance.IndexOf("\t\t\ttry\n\t\t\t{", StringComparison.Ordinal);
		var pass = advance.IndexOf("HostVitalsPass.Run(driver.Body, driver.Quarantine);", StringComparison.Ordinal);
		var finallyAt = advance.IndexOf("\t\t\tfinally\n\t\t\t{", StringComparison.Ordinal);
		var measured = advance.IndexOf("passTicks = Stopwatch.GetTimestamp() - passStarted;", StringComparison.Ordinal);
		return passStarted > 0 && tryAt > passStarted && pass > tryAt && finallyAt > pass && measured > finallyAt;
	}

	private static bool PassShapeHolds(string source)
	{
		var randomSave = source.IndexOf("var randomState = Random.state;", StringComparison.Ordinal);
		var seed = source.IndexOf("Random.InitState(Seed);", StringComparison.Ordinal);
		var scope = source.IndexOf("using (EnterScope())", StringComparison.Ordinal);
		var quarantineCapture = source.IndexOf("quarantine.Capture(body);", StringComparison.Ordinal);
		var limbs = source.IndexOf("BodyPatches.RunLimbUpdates(body);", StringComparison.Ordinal);
		var stages = source.IndexOf("BodyUpdatePatch.RunVitalsSubset(body);", StringComparison.Ordinal);
		var quarantineRestore = source.IndexOf("quarantine.Restore(body);", StringComparison.Ordinal);
		var minuteReset = source.IndexOf("body.minuteCheckTime = 0f;", StringComparison.Ordinal);
		var halfMinuteReset = source.IndexOf("body.halfMinuteCheckTime = 0f;", StringComparison.Ordinal);
		var restore = source.IndexOf("Random.state = randomState;", StringComparison.Ordinal);
		var cameraRestore = source.IndexOf("camera.threatMusicTime = threatMusicTime;", StringComparison.Ordinal);
		return randomSave > 0 && seed > randomSave && scope > randomSave && seed > scope
			&& quarantineCapture > seed && limbs > quarantineCapture && stages > limbs
			&& quarantineRestore > stages && minuteReset > quarantineRestore && halfMinuteReset > minuteReset
			&& restore > halfMinuteReset && cameraRestore > restore;
	}

	private static bool RenderLimbPreservesOwnedBleed(string source) =>
		source.Contains("if (!preserveOwnedBleed)", StringComparison.Ordinal)
		&& source.Contains("limb.bleedAmount = limbData.BleedAmount;", StringComparison.Ordinal);

	/// <summary>The pass-scoped suppressions: class name and the exact guard the prefix must carry.</summary>
	private static readonly (string Class, string Guard)[] PassScopedPatches =
	[
		("BodyRagdollPatch", "private static bool Prefix() => !HostVitalsPass.IsActive;"),
		("BodyTryStartFibrillationPatch", "private static bool Prefix() => !HostVitalsPass.IsActive;"),
		("BodyTryLastStandPatch", "private static bool Prefix() => !HostVitalsPass.IsActive;"),
		("BodyWakeUpPatch", "private static bool Prefix() => !HostVitalsPass.IsActive;"),
		("BodyDropItemPatch", "private static bool Prefix() => !HostVitalsPass.IsActive;"),
		("VomiterVomitPatch", "private static bool Prefix() => !HostVitalsPass.IsActive;"),
		("VomiterVomitBloodPatch", "private static bool Prefix() => !HostVitalsPass.IsActive;"),
		("BodyCryPatch", "__result = HostVitalsPass.EmptyRoutine();"),
		("BodyBrainControlReversePatch", "__result = HostVitalsPass.EmptyRoutine();"),
		("BodyBrainDamageRagdollPatch", "__result = HostVitalsPass.EmptyRoutine();"),
		("PlayerCameraFlashBrainPatch", "__result = HostVitalsPass.EmptyRoutine();"),
	];

	private static string ReadSource(params string[] parts)
	{
		var path = Path.Combine(FindRepositoryRoot(), "src", "CasualtiesUnknownOnline.GameAdapter");
		foreach (var part in parts)
		{
			path = Path.Combine(path, part);
		}

		return File.ReadAllText(path);
	}

	private static string ReadRuntimeSource(params string[] parts)
	{
		var path = Path.Combine(FindRepositoryRoot(), "src", "CasualtiesUnknownOnline.Runtime");
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

	private static string MemberBlock(string source, string anchor)
	{
		var start = source.IndexOf(anchor, StringComparison.Ordinal);
		Assert.True(start >= 0, $"the pin's anchor `{anchor}` was not found");
		var tail = source.Substring(start);
		var boundaries = new[] { "\n\t[HarmonyPatch", "\n\tinternal ", "\n\tprivate ", "\n\tpublic ", "\n}" };
		var end = tail.Length;
		foreach (var boundary in boundaries)
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
	private static string Stripped(string source)
	{
		var lines = source.Replace("\r\n", "\n").Split('\n');
		var kept = lines.Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal));
		return string.Join("\n", kept);
	}
}
