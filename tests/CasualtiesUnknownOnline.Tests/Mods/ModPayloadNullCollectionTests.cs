using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Xml.Linq;
using CasualtiesUnknownOnline.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// One rule for every collection member of every mod-authored payload contract
/// in Abstractions: <b>null means empty</b>, and the member itself owns that
/// rule. A member can be null two ways, and the fix has to answer both: an
/// explicit nil reaches the member's coalescing setter, while an element the
/// payload OMITS is never set at all — the serializer runs no constructor and
/// no initializer — so only the decode seam can repair it. A provider that
/// dereferences either shape without a guard turns "I did not set this" into a
/// definition the binder skips with a logged exception.
///
/// This suite is the census: its rows are DISCOVERED from the contracts, so a
/// new collection member becomes a new row automatically and a member that
/// forgets the rule fails here instead of in a provider months later. A member
/// that is nested (a list on a behaviour DTO, on a sprite animation, on a
/// container) is reached through the contract that carries it, which is the
/// same <c>ToPayload</c>/<c>FromPayload</c> pair a provider decodes.
/// </summary>
public class ModPayloadNullCollectionTests
{
	/// <summary>
	/// The number of collection members the payload contracts carry. It is a
	/// floor, not a formality: adding or removing a member must move this number
	/// in the same change, which is what makes the census a deliberate act.
	/// </summary>
	private const int ExpectedMemberCount = 27;

	private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

	private static readonly IReadOnlyList<CollectionMember> Members = DiscoverCollectionMembers();

	private static readonly Dictionary<string, CollectionMember> MembersByName =
		Members.ToDictionary(member => member.Name, StringComparer.Ordinal);

	/// <summary>One row per collection member, so a failure names the exact member.</summary>
	public static IEnumerable<object[]> CollectionMemberNames =>
		Members.Select(member => new object[] { member.Name });

	[Fact]
	public void Census_CoversEveryCollectionMemberOfEveryPayloadContract()
	{
		Assert.Equal(ExpectedMemberCount, Members.Count);

		// The scan is a TYPE SHAPE rule, and both directions are pinned: a
		// non-collection member of the same contract is not a row, a nested
		// contract's collection member is one with the parent as its carrier,
		// and a contract that is not part of a payload at all is not a row.
		Assert.DoesNotContain(Members, member => member.Property.Name == nameof(ModItemDefinition.DisplayName));
		Assert.DoesNotContain(Members, member => member.Carrier == typeof(ModManifest));
		Assert.Contains(Members, member => member is
		{ Carrier: var carrier, Property.Name: "TagRestriction" } && carrier == typeof(ModItemDefinition));
		Assert.Contains(Members, member => member is
		{ Carrier: var carrier, Property.Name: "FramePaths" } && carrier == typeof(ModItemDefinition));
		Assert.Contains(Members, member => member.Property.Name == nameof(ModStatusUpdate.Value));

		// Every member reads non-null on a CONSTRUCTED instance. That is the
		// member initializer doing its job and it says nothing about a decoded
		// one: the serializer runs no initializer, which is why the theory below
		// drives the omitted shape separately.
		Assert.All(Members, member => Assert.NotNull(member.Property.GetValue(Reached(NewCarrier(member), member))));

		// The assembly has no collection member outside this census: the payload
		// contracts above plus the declarations a mod builds in code, which own
		// the same rule at their own construction point (the case below pins
		// them). A new one anywhere fails here until it is accounted for. The
		// scan is over public CLASSES because an interface member is a read-only
		// view the framework answers, not a declaration a mod fills in.
		Assert.Equal(
			[
				.. new[]
				{
					"CuoModAttribute.Dependencies",
					"ModConsoleCommand.ArgumentKinds",
					// Not a mod-authored member: the registry's defensive copy of
					// bytes the registration policy already refused when null, so
					// the accessor can never answer null either.
					"ModContentDefinition.Data",
					"ModManifest.Dependencies",
					"ModPacket.Handlers",
					"ModStatusMoodleRequest.Payload",
				}
					.Concat(Members.Select(member => member.Name))
					.OrderBy(name => name, StringComparer.Ordinal),
			],
			EveryCollectionMemberInTheAssembly());
	}

