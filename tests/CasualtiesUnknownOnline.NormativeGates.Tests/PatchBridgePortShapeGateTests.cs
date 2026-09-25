using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The patch-bridge port gate (<c>review/patch-bridge-domain-ports.md</c>): the
/// aggregate is frozen, and a domain's patch surface lives in its own port.
///
/// <para>
/// IPatchBridge is the one seam every <c>[HarmonyPatch]</c> class reads, so it is
/// the wall this gate keeps from widening. Its census is pinned member by
/// member — a member ADDED to it, and equally one removed without moving to a
/// port, fails here. The same pin covers the aggregate's composition (the four
/// earlier patch seams), every seam interface's own census, the one
/// implementation (<c>GameAdapterBridge</c>): its composition, its public
/// surface, which must be exactly the members of the seams it serves, and its
/// non-public implementation members; and the static seam's accessors, one per
/// port.
/// </para>
///
/// <para>
/// The direction is enforced by the COMPILER, not by this gate: the aggregate
/// neither declares nor composes the fluid port's members, so a
/// patch written against IPatchBridge cannot reach the fluid domain at all —
/// <see cref="ThePort_IsNotReachableThroughTheAggregate"/> states that fact.
/// This gate reads SOURCE (Roslyn), so it runs in the fast suite without the
/// game assemblies; the compiled artifact is covered by
/// <c>CasualtiesUnknownOnline.Tests/Patching/PatchBridgePortContractTests</c>.
/// </para>
///
/// <para>
/// Reach, stated rather than implied: the census reads the members of each
/// named file's single top-level type — methods, properties, events, indexers
/// and fields, a field contributing each declarator. Constructors (including a
/// primary constructor, which is not a member node), operators, destructors and
/// nested types are not seam surface and are deliberately not counted. A new
/// FILE declaring part of a pinned type is out of reach; neither type is
/// <c>partial</c> today.
/// </para>
/// </summary>
public class PatchBridgePortShapeGateTests
{
	private const string AdapterDir = "src/CasualtiesUnknownOnline.GameAdapter/";

	private const string AggregateFile = AdapterDir + "IPatchBridge.cs";

	private const string BridgeFile = AdapterDir + "GameAdapterBridge.cs";

	private const string SeamFile = AdapterDir + "PatchBridge.cs";

	/// <summary>The frozen aggregate: every member IPatchBridge may declare. Adding one, or removing one without a port to carry it, fails the census.</summary>
	private static readonly string[] AggregateMembers =
	[
		"ApplyBodyCirculationPostfix",
		"ApplyBodyCirculationPrefix",
		"ApplyBodyStatusProjection",
		"ApplyLimbStatusProjection",
		"ApplyModMoodles",
		"Carriage",
		"DeferLifePodShake",
		"DeferLifePodSound",
		"EnsureGuestWorldParams",
		"HasRestorableWorld",
		"IsHeaterCookAuthority",
		"IsHostMode",
		"IsInGateWindow",
		"IsReplayingLifePodSound",
		"IsSessionActive",
		"IsWaitingForReady",
		"IsWorldGenIsolated",
		"ModContent",
		"OnArmSwing",
		"OnAttackAnim",
		"OnBlockDamaged",
		"OnBlockSet",
		"OnBrokenItemUpdate",
		"OnBuildingEntityDamaged",
		"OnBuildingEntityDestroyed",
		"OnBuildingEntityOpened",
		"OnCharacterLandingVisual",
		"OnCharacterRagdoll",
		"OnCharacterSound",
		"OnCombineBegin",
		"OnCombineEnd",
		"OnContainerUnloadedAll",
		"OnCraftBegin",
		"OnCraftEnd",
		"OnCrystalEnemyBodyResolved",
		"OnCrystalLungeBegin",
		"OnCrystalLungeEnd",
		"OnCustomBuildingWorldGeneration",
		"OnCustomItemWorldGeneration",
		"OnCustomLiquidWorldGeneration",
		"OnCustomTileBroken",
		"OnCustomTileOreGeneration",
		"OnDarkenSkipped",
		"OnDragReleasedToWorld",
		"OnDynamiteExploded",
		"OnEarthquakeStarted",
		"OnElderHorrorDefeat",
		"OnElderHorrorTick",
		"OnEnemyBite",
		"OnEnemyItemCollision",
		"OnEntityInstantiated",
		"OnGrabberGrabbed",
		"OnGuestStartAttempt",
		"OnGunStateChanged",
		"OnHeaterCookBegin",
		"OnHeaterCookCaptureFailed",
		"OnHeaterCookCompleted",
		"OnHostContinueRequested",
		"OnInventoryChanged",
		"OnItemDestroyed",
		"OnItemDropped",
		"OnItemInstantiated",
		"OnItemLoadedIntoContainer",
		"OnItemPickedUp",
		"OnItemPickupStart",
		"OnItemThrown",
		"OnItemUnloadedFromContainer",
		"OnItemUsed",
		"OnItemWorn",
		"OnLimbStateEvent",
		"OnLiquidTransferFinished",
		"OnLocalTimeScaleChanged",
		"OnNativeSaveSlot",
		"OnNonAuthoritativeItemImpactSuppressed",
		"OnPickUpResult",
		"OnPickupCheckFailed",
		"OnSceneLoadBegin",
		"OnSlotMoved",
		"OnSpeechReported",
		"OnSpiderTargetDecided",
		"OnTimeScaleSetRequested",
		"OnTraderActionReported",
		"OnTraderSwing",
		"OnTrapTriggered",
		"OnWorldBloodSpawn",
		"OnWorldGenerate",
		"OnWorldJoinRequested",
		"OnXalorisSepticTick",
		"ResetGenStreamToBaseline",
		"SessionSurface",
		"ShouldApplyQuakeBreak",
		"ShouldSuppressDestroy",
		"TryDeferMenuReturn",
		"TryDeferStartGateAlert",
		"TryHandleDraggedItemUseOnRemote",
		"WrapStructureWorldGen",
	];

