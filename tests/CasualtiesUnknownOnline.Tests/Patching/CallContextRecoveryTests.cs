using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The release deadline behind ticket <c>backlog/done/call-context-scope-leak.md</c>. A scope opened in a
/// Harmony Prefix and released in its Postfix is never released when the wrapped ORIGINAL throws — Harmony
/// skips every postfix on that path — and before this contract the sixteenth such leak latched
/// <c>Enter</c> into refusing for the rest of the process: batch <c>20261012-a</c> measured one game-side
/// throw per frame filling the stack in 0.2 s, 11,608 refusals over 34.8 s, and 4,458 of them AFTER the
/// throwing definition had been repaired (nothing else can release an entry whose owner already returned),
/// which is why the client had to be restarted. These cases pin the two halves of the replacement: the bound
/// REFUSES without ending the session, and the frame boundary releases what a frame left open — once per
/// leak episode, not once per frame.
///
/// <para>
/// The class drives process-global static state, so it joins the non-parallel GameAssembly collection like
/// <see cref="CallContextCompositionTests"/>; every case leaves the stack clean through the boundary.
/// </para>
/// </summary>
[Collection(GameAssemblyCollection.Name)]
[Trait("Category", "Integration")]
public class CallContextRecoveryTests
{
	private static readonly Type CallContext = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.CallContext",
		throwOnError: true)!;

	private static readonly Type Origin = CallContext.GetNestedType("Origin", BindingFlags.NonPublic | BindingFlags.Public)
		?? throw new InvalidOperationException("CallContext.Origin not found.");

	private static readonly MethodInfo Enter = CallContext.GetMethod("Enter", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
		?? throw new InvalidOperationException("CallContext.Enter not found.");

	private static readonly PropertyInfo Current = CallContext.GetProperty("Current", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
		?? throw new InvalidOperationException("CallContext.Current not found.");

	/// <summary>Resolved per test so a missing contract fails with its own message rather than a type-initializer trace.</summary>
	private static MethodInfo RequireEndFrame() =>
		CallContext.GetMethod("EndFrame", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
		?? throw new InvalidOperationException(
			"CallContext must declare the frame boundary `internal static string? EndFrame()`: a scope whose owner never reaches its release "
			+ "(a game-side throw inside the wrapped original skips the postfix) has to be released there, or the stack latches every later Enter "
			+ "in this process into refusal and the client has to be restarted (batch 20261012-a).");

	private static MethodInfo RequireIsWithin() =>
		CallContext.GetMethod("IsWithin", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
		?? throw new InvalidOperationException("CallContext.IsWithin not found.");

	[Fact]
	public void TheBound_RefusesInsteadOfEndingTheSession()
	{
		var isWithin = RequireIsWithin();
		var endFrame = RequireEndFrame();
		var open = new List<IDisposable>();
		try
		{
			// Far more Enters than any bound: the old contract threw on the (MaxDepth+1)-th one and every
			// scope-entering patch in the process threw on its own account from then on.
			for (var i = 0; i < 64; i++)
			{
				open.Add(EnterScope("RemoteApply"));
			}

			// The refused scope owns no entry: the innermost answer is still the last one that was pushed,
			// and the chain query does not see the refused origin.
			var refused = EnterScope("Craft");
			open.Add(refused);
			Assert.Equal("RemoteApply", CurrentOriginName());
			Assert.False(
				(bool)isWithin.Invoke(null, [OriginValue("Craft")])!,
				"a refused Enter must not leave an entry behind: the chain query would attribute a mutation to a scope that never opened");
		}
		finally
		{
			foreach (var scope in open)
			{
				scope.Dispose();
			}

			End(endFrame);
		}

		Assert.Equal("LocalAction", CurrentOriginName());
	}

	[Fact]
	public void TheFrameBoundary_ReleasesWhatAFrameLeftOpen_AndNamesIt()
	{
		var isWithin = RequireIsWithin();
		var endFrame = RequireEndFrame();
		End(endFrame); // start from a clean frame: the report is per leak episode, and these cases share the guard's process-global state
		var leaked = new List<IDisposable>();
		try
		{
			leaked.Add(EnterScope("RemoteApply"));
			leaked.Add(EnterScope("Craft"));
			Assert.Equal("Craft", CurrentOriginName());

			var summary = End(endFrame);

			Assert.NotNull(summary);
			Assert.Contains("2 leaked scope(s) released", summary);
			Assert.Contains("RemoteApply x1", summary);
			Assert.Contains("Craft x1", summary);
			Assert.Equal("LocalAction", CurrentOriginName());
			Assert.True(
				(bool)isWithin.Invoke(null, [OriginValue("LocalAction")])!,
				"a released frame leaves no scope behind: no-scope is the implicit bottom of the chain");
		}
		finally
		{
			foreach (var scope in leaked)
			{
				scope.Dispose();
			}

			End(endFrame);
		}

		// The late release of a scope the boundary already released must not pop a live entry: the stack is
		// clean, one real scope opens and closes, and the stack is clean again.
		Assert.Equal("LocalAction", CurrentOriginName());
		using (EnterScope("RemoteApply"))
		{
			Assert.Equal("RemoteApply", CurrentOriginName());
		}

		Assert.Equal("LocalAction", CurrentOriginName());
	}

	[Fact]
	public void ALeakEpisode_IsReportedOnce_AndReArmedByACleanFrame()
	{
		var endFrame = RequireEndFrame();
		End(endFrame); // start from a clean frame: the report is per leak episode, and these cases share the guard's process-global state
		try
		{
			EnterScope("Craft");
			var first = End(endFrame);
			Assert.NotNull(first);
			Assert.Contains("Craft x1", first);

			// The same episode keeps leaking — the measured shape of a per-frame game-side throw: released,
			// but silent, so an operator gets one line instead of one per frame.
			EnterScope("Craft");
			Assert.Null(End(endFrame));

			// A frame that ends clean re-arms the report for the next episode.
			Assert.Null(End(endFrame));
			EnterScope("Craft");
			Assert.NotNull(End(endFrame));
		}
		finally
		{
			End(endFrame);
		}
	}

	[Fact]
	public void LegitimateNesting_IsUntouched()
	{
		var isWithin = RequireIsWithin();
		var endFrame = RequireEndFrame();
		using (EnterScope("RemoteApply"))
		{
			using (EnterScope("InternalReorder"))
			{
				using (EnterScope("CharacterMedicalUse"))
				{
					Assert.Equal("CharacterMedicalUse", CurrentOriginName());
					Assert.True(
						(bool)isWithin.Invoke(null, [OriginValue("RemoteApply")])!,
						"the outer remote application stays visible under two nested classification scopes");
				}

				Assert.Equal("InternalReorder", CurrentOriginName());
			}

			Assert.Equal("RemoteApply", CurrentOriginName());
		}

		Assert.Equal("LocalAction", CurrentOriginName());

		// Real nesting is 2-3 levels and ends inside its own frame: the boundary reports nothing.
		Assert.Null(End(endFrame));
	}

	private static IDisposable EnterScope(string origin) =>
		(IDisposable)Enter.Invoke(null, [OriginValue(origin)])!;

	private static object OriginValue(string origin) => Enum.Parse(Origin, origin);

	private static string CurrentOriginName() => Current.GetValue(null)!.ToString()!;

	private static string? End(MethodInfo endFrame) => (string?)endFrame.Invoke(null, null);
}
