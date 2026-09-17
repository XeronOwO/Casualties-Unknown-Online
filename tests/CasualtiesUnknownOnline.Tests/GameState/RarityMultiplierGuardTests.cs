using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

/// <summary>
/// The two rarity multipliers are world-generation INPUTS — a layer's loot and trap
/// distribution are scaled by them — so a value the game could not have produced (a
/// NaN or an infinity) must never become the run baseline. The kernel refuses such a
/// baseline by name on BOTH paths: the command path (the host's own capture) rejects
/// it and the apply path (the checkpoint a guest receives) fails the batch, so the
/// guest never projects it into the params the adapter generates from. The adapter
/// carries the same rule as its last line before the live-world write; that site is
/// game-typed and is covered by read-only review plus the simulation suites.
/// </summary>
public class RarityMultiplierGuardTests
{
	private static readonly RunEpoch Epoch = new(1);
	private static readonly ActorId Host = new(1001);

	[Fact]
	public void IsWellFormed_AnAbsentMultiplierIsNotMalformed() =>
		Assert.True(RunRarityMultipliers.IsWellFormed(null));

	[Theory]
	[InlineData(0f)]
	[InlineData(1f)]
	[InlineData(2.5f)]
	public void IsWellFormed_AcceptsWhatTheGameCanAccumulate(float value) =>
		Assert.True(RunRarityMultipliers.IsWellFormed(value));

	[Theory]
	[InlineData(float.NaN)]
	[InlineData(float.PositiveInfinity)]
	[InlineData(float.NegativeInfinity)]
	public void IsWellFormed_RefusesANonFiniteValue(float value) =>
		Assert.False(RunRarityMultipliers.IsWellFormed(value));

	[Theory]
	[InlineData(float.NaN, true)]
	[InlineData(float.PositiveInfinity, true)]
	[InlineData(float.NegativeInfinity, true)]
	[InlineData(float.NaN, false)]
	[InlineData(float.PositiveInfinity, false)]
	[InlineData(float.NegativeInfinity, false)]
	public void StartRun_WithANonFiniteMultiplier_IsRejectedAndNothingIsCommitted(float value, bool onLoot)
	{
		var kernel = new GameStateKernel(Epoch);
		var run = onLoot
			? Run(lootRarity: value)
			: Run(trapRarity: value);

		var decision = kernel.Execute(
			new StartRunCommand(new OperationId(1), Host, Epoch, AuthorityKind.HostOnly, run),
			new CommandContext(Epoch, Host));

		Assert.False(decision.IsAccepted);
		Assert.Equal(RejectionReason.InvariantViolation, decision.Rejection!.Reason);
		Assert.Contains("non-finite", decision.Rejection.Message);
		Assert.Null(kernel.QueryRun());
	}

	/// <summary>
	/// The guard must refuse ONLY what the game cannot produce: 0 is a legitimate
	/// accumulated multiplier (the game scales, it does not clamp), so a run that
	/// reaches it still starts.
	/// </summary>
	[Fact]
	public void StartRun_WithFiniteNonNeutralMultipliers_IsAccepted()
	{
		var kernel = new GameStateKernel(Epoch);

		var decision = kernel.Execute(
			new StartRunCommand(new OperationId(1), Host, Epoch, AuthorityKind.HostOnly, Run(lootRarity: 0f, trapRarity: 2.5f)),
			new CommandContext(Epoch, Host));

		Assert.True(decision.IsAccepted);
		Assert.Equal(0f, kernel.QueryRun()!.LootRarityMultiplier);
		Assert.Equal(2.5f, kernel.QueryRun()!.TrapRarityMultiplier);
	}

	/// <summary>
	/// The guest's path is the WIRE, not the host's capture: a run batch whose row was
	/// rewritten the way a buggy or hostile producer would send it must fail to apply,
	/// so the guest stays without a run baseline instead of generating a layer scaled
	/// by a number that has no meaning.
	/// </summary>
	[Fact]
	public void Apply_WireRunBatchWithANonFiniteMultiplier_Fails()
	{
		var source = new GameStateKernel(Epoch);
		var batch = source.Execute(
			new StartRunCommand(new OperationId(1), Host, Epoch, AuthorityKind.HostOnly, Run()),
			new CommandContext(Epoch, Host)).Batch!;

		var wire = KernelWireMapper.ToWireBatch(batch);
		Assert.Single(wire.Events).RunState!.TrapRarityMultiplier = float.NaN;

		var guest = new GameStateKernel(Epoch);
		var applied = guest.Apply(KernelWireMapper.FromWireBatch(wire, Epoch));

		Assert.False(applied.Success);
		Assert.Contains("non-finite", applied.Error);
		Assert.Null(guest.QueryRun());
	}

	/// <summary>
	/// The RESTORE family is a third way into the kernel baseline, and it does not go through
	/// <c>Execute</c> at all: the archive decode and the wire checkpoint both hand a whole
	/// checkpoint to <c>Restore</c>, which is the one seam they share. A poisoned baseline is
	/// refused there, BEFORE the store is replaced, so it can never be re-projected into the
	/// params a peer generates from.
	/// </summary>
	[Fact]
	public void Restore_ACheckpointCarryingANonFiniteMultiplier_IsRefused()
	{
		var source = new GameStateKernel(Epoch);
		Assert.True(source.Execute(
			new StartRunCommand(new OperationId(1), Host, Epoch, AuthorityKind.HostOnly, Run()),
			new CommandContext(Epoch, Host)).IsAccepted);

		var poisoned = source.CreateCheckpoint() with { Run = Run(trapRarity: float.NaN) };
		var restored = new GameStateKernel(Epoch);

		var result = restored.Restore(poisoned);

		Assert.False(result.Success);
		Assert.Contains("non-finite", result.Error);
		Assert.Null(restored.QueryRun());
	}

	private static RunState Run(float lootRarity = RunRarityMultipliers.Neutral, float trapRarity = RunRarityMultipliers.Neutral) =>
		new(
			42,
			[1, 2, 3],
			BiomeOverride: 0,
			BiomeDepth: 2,
			TotalTraveled: 10,
			LoadedRun: false,
			RunSettings: [new RunSetting("speed", RunSettingKind.Float, FloatValue: 1.5f)],
			LayerIndex: 0,
			LootRarityMultiplier: lootRarity,
			TrapRarityMultiplier: trapRarity);
}