	/// <summary>
	/// The seam portfolio. <c>Composed</c> marks the interfaces IPatchBridge
	/// itself inherits (the four earlier splits); every entry's census is pinned,
	/// so a member added to any seam is a red until it is reviewed here.
	/// </summary>
	private static readonly (string Interface, string File, bool Composed, string[] Members)[] Seams =
	[
		("IRemoteBackpackPatchBridge", AdapterDir + "IRemoteBackpackPatchBridge.cs", true,
		[
			"EmitRemoteDragIntents",
			"EmitRemoteWhileDraggingFrame",
			"LocalSteamId",
			"ReportProxyNotOperable",
			"ReportRemoteDragUnresolved",
			"ReportRemoteGestureNotCarried",
		]),
		("IRemoteMedicalPatchBridge", AdapterDir + "IRemoteMedicalPatchBridge.cs", true,
		[
			"TryHandleRemoteMedicalLimbUse",
			"TryStartRemoteShrapnelSpecial",
			"TryStartRemoteWoundSpecial",
		]),
		("ICarriagePatchBridge", AdapterDir + "ICarriagePatchBridge.cs", true,
		[
			"GetCarriedEncumbrance",
			"IsLocalCarrier",
			"OnLocalCarrierBodyUpdated",
		]),
		("ISessionSurfacePatchBridge", AdapterDir + "ISessionSurfacePatchBridge.cs", true,
		[
			"IsNonModalEscapeSurfaceOpen",
			"IsOnlineUiModalOpen",
		]),
		("IModContentPatchBridge", AdapterDir + "IModContentPatchBridge.cs", false,
		[
			"ApplyCustomBuildingInstanceHooks",
			"TryGetCustomBlockInfo",
			"TryGetModDropSourceCategory",
			"TryResolveBuildingTemplate",
			"TryResolveItemTemplate",
		]),
		("IFluidPatchPort", AdapterDir + "IFluidPatchPort.cs", false,
		[
			"ApplyLiquidTileBodyTouch",
			"OnFluidDrinkReported",
			"OnFluidFixedUpdate",
			"TryDrinkCustomLiquid",
			"TryGetCustomLiquidColor",
			"TryGetCustomLiquidName",
			"TryGetCustomWaterInfo",
			"TryRenderCustomLiquids",
		]),
	];

	/// <summary>What the one implementation adds to the seams: its domain handles and the one private helper. No other non-public member may appear.</summary>
	private static readonly string[] BridgeImplementationMembers =
	[
		"GetRemoteProxyItemId",
		"_carriage",
		"_modContent",
		"_remoteMedicalOps",
		"_remoteDragIntents",
	];

	/// <summary>The static seam's whole census: the one field holding the bound bridge, and the accessors — the aggregate's plus one per port-shaped seam. A new accessor is a new door and is a red.</summary>
	private static readonly string[] SeamMembers =
	[
		"Bind",
		"Fluid",
		"Impl",
		"ModContent",
		"SessionSurface",
		"Unbind",
		"_bound",
	];

	/// <summary>The aggregate census floor — a pin emptied alongside its source would otherwise pass by checking nothing.</summary>
	private const int AggregateFloor = 90;

