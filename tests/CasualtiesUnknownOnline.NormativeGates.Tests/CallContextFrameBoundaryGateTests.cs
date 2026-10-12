using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The wiring the guard cannot check for itself: <c>CallContext.EndFrame()</c> releases the scopes a frame
/// leaked (ticket <c>backlog/done/call-context-scope-leak.md</c>), and it only runs because the adapter's
/// per-frame service pump calls it. Delete that one call and every behavioural case stays green while the
/// latch comes back — batch <c>20261012-a</c> measured what that costs: sixteen leaks filled the stack in
/// 0.2 s, 11,608 Enters were refused over 34.8 s, and 4,458 of them came after the throw that caused them had
/// been repaired, so the client had to be restarted. The call is therefore pinned by shape — exactly once,
/// inside <c>ICuoService.Update</c> — and the matcher is pinned with its own positive, negative and
/// wrong-method samples.
/// </summary>
public class CallContextFrameBoundaryGateTests
{
	private const string AdapterFile = "src/CasualtiesUnknownOnline.GameAdapter/GameAdapter.cs";

	/// <summary>The called expression as Roslyn spells it: an invocation's Expression carries the method name only, never the argument list.</summary>
	private const string BoundaryCall = "CallContext.EndFrame";

	[Fact]
	public void TheAdaptersFramePump_ClosesTheCallContextFrameExactlyOnce()
	{
		var source = RepositoryPaths.ReadText(AdapterFile);
		Assert.True(source.Length > 2000, $"{AdapterFile} is only {source.Length} character(s); the check would pass by checking nothing");

		var calls = CountBoundaryCalls(source);
		Assert.True(
			calls == 1,
			$"{AdapterFile} must call {BoundaryCall} exactly once, from `void ICuoService.Update()` — the per-frame pump that is the guard's release deadline. "
			+ $"Found {calls} call(s) (and -1 means the `ICuoService.Update` method itself is gone).");

		// The matcher's own teeth: a call in that method is seen, a method without it is not, and the same
		// call in any other method is not a frame boundary.
		Assert.Equal(1, CountBoundaryCalls("internal class Sample { void ICuoService.Update() { CallContext.EndFrame(); } }"));
		Assert.Equal(0, CountBoundaryCalls("internal class Sample { void ICuoService.Update() { Other(); } }"));
		Assert.Equal(-1, CountBoundaryCalls("internal class Sample { void Update() { CallContext.EndFrame(); } }"));
	}

	/// <summary>How many <c>CallContext.EndFrame()</c> calls sit inside the adapter's <c>ICuoService.Update</c> body, or -1 when that method is gone.</summary>
	private static int CountBoundaryCalls(string source)
	{
		var method = CSharpSyntaxTree
			.ParseText(source)
			.GetRoot()
			.DescendantNodes()
			.OfType<MethodDeclarationSyntax>()
			.FirstOrDefault(candidate =>
				candidate.ExplicitInterfaceSpecifier?.Name.ToString() == "ICuoService"
				&& candidate.Identifier.ValueText == "Update");
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
