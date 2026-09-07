using System;
using CasualtiesUnknownOnline.Runtime.Session.ProjectionHealth;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public class ProjectionHealthCoordinatorTests
{
	[Fact]
	public void Run_Success_TracksLastSuccessfulRevision()
	{
		var coordinator = CreateCoordinator();

		coordinator.Register("items", static () => { }, static () => 7);
		coordinator.Run("items", 5, static () => { });

		var info = Assert.Single(coordinator.Snapshot());
		Assert.Equal(5ul, info.LastSuccessfulRevision);
		Assert.False(info.Dirty);
		Assert.False(info.Degraded);
		Assert.Equal(0, info.ConsecutiveFailures);
		Assert.Null(info.LastError);
	}

	[Fact]
	public void Run_Failure_MarksDirtyAndDeferRebuildToPump()
	{
		var coordinator = CreateCoordinator();
		var rebuilds = 0;

		coordinator.Register("items", () => rebuilds++, static () => 7);
		coordinator.Run("items", 5, static () => throw new InvalidOperationException("projection boom"));

		var info = Assert.Single(coordinator.Snapshot());
		Assert.True(info.Dirty);
		Assert.Equal(5ul, info.LastFailedRevision);
		Assert.Equal(1, info.ConsecutiveFailures);
		Assert.Equal(1, info.TotalFailures);
		Assert.Equal("projection boom", info.LastError);
		Assert.Equal(0, rebuilds);
	}

	[Fact]
	public void Run_SuccessAfterFailure_KeepsDirtyUntilPumpRebuild()
	{
		var coordinator = CreateCoordinator();
		var rebuilds = 0;

		coordinator.Register("items", () => rebuilds++, static () => 7);
		coordinator.Run("items", 5, static () => throw new InvalidOperationException("first boom"));
		coordinator.Run("items", 6, static () => { });

		var info = Assert.Single(coordinator.Snapshot());
		Assert.True(info.Dirty, "an unrelated later projection success must not clear a failed domain");
		Assert.Equal(6ul, info.LastSuccessfulRevision);

		coordinator.Pump();

		Assert.Equal(1, rebuilds);
		Assert.False(coordinator.IsDirty("items"));
	}

	[Fact]
	public void Pump_RebuildsDirtyDomainFromKernelReadModelAndClearsDirty()
	{
		var coordinator = CreateCoordinator();
		var rebuilds = 0;
		var currentRevision = 6ul;

		coordinator.Register("items", () =>
		{
			rebuilds++;
			currentRevision = 9;
		}, () => currentRevision);

		coordinator.Run("items", 5, static () => throw new InvalidOperationException("projection boom"));
		coordinator.Pump();

		Assert.Equal(1, rebuilds);
		Assert.False(coordinator.IsDirty("items"));
		Assert.False(coordinator.IsDegraded("items"));
		var info = Assert.Single(coordinator.Snapshot());
		Assert.Equal(9ul, info.LastSuccessfulRevision);
		Assert.Equal(0, info.ConsecutiveFailures);
	}

	[Fact]
	public void RepeatedFailures_EscalateToDegradedState()
	{
		var coordinator = CreateCoordinator();

		coordinator.Register("items", static () => { }, static () => 1);
		for (var i = 1; i <= ProjectionHealthCoordinator.DegradeThreshold; i++)
		{
			coordinator.Run("items", (ulong)i, static () => throw new InvalidOperationException("failure"));
		}

		var info = Assert.Single(coordinator.Snapshot());
		Assert.True(info.Degraded);
		Assert.Equal(ProjectionHealthCoordinator.DegradeThreshold, info.ConsecutiveFailures);
		Assert.Equal(ProjectionHealthCoordinator.DegradeThreshold, info.TotalFailures);
	}

	[Fact]
	public void Pump_RebuildFailure_KeepsDirtyAndCountsAsAnotherFailure()
	{
		var coordinator = CreateCoordinator();
		var rebuilds = 0;

		coordinator.Register("items", () =>
		{
			rebuilds++;
			throw new InvalidOperationException("rebuild boom");
		}, static () => 1);

		coordinator.Run("items", 1, static () => throw new InvalidOperationException("projection boom"));
		coordinator.Pump();

		Assert.Equal(1, rebuilds);
		Assert.True(coordinator.IsDirty("items"));
		var info = Assert.Single(coordinator.Snapshot());
		Assert.Equal(2, info.ConsecutiveFailures);
		Assert.Equal("rebuild boom", info.LastError);
	}

	[Fact]
	public void Register_DuplicateDomain_Throws()
	{
		var coordinator = CreateCoordinator();

		coordinator.Register("items", static () => { }, static () => 1);
		Assert.Throws<InvalidOperationException>(() =>
			coordinator.Register("items", static () => { }, static () => 1));
	}

	[Fact]
	public void Register_TypedDomain_GoesThroughGlobalContract()
	{
		var coordinator = CreateCoordinator();
		var rebuilds = 0;
		var revision = 3ul;

		coordinator.Register(new ProjectionDomain("typed-domain", () =>
		{
			rebuilds++;
		}, () => revision));

		coordinator.Run("typed-domain", 3, static () => throw new InvalidOperationException("boom"));
		Assert.True(coordinator.IsDirty("typed-domain"));

		coordinator.Pump();

		Assert.Equal(1, rebuilds);
		Assert.False(coordinator.IsDirty("typed-domain"));
		var info = Assert.Single(coordinator.Snapshot());
		Assert.Equal("typed-domain", info.Domain);
		Assert.Equal(3ul, info.LastSuccessfulRevision);
	}

	[Fact]
	public void Register_TypedNullDomain_Throws()
	{
		var coordinator = CreateCoordinator();
		Assert.Throws<ArgumentNullException>(() => coordinator.Register((IProjectionDomain)null!));
	}

	[Fact]
	public void ProjectionDomain_NullArguments_Throw()
	{
		Assert.Throws<ArgumentNullException>(() => new ProjectionDomain(null!, static () => { }, static () => 1));
		Assert.Throws<ArgumentNullException>(() => new ProjectionDomain("x", null!, static () => 1));
		Assert.Throws<ArgumentNullException>(() => new ProjectionDomain("x", static () => { }, null!));
	}

	private static ProjectionHealthCoordinator CreateCoordinator() =>
		new(NullLogger<ProjectionHealthCoordinator>.Instance);
}
