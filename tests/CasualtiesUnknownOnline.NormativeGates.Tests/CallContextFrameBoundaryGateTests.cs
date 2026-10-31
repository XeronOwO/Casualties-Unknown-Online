using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The call-identity guard's release deadline. <c>CallContext.EndFrame()</c> closes one frame's call scopes, so it must run exactly once per frame and from the
/// frame pump itself — a second call would release scopes the game still holds, and a pump without it would leak a scope forever after one throw inside a patched
/// original.
/// </summary>
/// <remarks>
/// The pump moved to <c>FrameTick.Run</c> (2026-10-31) when the adapter's frame walk got its own type; the boundary moved with it, and the adapter's
/// <c>ICuoService.Update</c> is now the thing that must call the pump. Both halves are asserted, so a future move cannot quietly drop either.
/// </remarks>
[Trait("Category", "Unit")]
public class CallContextFrameBoundaryGateTests
{
	private const string AdapterFile = "src/CasualtiesUnknownOnline.GameAdapter/GameAdapter.cs";
	private const string PumpFile = "src/CasualtiesUnknownOnline.GameAdapter/FrameTick.cs";

	/// <summary>The called expression as Roslyn spells it: an invocation's Expression carries the method name only, never the argument list.</summary>
	private const string BoundaryCall = "CallContext.EndFrame";

	[Fact]
	public void TheFramePump_ClosesTheCallContextFrameExactlyOnce()
	{
		var source = RepositoryPaths.ReadText(PumpFile);
		Assert.True(source.Length > 2000, $"{PumpFile} is only {source.Length} character(s); the check would pass by checking nothing");

		var calls = CountBoundaryCalls(source, "Run");
		Assert.True(
			calls == 1,
			$"{PumpFile} must call {BoundaryCall} exactly once, from `void Run()` — the per-frame pump that is the guard's release deadline. "
			+ $"Found {calls} call(s) (and -1 means the `Run` method itself is gone).");

		// The matcher's own teeth: a call in that method is seen, a method without it is not, and the same
		// call in any other method is not a frame boundary.
		Assert.Equal(1, CountBoundaryCalls("internal class Sample { void Run() { CallContext.EndFrame(); } }", "Run"));
		Assert.Equal(0, CountBoundaryCalls("internal class Sample { void Run() { Other(); } }", "Run"));
		Assert.Equal(-1, CountBoundaryCalls("internal class Sample { void Update() { CallContext.EndFrame(); } }", "Run"));
	}

	[Fact]
	public void TheAdapter_PumpsTheFrameExactlyOnce()
	{
		var source = RepositoryPaths.ReadText(AdapterFile);
		Assert.Contains("_frameTick.Run();", source, StringComparison.Ordinal);
	}

	/// <summary>How many <c>CallContext.EndFrame()</c> calls sit inside the named method's body, or -1 when that method is gone.</summary>
	private static int CountBoundaryCalls(string source, string methodName)
	{
		var method = CSharpSyntaxTree
			.ParseText(source)
			.GetRoot()
			.DescendantNodes()
			.OfType<MethodDeclarationSyntax>()
			.FirstOrDefault(candidate => candidate.Identifier.ValueText == methodName);
		if (method is null)
		{
			return -1;
		}

		return method
			.DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.Count(invocation => invocation.Expression.ToString() == BoundaryCall);
	}
}
