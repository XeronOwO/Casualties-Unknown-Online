using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.ContractTool.Snapshot;

/// <summary>
/// The string shapes the diff compares members by. Keeping them in one place is
/// what makes "same member" mean one thing everywhere:
///   - <see cref="Identity"/> is the key a member is matched on — for a method
///     the NAME plus parameter types, never the return type, because a return
///     type cannot overload and a return-type move must surface as a signature
///     change rather than as a removal plus an addition;
///   - <see cref="Shape"/> is what a rename candidate has to match — the
///     parameter and return types with no name in them;
///   - <see cref="Signature"/> is what a report prints.
/// </summary>
public static class MemberShape
{
	/// <summary>Match key: name plus parameter types (a method cannot be overloaded by return type).</summary>
	public static string Identity(SnapshotMethod method) => method.Name + "(" + Parameters(method.Parameters) + ")";

	/// <summary>Rename-candidate key: parameter types plus return type, no name.</summary>
	public static string Shape(SnapshotMethod method) => "(" + Parameters(method.Parameters) + "):" + method.Returns;

	/// <summary>Printable signature: return type, name, parameter types.</summary>
	public static string Signature(SnapshotMethod method) => method.Returns + " " + Identity(method);

	/// <summary>The parameter type list, comma-separated.</summary>
	public static string Parameters(IReadOnlyList<SnapshotParameter> parameters) => string.Join(", ", parameters.Select(parameter => parameter.Type));

	/// <summary>The parameter name list, comma-separated (Harmony binds by these names).</summary>
	public static string ParameterNames(IReadOnlyList<SnapshotParameter> parameters) => string.Join(", ", parameters.Select(parameter => parameter.Name));
}
