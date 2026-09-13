using System;
using System.Reflection;
using System.Runtime.Serialization;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The classification half of the trap/entity action divergence hardening: what the
/// shared action library answers when the LOCAL copy cannot carry the fact the row
/// names. That verdict is the restore account's currency (<see cref="TrapActionVerdict"/>),
/// so an action that writes NOTHING and still answers <see cref="TrapActionOutcome.Applied"/>
/// hands the player a row the report calls restored.
///
/// The action bodies are game-typed, so most of the library is read-only reviewed — but
/// not all of it: the two <c>LifepodController</c> actions whose missing member is a pure
/// FIELD READ can be driven here on a NEVER-INITIALIZED copy of the game component.
/// <c>LifepodController.shower</c> and <c>.heater</c> are inspector members that the
/// component's own <c>Start</c> never touches (it writes <c>heatSprite</c>,
/// <c>disinfectSprite</c> and <c>heatButton</c>, LifepodController.cs:8-13), so a copy
/// carrying neither is exactly the divergence case: the game's own lifecycle never
/// noticed, and only the action's call dereferences them.
///
/// The other two over-claims of the same family start with engine calls this host cannot
/// make (<c>ApplyBioTerminal</c>'s <c>GetComponent</c>, <c>ApplyCrystalShy</c>'s
/// <c>Physics2D.OverlapCircleAll</c>) and carry read-only review evidence instead.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TrapActionClassificationTests
{
	/// <summary>The two shared action libraries a kind's server can live in.</summary>
	private static readonly string[] ActionTypeNames = ["TrapStateActions", "CrystalStateActions"];

	/// <summary>The shower row names the shower activation, so a controller whose prefab
	/// carries no shower cannot carry the fact: <c>ActivateShower</c> dereferences
	/// <c>this.shower</c> unconditionally (LifepodController.cs:45-49) and the throw is
	/// NOT one row's refusal — it escapes the replay's row loop and reaches
	/// <c>RestoredWorldFactReplay</c>'s catch, which marks the live-world write
	/// incomplete and releases every handover.</summary>
	[Fact]
	public void ShowerAction_OnACopyWithNoShower_AnswersNotApplicable() =>
		Assert.Equal(TrapActionOutcome.NotApplicable, InvokeAction("ApplyShower", "LifepodController"));

	/// <summary>The heat row carries the heat state the trigger side's toggle reached
	/// (<c>TrapLifepodButtonPatch</c> sends <c>controller.heatState</c>), so the state
	/// must be REPRESENTABLE here: <c>ToggleHeatState</c> writes <c>heater.desiredTemp</c>
	/// and <c>heater.enabled</c> on every step (LifepodController.cs:25-26/33-34/39 — the
	/// dereference that throws in production is the null <c>heater</c>), so a null heater
	/// reaches an unguarded game write instead of answering. This host reaches that write
	/// far enough to die inside it (the frame is <c>LifepodController.ToggleHeatState</c>,
	/// not the action), which is the evidence one step removed: the action CALLS INTO the
	/// game write on a copy that carries no member, and the assertion is that it must
	/// decide before it gets there.</summary>
	[Fact]
	public void HeatAction_OnACopyWithNoHeater_AnswersNotApplicable() =>
		Assert.Equal(TrapActionOutcome.NotApplicable, InvokeAction("ApplyHeat", "LifepodController", (byte)1));

	/// <summary>The EMP and shy rows name their effect's <c>activated</c> latch — the
	/// trigger patches observe exactly that rise (<c>TrapCrystalPatch.EmpTryEMPPostfix</c>,
	/// <c>ShyTouchedPrefix/Postfix</c>) — and a crystal whose rolled effect list carries
	/// no such effect cannot carry the fact. That decision is the shared latch rule, and
	/// it is machine-checked here on a never-initialized crystal (no <c>Awake</c>, so no
	/// effect list).
	///
	/// The three crystal ACTION bodies whose replay positions a sound or a component
	/// (<c>ApplyCrystalEMP</c>, <c>ApplyCrystalShy</c>, <c>ApplyCrystalMetamorphic</c>) are NOT
	/// driven here: each reads <c>crystal.transform</c> — and the metamorphic one also calls
	/// <c>gameObject.AddComponent</c> — Unity members the runtime binds for the WHOLE method
	/// before any early return runs, so those tests could never go green in this host (observed:
	/// <c>SecurityException: ECall methods must be packaged into a system module</c> raised from
	/// the action frame itself). Their classification carries the file:line review evidence on
	/// the ticket, the same split the rest of this domain uses, while the rule they call and the
	/// actions whose bodies stay managed are pinned here.</summary>
	[Fact]
	public void EffectLatch_OnACopyCarryingNoSuchEffect_IsNotApplicable() =>
		Assert.Equal(TrapActionOutcome.NotApplicable, InvokeLatch("CrystalEMP"));

	/// <summary>The mimic action IS host-drivable (its body touches no Unity member), so the
	/// production <c>ApplyCrystalMimic</c> is driven here: a crystal carrying no mimic effect
	/// must REFUSE the row rather than claim it — the divergence the mimic's own accessor was
	/// written for.</summary>
	[Fact]
	public void MimicAction_OnACopyWithNoMimicEffect_AnswersNotApplicable() =>
		Assert.Equal(TrapActionOutcome.NotApplicable, InvokeAction("ApplyCrystalMimic", "CrystalBehaviour", true));

	/// <summary>
	/// The latch rule itself (<c>CrystalEffectAccess.TryActivate</c>), driven through the
	/// adapter's own type on a never-initialized <c>CrystalBehaviour</c>.
	/// </summary>
	private static TrapActionOutcome InvokeLatch(string effectTypeName)
	{
		var behaviour = GameAssemblyHost.ResolveType("CrystalBehaviour");
		Assert.True(behaviour != null, "CrystalBehaviour must resolve from the game assembly");

		var access = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.CrystalEffectAccess",
			throwOnError: true)!;
		var target = access.GetMethod("TryActivate", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		Assert.True(target != null, $"{access.FullName}.TryActivate must exist");

		return (TrapActionOutcome)target!.Invoke(
			null,
			[FormatterServices.GetUninitializedObject(behaviour!), effectTypeName])!;
	}

	/// <summary>
	/// Run one shared action on a NEVER-INITIALIZED copy of its game component: no
	/// <c>Awake</c>, no <c>Start</c>, no inspector values, no effect list — the state a
	/// prefab without the member produces, reached without a game engine. The action
	/// library is the adapter's internal static class, so it is resolved reflectively (the
	/// adapter is never compile-referenced by this suite); the VERDICT is a Runtime type
	/// the test assembly can see (InternalsVisibleTo), so the assertion compares the enum
	/// itself rather than a string.
	/// </summary>
	private static TrapActionOutcome InvokeAction(string method, string component, params object[] extraArgs)
	{
		var componentType = GameAssemblyHost.ResolveType(component);
		Assert.True(componentType != null, $"{component} must resolve from the game assembly");

		var target = FindAction(method);
		var args = new object[extraArgs.Length + 1];
		args[0] = FormatterServices.GetUninitializedObject(componentType!);
		extraArgs.CopyTo(args, 1);
		return (TrapActionOutcome)target.Invoke(null, args)!;
	}

	/// <summary>
	/// The member is resolved from the adapter's OWN type — the library the kind belongs to
	/// (<c>TrapStateActions</c> for the lifepod/mechanism family, <c>CrystalStateActions</c>
	/// for the crystal family) — never from a copy of it, so the test drives the production
	/// body. A method that no library declares is a failure, not a skip.
	/// </summary>
	private static MethodInfo FindAction(string method)
	{
		foreach (var typeName in ActionTypeNames)
		{
			var type = GameAssemblyHost.Adapter.GetType(
				$"CasualtiesUnknownOnline.GameAdapter.World.{typeName}",
				throwOnError: true)!;
			var found = type.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (found != null)
			{
				return found;
			}
		}

		throw new InvalidOperationException($"no shared action library declares {method}");
	}
}