	[Fact]
	public void CodeConstructedDeclarations_TreatNullAsNone()
	{
		Assert.Empty(new CuoModAttribute("mod.a", "A", "1.0.0") { Dependencies = null! }.Dependencies);
		Assert.Empty(new ModManifest("mod.a", "A", "1.0.0", NetworkMode.Synchronized, null, dependencies: null!).Dependencies);
		Assert.Empty(new ModPacket("packet.a", ModPacketSender.AnyMember, ModPacketDelivery.EveryMember, (ModPacketHandler[])null!).Handlers);
		Assert.Empty(new ModStatusMoodleRequest { Payload = null! }.Payload);
		Assert.Empty(new ModConsoleCommand(
			"echo", "Echo a line", "echo <text>", CommandPermission.Anyone, null!, _ => null).ArgumentKinds);
	}

	private static IReadOnlyList<string> EveryCollectionMemberInTheAssembly() =>
		[.. typeof(IModContent).Assembly.GetTypes()
			.Where(type => type.IsClass && (type.IsPublic || type.IsNestedPublic))
			.SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(property => IsCollection(property.PropertyType))
				.Select(property => $"{type.Name}.{property.Name}"))
			.OrderBy(name => name, StringComparer.Ordinal)];

	[Theory]
	[MemberData(nameof(CollectionMemberNames))]
	public void ExplicitlyNullCollection_IsNoneInBothPaths(string memberName)
	{
		var member = MembersByName[memberName];
		var definition = NewCarrier(member);

		// A payload we built ourselves never carries an explicit nil for a
		// collection member, whatever the mod assigned.
		var payload = ToPayload(member.Carrier, definition);
		Assert.False(IsNil(payload, member), $"{member.Name} was serialized as an explicit nil.");

		// A mod's own serializer, hand-written bytes or an older payload can
		// still carry one. Decoding it must yield "none".
		var restored = FromPayload(member.Carrier, PayloadWithExplicitNil(payload, member));
		AssertEmpty(member, member.Property.GetValue(Reached(restored, member)));

		// And assigning null in C# is the same statement.
		member.Property.SetValue(Reached(definition, member), null);
		AssertEmpty(member, member.Property.GetValue(Reached(definition, member)));

		// The third shape is the element being ABSENT, which is what a
		// hand-written payload produces. The serializer runs no constructor and
		// no field initializer, so the member's own setter never runs either and
		// only the decode seam can answer for it.
		var omitted = FromPayload(member.Carrier, PayloadWithoutMember(payload, member));
		AssertEmpty(member, member.Property.GetValue(Reached(omitted, member)));

		// The decoded contract is repaired rather than merely hidden: encoding
		// it again writes an empty collection, never a nil.
		Assert.False(
			IsNil(ToPayload(member.Carrier, omitted), member),
			$"{member.Name} was re-encoded as an explicit nil after decoding an omitted element.");
	}

	private static IReadOnlyList<CollectionMember> DiscoverCollectionMembers()
	{
		var contracts = typeof(IModContent).Assembly.GetTypes()
			.Where(type => type.IsClass && type.GetCustomAttribute<DataContractAttribute>() is not null)
			.ToList();
		var carriers = FindCarriers(contracts);

		return
		[
			.. contracts
				.Where(carriers.ContainsKey)
				.SelectMany(contract => contract.GetProperties(BindingFlags.Public | BindingFlags.Instance)
					.Where(property => property.GetCustomAttribute<DataMemberAttribute>() is not null
						&& IsCollection(property.PropertyType))
					.Select(property => new CollectionMember(carriers[contract].Root, carriers[contract].Path, property)))
				.OrderBy(member => member.Name, StringComparer.Ordinal),
		];
	}

	/// <summary>
	/// Every contract's way into a payload: the root contract that owns the
	/// <c>ToPayload</c>/<c>FromPayload</c> pair plus the member path that reaches
	/// it. A contract with no path is not carried by any payload and its members
	/// are not rows — the census count is what keeps that from being silent.
	/// </summary>
	private static Dictionary<Type, Carrier> FindCarriers(IReadOnlyList<Type> contracts)
	{
		var contractTypes = contracts.ToHashSet();
		var carriers = new Dictionary<Type, Carrier>();
		var queue = new Queue<Type>();
		foreach (var root in contracts.Where(IsPayloadRoot).OrderBy(type => type.Name, StringComparer.Ordinal))
		{
			carriers[root] = new Carrier(root, []);
			queue.Enqueue(root);
		}

		while (queue.Count > 0)
		{
			var current = queue.Dequeue();
			foreach (var step in current.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(property => contractTypes.Contains(property.PropertyType)))
			{
				if (carriers.ContainsKey(step.PropertyType))
				{
					continue;
				}

				carriers[step.PropertyType] = new Carrier(carriers[current].Root, [.. carriers[current].Path, step]);
				queue.Enqueue(step.PropertyType);
			}
		}

		return carriers;
	}

	private static bool IsPayloadRoot(Type type) =>
		type.GetMethod("ToPayload", BindingFlags.Public | BindingFlags.Instance) is not null
		&& type.GetMethod("FromPayload", BindingFlags.Public | BindingFlags.Static) is not null;

	private static bool IsCollection(Type type) =>
		type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

	/// <summary>An instance of the carrier with every step on the way to the member filled in.</summary>
	private static object NewCarrier(CollectionMember member) => Fill(Activator.CreateInstance(member.Carrier)!, member.Path);

	private static object Fill(object instance, IReadOnlyList<PropertyInfo> path)
	{
		var current = instance;
		foreach (var step in path)
		{
			var next = Activator.CreateInstance(step.PropertyType)!;
			step.SetValue(current, next);
			current = next;
		}

		return instance;
	}

	private static object Reached(object carrier, CollectionMember member)
	{
		var current = carrier;
		foreach (var step in member.Path)
		{
			current = step.GetValue(current)
				?? throw new InvalidOperationException($"{step.DeclaringType?.Name}.{step.Name} came back null from the payload.");
		}

		return current;
	}

	private static byte[] ToPayload(Type root, object definition) =>
		(byte[])root.GetMethod("ToPayload", BindingFlags.Public | BindingFlags.Instance)!.Invoke(definition, null)!;

	private static object FromPayload(Type root, byte[] payload)
	{
		var fromPayload = root.GetMethod("FromPayload", BindingFlags.Public | BindingFlags.Static)
			?? throw new InvalidOperationException($"{root.Name}.FromPayload(byte[]) not found.");
		return fromPayload.Invoke(null, [payload])
			?? throw new InvalidOperationException(
				$"{root.Name}.FromPayload refused a payload that carries an explicit nil collection.");
	}

	/// <summary>
	/// Rewrites one member of a real payload into an explicit nil, which is the
	/// shape a mod's own serializer produces for a null list. The element is
	/// found through the carrier path and replaced in place, so member order
	/// (the contract's <c>Order</c>) and every sibling stay untouched.
	/// </summary>
	private static byte[] PayloadWithExplicitNil(byte[] payload, CollectionMember member)
	{
		var document = XDocument.Parse(Encoding.UTF8.GetString(payload));
		var element = FindMemberElement(document, member);
		element.RemoveNodes();
		element.SetAttributeValue(Xsi + "nil", "true");
		return Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
	}

	/// <summary>
	/// Removes one member's element entirely — the shape a hand-written or
	/// foreign payload has, and the one the serializer answers with a member it
	/// never sets at all.
	/// </summary>
	private static byte[] PayloadWithoutMember(byte[] payload, CollectionMember member)
	{
		var document = XDocument.Parse(Encoding.UTF8.GetString(payload));
		FindMemberElement(document, member).Remove();
		return Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
	}

	private static bool IsNil(byte[] payload, CollectionMember member) =>
		FindMemberElement(XDocument.Parse(Encoding.UTF8.GetString(payload)), member)
			.Attribute(Xsi + "nil")?.Value == "true";

	private static XElement FindMemberElement(XDocument document, CollectionMember member)
	{
		var scope = document.Root
			?? throw new InvalidOperationException("the payload has no root element.");
		foreach (var step in member.Path)
		{
			scope = scope.Elements().FirstOrDefault(element => element.Name.LocalName == step.Name)
				?? throw new InvalidOperationException($"the payload carries no <{step.Name}> element.");
		}

		return scope.Elements().FirstOrDefault(element => element.Name.LocalName == member.Property.Name)
			?? throw new InvalidOperationException(
				$"the payload carries no <{member.Property.Name}> element under <{scope.Name.LocalName}>.");
	}

	private static void AssertEmpty(CollectionMember member, object? value)
	{
		Assert.True(value is not null, $"{member.Name} decoded as null instead of an empty collection.");
		Assert.Empty((IEnumerable)value!);
	}

	private sealed record Carrier(Type Root, IReadOnlyList<PropertyInfo> Path);

	private sealed record CollectionMember(Type Carrier, IReadOnlyList<PropertyInfo> Path, PropertyInfo Property)
	{
		internal string Name => $"{Property.DeclaringType!.Name}.{Property.Name}";
	}
}
