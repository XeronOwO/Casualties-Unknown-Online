using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// One rule for every collection member of every mod-authored declaration in
/// Abstractions: <b>null means empty</b>, and the declaration itself owns that
/// rule.
///
/// The rule used to need two halves, because a payload had two shapes: an
/// explicit nil reached the member's coalescing setter, while an element the
/// payload OMITTED was never set at all — the serializer runs no constructor and
/// no initializer — so only the decode seam could repair it. Neither half has a
/// payload to answer for now: a content definition is the typed object a mod
/// built (<see cref="IModContentDefinition"/>), a status value is a
/// <see cref="ModValue"/>, and what travels is CUO's own encoding of that value
/// rather than a serialized declaration. What is left is the MEMBER half, which
/// is what every mod-built declaration answers for, and this suite is its census.
///
/// The rows are DISCOVERED, so a new collection member becomes a new row
/// automatically and a member that forgets the rule fails here instead of in a
/// provider months later. The scan reaches a member a mod can WRITE: a
/// read-only collection member is a view the framework answers (a
/// <see cref="ModValue"/>'s items and fields, for instance), not a declaration a
/// mod fills in, so it is not a row — and the equality assertion below still
/// fails if a new class or member appears outside the census.
/// </summary>
public class ModNullCollectionRuleTests
{
	/// <summary>
	/// The number of collection members a mod fills in on a declaration it builds
	/// in code — the runtime moodle request included, because a caller builds it
	/// with an object initializer. It is a floor, not a formality: adding or
	/// removing a member must move this number in the same change, which is what
	/// makes the census a deliberate act.
	///
	/// It moved from 27 to 26 when the moodle request's <c>Payload</c> became a
	/// <see cref="ModValue"/>: a value is not a collection, so the member the old
	/// rule existed for is not a collection member any more — the type change is
	/// the reason, not a dropped row.
	/// </summary>
	private const int ExpectedConstructedMemberCount = 26;

	/// <summary>
	/// The declarations whose only constructor takes arguments (an attribute, a
	/// manifest, a packet or a command), so the discovery below cannot instantiate
	/// them and their own test pins them. A declaration a caller can build with an
	/// object initializer is discovered like every other mod-built declaration.
	/// </summary>
	private static readonly string[] ParameterObjectMembers =
	[
		"CuoModAttribute.Dependencies",
		"ModConsoleCommand.ArgumentKinds",
		"ModManifest.Dependencies",
		"ModPacket.Handlers",
	];

	/// <summary>
	/// The value model's READ-ONLY collection members: the items of a list and the
	/// fields of a map are views the framework answers, not declarations a mod
	/// fills in, so the null rule does not reach them and they are not rows. They
	/// are NAMED rather than filtered out, so a new read-only collection member
	/// still fails the census below.
	/// </summary>
	private static readonly string[] ReadOnlyValueViews =
	[
		"ModValue.Fields",
		"ModValue.Items",
	];

	private static readonly IReadOnlyList<ConstructedMember> ConstructedMembers = DiscoverConstructedMembers();

	private static readonly Dictionary<string, ConstructedMember> ConstructedMembersByName =
		ConstructedMembers.ToDictionary(member => member.Name, StringComparer.Ordinal);

	/// <summary>One row per mod-built declaration member, so a failure names the exact member.</summary>
	public static IEnumerable<object[]> ConstructedMemberNames =>
		ConstructedMembers.Select(member => new object[] { member.Name });