	/// <summary>
	/// The composed seams' floor, for the same reason. It moved from 20 to 10
	/// when the remote-inventory rework (decision 218) deleted the fourteen
	/// gesture-per-operation members of <c>IRemoteBackpackPatchBridge</c>: the
	/// mechanism they served — CUO classifying the release into an operation
	/// enum — no longer exists, and the four members that replaced them are the
	/// whole surface the patches read. The floor stays a floor: a seam emptied
	/// alongside its pin still fails here.
	/// </summary>
	private const int ComposedSeamFloor = 10;

	public static IEnumerable<object[]> SeamCensus() =>
		Seams.Select(seam => new object[] { seam.Interface, seam.File, Census(seam.Members) });

	[Fact]
	public void Aggregate_DeclaresExactlyTheFrozenCensus()
	{
		Assert.True(
			AggregateMembers.Length >= AggregateFloor,
			$"the pinned aggregate census holds only {AggregateMembers.Length} member(s) — the pin was emptied, not the interface");

		Assert.Equal(Census(AggregateMembers), Census(DeclaredMembers(AggregateFile)));
	}

	[Fact]
	public void Aggregate_ComposesExactlyThePinnedSeams()
	{
		var composed = Seams.Where(seam => seam.Composed).ToArray();
		Assert.True(
			composed.Sum(seam => seam.Members.Length) >= ComposedSeamFloor,
			$"the pinned composed census holds only {composed.Sum(seam => seam.Members.Length)} member(s) — the pin was emptied, not the interfaces");

		Assert.Equal(
			Census(composed.Select(seam => seam.Interface)),
			Census(BaseList(AggregateFile)));
	}

	[Theory]
	[MemberData(nameof(SeamCensus))]
	public void Seam_DeclaresExactlyItsPinnedMembers(string name, string file, string expected)
	{
		var actual = Census(DeclaredMembers(file));
		Assert.True(
			string.Equals(expected, actual, StringComparison.Ordinal),
			$"{name} ({file}) declares: {actual}{Environment.NewLine}pinned: {expected}");
	}

	/// <summary>
	/// The migration's guard: a port is not a rename of the aggregate. No port
	/// member may also be declared by IPatchBridge, the aggregate must not
	/// compose the port, and the fluid domain's members reachable through the
	/// aggregate must be exactly none — otherwise the port would be the whole
	/// interface under a new name and this ticket's split would be cosmetic.
	/// </summary>
	[Fact]
	public void ThePort_IsNotReachableThroughTheAggregate()
	{
		var port = Seams.Single(seam => seam.Interface == "IFluidPatchPort");
		Assert.False(port.Composed, "the port is composed by the aggregate — the compiler would keep the members reachable through IPatchBridge");

		var shared = port.Members.Intersect(DeclaredMembers(AggregateFile), StringComparer.Ordinal).ToArray();
		Assert.Empty(shared);
	}

	/// <summary>One name, one door: a member declared by two seams — or by a seam and the aggregate — would be reachable through either, which is how the wall grows back.</summary>
	[Fact]
	public void NoMemberName_IsSharedByTwoSeams()
	{
		var doors = Seams
			.SelectMany(seam => seam.Members.Select(member => (Seam: seam.Interface, Member: member)))
			.Concat(AggregateMembers.Select(member => (Seam: "IPatchBridge", Member: member)))
			.GroupBy(pair => pair.Member, StringComparer.Ordinal)
			.Where(group => group.Count() > 1)
			.Select(group => $"{group.Key}: {string.Join(" + ", group.Select(pair => pair.Seam))}")
			.ToArray();

		Assert.Empty(doors);
	}

	[Fact]
	public void Bridge_ImplementsExactlyTheAggregateAndThePorts() =>
		Assert.Equal(Census(["IFluidPatchPort", "IPatchBridge"]), Census(BaseList(BridgeFile)));

	/// <summary>
	/// The class serves the seams and nothing else: its public surface is exactly
	/// the aggregate's members plus the composed seams' plus the port's, so a new
	/// public member is a red whether it was declared on an interface or only on
	/// the class, and a member removed from an interface but left here is caught
	/// too (it would be an orphan surface no patch can reach by contract).
	/// </summary>
	[Fact]
	public void Bridge_PublicSurface_IsExactlyTheSeamsItServes()
	{
		var expected = AggregateMembers
			.Concat(Seams.Where(seam => seam.Composed).SelectMany(seam => seam.Members))
			.Concat(Seams.Where(seam => !seam.Composed && seam.Interface == "IFluidPatchPort").SelectMany(seam => seam.Members));

		Assert.Equal(Census(expected), Census(PublicMembers(BridgeFile)));
	}

