using System.Linq;
using Mono.Cecil;

namespace CasualtiesUnknownOnline.ContractTool.Snapshot;

/// <summary>
/// The canonical type-name form a snapshot stores. It is deliberately the CLR
/// spelling rather than Cecil's:
///   - namespace-qualified (<c>System.Void</c>, <c>Body</c> for the game's
///     global-namespace types);
///   - nested types joined with <c>+</c> (<c>Outer+Inner</c>), where Cecil says
///     <c>Outer/Inner</c>;
///   - generic instances as <c>Name`1&lt;Arg,Arg&gt;</c>, with the arity suffix
///     kept on the definition name;
///   - arrays as <c>T[]</c> / <c>T[,]</c>, by-ref as <c>T&amp;</c>, pointers as
///     <c>T*</c>, generic parameters by their own name.
/// Matching the runtime's reflection spelling is what lets the contract-row
/// parity gate compare one canonical form on both sides; the gate owns the only
/// bridge from a reflection <c>FullName</c> (assembly-qualified generic
/// arguments) to this form.
/// </summary>
public static class TypeNameFormat
{
	/// <summary>The canonical name of a Cecil type reference; null renders as <c>?</c>.</summary>
	public static string Of(TypeReference? type) => type is null ? "?" : Render(type);

	private static string Render(TypeReference type)
	{
		return type switch
		{
			ArrayType array => Render(array.ElementType) + "[" + new string(',', array.Rank - 1) + "]",
			ByReferenceType byReference => Render(byReference.ElementType) + "&",
			PointerType pointer => Render(pointer.ElementType) + "*",
			GenericParameter parameter => parameter.Name,
			GenericInstanceType generic => Name(generic.ElementType) + "<" + string.Join(",", generic.GenericArguments.Select(Render)) + ">",
			TypeSpecification specification => Render(specification.ElementType),
			_ => Name(type),
		};
	}

	private static string Name(TypeReference type)
	{
		if (type.DeclaringType is not null)
		{
			return Name(type.DeclaringType) + "+" + type.Name;
		}

		return string.IsNullOrEmpty(type.Namespace) ? type.Name : type.Namespace + "." + type.Name;
	}
}
