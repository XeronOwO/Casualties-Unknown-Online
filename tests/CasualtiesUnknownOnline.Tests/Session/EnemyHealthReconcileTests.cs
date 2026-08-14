using System;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The guest-side enemy health reconciliation (EnemyHealthReconcile): a guest's
/// local attack on a frozen enemy drops its health for immediate feedback, but
/// the host's batch overwrites with the authoritative value that has not yet
/// absorbed the in-flight report. The machine must preserve the local drop
/// across overwrites and clear it once the host's health catches up.
/// </summary>
public class EnemyHealthReconcileTests
{
	[Fact]
	public void LocalDamage_IsPreservedAcrossTheBatch()
	{
		var reconcile = new EnemyHealthReconcile();

		reconcile.RecordLocalDamage(10f);
		var display = reconcile.Reconcile(100f); // host still at 100 — the report is in flight

		Assert.True(Math.Abs(90f - display) < 0.001f, "the local drop must survive the host's stale overwrite");
	}

	[Fact]
	public void HostHealthDrop_ClearsThePending()
	{
		var reconcile = new EnemyHealthReconcile();

		reconcile.RecordLocalDamage(10f);
		reconcile.Reconcile(100f); // baseline
		var display = reconcile.Reconcile(90f); // host applied the 10 damage

		Assert.True(Math.Abs(90f - display) < 0.001f, "once the host applies the damage, the display converges to the host health (no double-count)");
	}

	[Fact]
	public void MultipleLocalAttacks_Accumulate()
	{
		var reconcile = new EnemyHealthReconcile();

		reconcile.RecordLocalDamage(10f);
		reconcile.RecordLocalDamage(5f);
		var display = reconcile.Reconcile(100f);

		Assert.True(Math.Abs(85f - display) < 0.001f, "every local attack contributes to the pending damage");
	}

	[Fact]
	public void HostDropFromAnotherSource_ConvergesToHostHealth()
	{
		var reconcile = new EnemyHealthReconcile();

		reconcile.RecordLocalDamage(10f);
		reconcile.Reconcile(100f); // baseline
								   // Another player's 5 damage plus our 10 land on the host together.
		var display = reconcile.Reconcile(85f);

		Assert.True(Math.Abs(85f - display) < 0.001f, "over-clearing is harmless — the display converges to the host health");
	}

	[Fact]
	public void HostHealthIncrease_KeepsThePending()
	{
		var reconcile = new EnemyHealthReconcile();

		reconcile.RecordLocalDamage(10f);
		reconcile.Reconcile(100f); // baseline
		var display = reconcile.Reconcile(110f); // the host healed — the local damage is still pending

		Assert.True(Math.Abs(100f - display) < 0.001f, "a heal must not clear the pending local damage");
	}

	[Fact]
	public void EnemyDeath_ClearsPendingAndDisplaysZero()
	{
		var reconcile = new EnemyHealthReconcile();

		reconcile.RecordLocalDamage(10f);
		reconcile.Reconcile(10f); // baseline — the enemy is already near death
		var display = reconcile.Reconcile(0f); // the host applied the killing blow

		Assert.True(Math.Abs(0f - display) < 0.001f, "a dead enemy displays zero, pending cleared");
	}
}
