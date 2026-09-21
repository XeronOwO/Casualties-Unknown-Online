using System;
using System.Reflection;

namespace CasualtiesUnknownOnline.GameAdapter.Capabilities;

/// <summary>
/// A game member a capability depends on that no patch contract can name — the
/// residual decision 199 records for a read that sits inside a compiler-generated
/// lambda. The probe resolves it by reflection and reports a miss as a reason
/// instead of assuming the read still works. (The one row CUO used to declare,
/// <c>PlayerCamera.recipeItemFilter</c>, left with the pinyin patch: the satellite
/// pinyin mod asserts that field in its own contract test now.)
/// </summary>
internal sealed class AdapterMemberProbe
{
	/// <summary>Any visibility, instance or static — the probe asks "is it still there", not "is it public".</summary>
	private const BindingFlags AnyMember =
		BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

	internal AdapterMemberProbe(Type declaringType, string memberName)
	{
		DeclaringType = declaringType;
		MemberName = memberName;
	}

	internal Type DeclaringType { get; }

	internal string MemberName { get; }

	internal bool Resolves() =>
		DeclaringType.GetField(MemberName, AnyMember) is not null
		|| DeclaringType.GetProperty(MemberName, AnyMember) is not null
		|| DeclaringType.GetMethod(MemberName, AnyMember) is not null;

	/// <summary>The reader-facing identity, as the report and the log should name it.</summary>
	internal string Describe() => $"{DeclaringType.Name}.{MemberName}";
}
