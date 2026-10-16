using System;
using System.Reflection;

namespace CasualtiesUnknownOnline.Runtime.Diagnostics;

/// <summary>
/// The build a running client IS, read once from the assembly that carries it: the
/// <see cref="AssemblyInformationalVersionAttribute"/> the build stamped, which is
/// <c>&lt;version&gt;+&lt;commit&gt;</c> when the SDK could read a git revision, the same with a <c>-dirty</c>
/// marker when the tree it built had uncommitted work, and the bare numeric version when it could read no
/// revision at all. One reader, because three surfaces show it — the startup line, the Online UI's Home page
/// and a world archive (<c>WorldCutWriter</c>) — and deriving it a second time from BepInEx's own metadata is
/// exactly how two surfaces come to name different builds.
///
/// <para>
/// The DISPLAY LENGTH is a decision rather than a default. A LOG gets the whole stamp: a support reader
/// matches it against <c>tools/verify-deploy.ps1</c>'s output character for character. The UI gets
/// <see cref="Short"/>, because a player reads that off a screenshot and types it into a bug report — and the
/// short form keeps every marker the stamp carries, because a screen that showed a clean-looking revision for
/// a build made from uncommitted work is the one surface the marker exists for.
/// </para>
/// </summary>
public static class BuildIdentity
{
	/// <summary>How much of the revision a screen shows: enough to identify the build, short enough to read off a screenshot.</summary>
	public const int ShortRevisionLength = 7;

	/// <summary>
	/// The assembly's own stamp, or <c>"unknown"</c> when it carries none — never a composed string and never a
	/// second shape, so what a surface shows is what the build recorded. (The archive documented
	/// <c>"unknown"</c> for that case; falling back to the AssemblyVersion would put <c>0.1.0.0</c> on one
	/// surface, a shape no other surface ever shows.)
	/// </summary>
	public static string Of(Assembly assembly)
	{
		if (assembly is null)
		{
			throw new ArgumentNullException(nameof(assembly));
		}

		return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
			?? "unknown";
	}

	/// <summary>
	/// The screen form of a stamp: the numeric version, a short revision, and any marker the revision carried.
	/// A stamp with no revision is shown UNCHANGED rather than trimmed — a build that cannot name its commit
	/// says so by naming only what it knows.
	/// </summary>
	public static string Short(string stamp)
	{
		if (string.IsNullOrEmpty(stamp))
		{
			return "unknown";
		}

		// The SDK composes `<version>+<revision>`; it switches to `.` only when InformationalVersion already
		// carried a `+`, a shape this tree never sets, and that shape is shown unchanged rather than trimmed
		// into something the build did not say.
		var plus = stamp.IndexOf("+", StringComparison.Ordinal);
		if (plus < 0 || plus == stamp.Length - 1)
		{
			return stamp;
		}

		var revision = stamp.Substring(plus + 1);
		var marker = string.Empty;
		var dash = revision.IndexOf("-", StringComparison.Ordinal);
		if (dash >= 0)
		{
			marker = revision.Substring(dash);
			revision = revision.Substring(0, dash);
		}

		var shown = revision.Length <= ShortRevisionLength ? revision : revision.Substring(0, ShortRevisionLength);
		return stamp.Substring(0, plus) + "+" + shown + marker;
	}
}