	[Fact]
	public void Census_CoversEveryModAuthoredDeclarationTheAssemblyBuilds()
	{
		Assert.Equal(ExpectedConstructedMemberCount, ConstructedMembers.Count);

		// Both directions: a member a mod fills in is a row — the item definition
		// carries its own list, its nested container and its nested sprite
		// animation each carry theirs — while a value's read-only views are not,
		// and the declarations that need constructor arguments are named
		// separately rather than silently dropped.
		Assert.Contains(ConstructedMembers, member => member.Name == "ModItemDefinition.SpawnComponents");
		Assert.Contains(ConstructedMembers, member => member.Name == "ModItemDefinition.CustomData");
		Assert.Contains(ConstructedMembers, member => member.Name == "ModItemContainer.TagRestriction");
		Assert.Contains(ConstructedMembers, member => member.Name == "ModItemSpriteAnimation.FramePaths");
		Assert.Contains(ConstructedMembers, member => member.Name == "ModRecipeDefinition.Ingredients");
		Assert.DoesNotContain(ConstructedMembers, member => member.Carrier == typeof(ModValue));
		Assert.DoesNotContain(ConstructedMembers, member => member.Carrier == typeof(ModStatusUpdate));

		// The assembly has no collection member outside this census: the rows
		// above, the declarations that need constructor arguments and the value
		// model's read-only views. A new one anywhere fails here until it is
		// accounted for. The scan is over public CLASSES because an interface
		// member is a read-only view the framework answers, not a declaration a mod
		// fills in — the same reason the value model's views carry their own group
		// rather than a row.
		Assert.Equal(
			[
				.. ParameterObjectMembers
					.Concat(ReadOnlyValueViews)
					.Concat(ConstructedMembers.Select(member => member.Name))
					.OrderBy(name => name, StringComparer.Ordinal),
			],
			EveryCollectionMemberInTheAssembly());

		// Every one of them reads non-null before anything is assigned: that is
		// the initializer, and it is what keeps a provider from dereferencing a
		// list the mod never filled in.
		Assert.All(ConstructedMembers, member =>
		{
			var carrier = Activator.CreateInstance(member.Carrier)!;
			Assert.NotNull(member.Property.GetValue(carrier));
		});
	}

	[Fact]
	public void ParameterObjectDeclarations_TreatNullAsNone()
	{
		Assert.Empty(new CuoModAttribute("mod.a", "A", "1.0.0") { Dependencies = null! }.Dependencies);
		Assert.Empty(new ModManifest("mod.a", "A", "1.0.0", NetworkMode.Synchronized, null, dependencies: null!).Dependencies);
		Assert.Empty(new ModPacket("packet.a", ModPacketSender.AnyMember, ModPacketDelivery.EveryMember, (ModPacketHandler[])null!).Handlers);
		Assert.Empty(new ModConsoleCommand(
			"echo", "Echo a line", "echo <text>", CommandPermission.Anyone, null!, _ => null).ArgumentKinds);
	}

	[Theory]
	[MemberData(nameof(ConstructedMemberNames))]
	public void NullCollectionWrite_IsNone(string memberName)
	{
		var member = ConstructedMembersByName[memberName];
		var declaration = Activator.CreateInstance(member.Carrier)!;

		member.Property.SetValue(declaration, null);

		AssertEmpty(member, member.Property.GetValue(declaration));
	}

	private static IReadOnlyList<string> EveryCollectionMemberInTheAssembly() =>
		[.. typeof(IModContent).Assembly.GetTypes()
			.Where(type => type.IsClass && (type.IsPublic || type.IsNestedPublic))
			.SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(property => IsCollection(property.PropertyType))
				.Select(property => $"{type.Name}.{property.Name}"))
			.OrderBy(name => name, StringComparer.Ordinal)];

	/// <summary>
	/// Every collection member of every declaration a mod builds in code: a
	/// public class the assembly exposes that a mod can instantiate with no
	/// arguments. The ones that need constructor arguments are named by
	/// <see cref="ParameterObjectMembers"/> and pinned by their own test, and the
	/// equality assertion in the census fails if a class moves between the two
	/// groups.
	/// </summary>
	private static IReadOnlyList<ConstructedMember> DiscoverConstructedMembers() =>
		[.. typeof(IModContent).Assembly.GetTypes()
			.Where(type => type.IsClass
				&& (type.IsPublic || type.IsNestedPublic)
				&& type.GetConstructor(Type.EmptyTypes) is not null)
			.SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(property => IsCollection(property.PropertyType))
				.Select(property => new ConstructedMember(type, property)))
			.OrderBy(member => member.Name, StringComparer.Ordinal)];

	private static bool IsCollection(Type type) =>
		type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

	private static void AssertEmpty(ConstructedMember member, object? value)
	{
		Assert.True(value is not null, $"{member.Name} read back null instead of an empty collection.");
		Assert.Empty((IEnumerable)value!);
	}

	private sealed record ConstructedMember(Type Carrier, PropertyInfo Property)
	{
		internal string Name => $"{Carrier.Name}.{Property.Name}";
	}
}
