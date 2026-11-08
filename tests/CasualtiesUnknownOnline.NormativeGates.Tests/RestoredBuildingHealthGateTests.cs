using System;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// Pins the restored building-health rule: a DESTROYED fact whose building the regenerated layer does
/// not have is satisfied (the damage the player did is that the building is gone), while a LIVE-health
/// fact with no entity stays a refused row. The rule lives in the health applier in
/// `src/CasualtiesUnknownOnline.GameAdapter/World/WorldBuildingEntitySync.cs`; this gate reads the
/// source it would otherwise drift from. Blind spots: it does not prove the applier runs — the runtime
/// proof is the acceptance reading (`docs/evidence/acceptance/world-entity-restore-20261108-s2.md`).
/// </summary>
public sealed class RestoredBuildingHealthGateTests
{
	private const string File = "src/CasualtiesUnknownOnline.GameAdapter/World/WorldBuildingEntitySync.cs";

	[Fact]
	public void TheDestroyedFact_IsSatisfiedBeforeTheRefusal()
	{
		var text = RepositoryPaths.ReadText(File);
		var destroyed = text.IndexOf("if (health < 0.5f)", StringComparison.Ordinal);
		var refusal = text.IndexOf("Building-entity health snapshot at ({X}, {Y}) — no entity there", StringComparison.Ordinal);
		Assert.True(destroyed >= 0, "the destroyed-fact branch is gone — a destroyed building with no entity would be refused again");
		Assert.True(refusal > destroyed, "the refusal branch must come after the destroyed-fact branch");
	}

	[Fact]
	public void TheApplier_StillWritesLiveHealth()
	{
		var text = RepositoryPaths.ReadText(File);
		Assert.Contains("entity.health = health;", text, StringComparison.Ordinal);
	}

	[Fact]
	public void AnOpenedFact_WithNoEntity_IsSatisfied()
	{
		var text = RepositoryPaths.ReadText(File);
		var note = text.IndexOf("the opened fact is satisfied", StringComparison.Ordinal);
		Assert.True(note >= 0, "an opened lockable that leaves the layer is refused again — its gone state IS the opened state");
		Assert.DoesNotContain("Building entity open at {Pos} — no entity there (moved or already gone)", text, StringComparison.Ordinal);
	}
}
