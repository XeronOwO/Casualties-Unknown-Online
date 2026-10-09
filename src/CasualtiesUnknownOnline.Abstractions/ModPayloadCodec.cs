using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The one decode seam every payload contract CUO itself carries goes through:
/// the status update and the two status projections. A content definition is
/// registered as a typed object and is never serialized (see
/// <see cref="IModContentDefinition"/>), so it is not a contract of this codec.
/// Each travelling contract's <c>FromPayload</c> is a call into this codec.
/// Besides the deserialization it enforces the rule the members themselves
/// declare — a collection member that is null means "none" — because the
/// serializer runs NEITHER a constructor NOR a field initializer: an explicit
/// nil reaches the member's coalescing setter, but a member whose element is
/// ABSENT from the payload is never set at all and would stay null. Only a
/// decode can see that shape, which is why the rule lives at both ends: on the
/// member (a write) and here (a decode).
/// </summary>
internal static class ModPayloadCodec
{
	/// <summary>
	/// A contract graph is a flat data-object graph; this bound is a guard
	/// against a self-referential payload, not a shape any contract has.
	/// </summary>
	private const int MaxGraphDepth = 16;

	/// <summary>
	/// Deserialize a payload into <typeparamref name="T"/> and normalise its
	/// collection members. Returns null when the payload is not a valid
	/// definition under the current contract — a failure the caller reports, so
	/// the framework never fails a whole mod discovery over one bad payload.
	/// </summary>
	internal static T? Decode<T>(byte[]? payload)
		where T : class
	{
		if (payload is null)
		{
			return null;
		}

		try
		{
			using var stream = new MemoryStream(payload);
			var serializer = new DataContractSerializer(typeof(T));
			var definition = serializer.ReadObject(stream) as T;
			NormalizeCollections(definition, 0);
			return definition;
		}
		catch (Exception)
		{
			// Any deserialization failure means the payload is not a valid
			// definition under the current contract; the binder should refuse it
			// rather than fail the whole mod discovery.
			return null;
		}
	}

	/// <summary>
	/// Replaces every null collection member of a decoded contract — and of the
	/// contracts and collection entries it carries — with an empty one, so no
	/// consumer needs a guard, and re-encoding a decoded definition writes an
	/// empty collection instead of an explicit nil.
	/// </summary>
	private static void NormalizeCollections(object? value, int depth)
	{
		if (value is null || depth > MaxGraphDepth)
		{
			return;
		}

		var type = value.GetType();
		if (type.GetCustomAttribute<DataContractAttribute>() is null)
		{
			return;
		}

		foreach (var member in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			if (member.GetCustomAttribute<DataMemberAttribute>() is null || !member.CanRead)
			{
				continue;
			}

			if (member.GetValue(value) is not { } current)
			{
				// The absent shape: the payload carried no element for this
				// member, so the serializer never ran its setter and no
				// initializer ever ran either.
				if (member.CanWrite && IsCollection(member.PropertyType))
				{
					member.SetValue(value, EmptyCollection(member.PropertyType));
				}

				continue;
			}

			if (member.PropertyType.GetCustomAttribute<DataContractAttribute>() is not null)
			{
				NormalizeCollections(current, depth + 1);
			}
			else if (IsCollection(member.PropertyType) && member.PropertyType != typeof(byte[]))
			{
				foreach (var entry in (IEnumerable)current)
				{
					NormalizeCollections(entry, depth + 1);
				}
			}
		}
	}

	private static bool IsCollection(Type type) =>
		type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

	private static object EmptyCollection(Type type) =>
		type.IsArray ? Array.CreateInstance(type.GetElementType()!, 0) : Activator.CreateInstance(type)!;
}