	[Fact]
	public void Bridge_DeclaresExactlyThePinnedImplementationMembers()
	{
		Assert.True(
			BridgeImplementationMembers.Length >= 5,
			$"the pinned implementation census holds only {BridgeImplementationMembers.Length} name(s) — the pin was emptied, not the class");

		Assert.Equal(Census(BridgeImplementationMembers), Census(NonPublicMembers(BridgeFile)));
	}

	[Fact]
	public void Seam_DeclaresExactlyThePinnedMembers() =>
		Assert.Equal(Census(SeamMembers), Census(DeclaredMembers(SeamFile)));

	/// <summary>
	/// The census's own contract, on samples rather than on the tree: every member
	/// kind it reads is seen — a field per declarator, a property, an event, an
	/// indexer, an expression-bodied method — and a name that only APPEARS in a
	/// doc comment, a cref or a call is not a declaration.
	/// </summary>
	[Theory]
	[InlineData("internal interface ISample { void One(); bool Two { get; } }", "One,Two")]
	[InlineData("internal sealed class Sample { private int _a, _b; public void One() { } public bool Two => true; public event System.Action Changed; }", "Changed,One,Two,_a,_b")]
	[InlineData("internal sealed class Sample { public int this[int i] => i; public static void Three<T>(T value) { } }", "Three,this[]")]
	[InlineData("/// <summary>Calls <see cref=\"Missing\"/> and One().</summary>\ninternal interface ISample { void One(); } // Two()\n", "One")]
	[InlineData("internal sealed class Sample { public Sample(int x) { } ~Sample() { } public static Sample operator +(Sample a, Sample b) => a; private sealed class Nested { public void Hidden() { } } }", "")]
	public void TheCensus_ReadsDeclarationsAndIgnoresMentions(string source, string expected) =>
		Assert.Equal(expected, Census(DeclaredMembersOf(source)), StringComparer.Ordinal);

	/// <summary>The members the file's single top-level type declares.</summary>
	private static string[] DeclaredMembers(string relativePath) =>
		DeclaredMembersOf(RepositoryPaths.ReadText(relativePath));

	private static string[] DeclaredMembersOf(string source) =>
		[.. TypeOf(source).Members.SelectMany(Names)];

	/// <summary>The public half of the same census.</summary>
	private static string[] PublicMembers(string relativePath) =>
		[.. TypeOf(RepositoryPaths.ReadText(relativePath)).Members.Where(HasPublicModifier).SelectMany(Names)];

	/// <summary>The non-public half: the implementation detail the seam does not promise.</summary>
	private static string[] NonPublicMembers(string relativePath) =>
		[.. TypeOf(RepositoryPaths.ReadText(relativePath)).Members.Where(member => !HasPublicModifier(member)).SelectMany(Names)];

	/// <summary>The interfaces one file's type lists after its colon, by simple name.</summary>
	private static string[] BaseList(string relativePath)
	{
		var type = TypeOf(RepositoryPaths.ReadText(relativePath));
		return type.BaseList is null
			? []
			: [.. type.BaseList.Types.Select(baseType => SimpleName(baseType.Type))];
	}

	private static TypeDeclarationSyntax TypeOf(string source) =>
		CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot()
			.DescendantNodes()
			.OfType<TypeDeclarationSyntax>()
			.FirstOrDefault()
		?? throw new InvalidOperationException("no type declaration in the scanned source — this rule would pass by checking nothing");

	private static bool HasPublicModifier(MemberDeclarationSyntax member) =>
		member.Modifiers.Any(SyntaxKind.PublicKeyword);

	/// <summary>
	/// A member's readable name. Constructors, destructors, operators and nested
	/// types contribute nothing: they are not part of what a patch may call
	/// through a seam.
	/// </summary>
	private static IEnumerable<string> Names(MemberDeclarationSyntax member) => member switch
	{
		FieldDeclarationSyntax field => field.Declaration.Variables.Select(variable => variable.Identifier.ValueText),
		EventFieldDeclarationSyntax eventField => eventField.Declaration.Variables.Select(variable => variable.Identifier.ValueText),
		PropertyDeclarationSyntax property => [property.Identifier.ValueText],
		EventDeclarationSyntax @event => [@event.Identifier.ValueText],
		IndexerDeclarationSyntax => ["this[]"],
		MethodDeclarationSyntax method => [method.Identifier.ValueText],
		_ => [],
	};

	private static string SimpleName(TypeSyntax type) => type switch
	{
		GenericNameSyntax generic => generic.Identifier.ValueText,
		QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
		_ => type.ToString(),
	};

	private static string Census(IEnumerable<string> members) =>
		string.Join(",", members.OrderBy(name => name, StringComparer.Ordinal));
}
