using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The generation wrapper's yield classification. The regression this pins is the host-absence
/// wait (batch 20261002-p): UnityEngine's <c>CustomYieldInstruction</c> (WaitUntil) implements
/// <c>IEnumerator</c>, so a wrapper that drives every enumerator recursively drove the wait frame
/// by frame — the waiting member recorded 41 segments against the host's 19, and the
/// layer-modifier replay rewound to the last WAITED frame's state (D70305E2…) instead of the
/// generation's own last segment start (4AFD152F…). A wait is never a generation segment.
/// </summary>
public class GenerationYieldTests
{
	[Fact]
	public void Classify_ACustomYieldInstruction_IsAWait_NotANestedCoroutine() =>
		Assert.Equal(GenerationYield.Kind.Wait, GenerationYield.Classify(isEnumerator: true, isWaitInstruction: true));

	[Fact]
	public void Classify_ANestedGenerationCoroutine_IsDrivenRecursively() =>
		Assert.Equal(GenerationYield.Kind.Nested, GenerationYield.Classify(isEnumerator: true, isWaitInstruction: false));

	[Fact]
	public void Classify_APlainYield_SealsOneSegment() =>
		Assert.Equal(GenerationYield.Kind.Plain, GenerationYield.Classify(isEnumerator: false, isWaitInstruction: false));

	[Fact]
	public void Classify_TheWaitFact_OutranksTheEnumeratorFact() =>
		// Unity's wait types are always enumerators, so this combination is unreachable in
		// production; the case pins the rule's precedence (wait first), not a real input.
		Assert.Equal(GenerationYield.Kind.Wait, GenerationYield.Classify(isEnumerator: false, isWaitInstruction: true));
}
