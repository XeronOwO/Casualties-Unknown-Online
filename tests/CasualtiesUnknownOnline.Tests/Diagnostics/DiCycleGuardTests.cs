using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Diagnostics;

public class DiCycleGuardTests
{
	[Fact]
	public void FieldBackedConstructorCycle_ValidateOnBuildThrowsWithChain()
	{
		var services = new ServiceCollection();
		services.AddSingleton<CycleA>();
		services.AddSingleton<CycleB>();

		var exception = Assert.ThrowsAny<Exception>(() =>
			services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true }));

		Assert.IsType<AggregateException>(exception);
		Assert.Contains("circular dependency", exception.ToString(), StringComparison.OrdinalIgnoreCase);
		Assert.Contains(nameof(CycleA), exception.ToString());
		Assert.Contains(nameof(CycleB), exception.ToString());
	}

	[Fact]
	public void FactorySelfCycle_ResolveThrowsInsteadOfHanging()
	{
		var services = new ServiceCollection();
		services.AddSingleton(p => p.GetRequiredService<FactoryCycleA>());
		DiCycleGuard.WrapFactoryDescriptors(services);

		var provider = services.BuildServiceProvider();
		var exception = Assert.Throws<InvalidOperationException>(() =>
			provider.GetRequiredService<FactoryCycleA>());

		Assert.Contains("circular dependency", exception.Message, StringComparison.OrdinalIgnoreCase);
		Assert.Contains(nameof(FactoryCycleA), exception.Message);
	}

	[Fact]
	public void FactoryCycle_InvokesDiagnosticCallback()
	{
		Exception? reported = null;
		var services = new ServiceCollection();
		services.AddSingleton(p => p.GetRequiredService<FactoryCycleA>());
		DiCycleGuard.WrapFactoryDescriptors(services, exception => reported = exception);

		var provider = services.BuildServiceProvider();
		Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<FactoryCycleA>());

		Assert.NotNull(reported);
		Assert.Contains(nameof(FactoryCycleA), reported!.Message);
	}

	[Fact]
	public void FactoryToConstructorCycle_ResolveThrowsWithFullChain()
	{
		var services = new ServiceCollection();
		services.AddSingleton<FactoryCycleB>();
		services.AddSingleton(p => new FactoryCycleA(p.GetRequiredService<FactoryCycleB>()));
		DiCycleGuard.WrapFactoryDescriptors(services);

		var provider = services.BuildServiceProvider();
		var exception = Assert.Throws<InvalidOperationException>(() =>
			provider.GetRequiredService<FactoryCycleA>());

		Assert.Contains("circular dependency", exception.Message, StringComparison.OrdinalIgnoreCase);
		Assert.Contains(nameof(FactoryCycleA), exception.Message);
		Assert.Contains(nameof(FactoryCycleB), exception.Message);
	}

	[Fact]
	public void ValidFactoryChain_StillResolves()
	{
		var services = new ServiceCollection();
		services.AddSingleton<RealLeaf>();
		services.AddSingleton(p => new RealNode(p.GetRequiredService<RealLeaf>()));
		DiCycleGuard.WrapFactoryDescriptors(services);

		var provider = services.BuildServiceProvider();
		var node = provider.GetRequiredService<RealNode>();

		Assert.NotNull(node);
		Assert.NotNull(node.Leaf);
	}

	[Fact]
	public void WrapFactoryDescriptors_PreservesDescriptorOrder()
	{
		var services = new ServiceCollection();
		services.AddSingleton<RealLeaf>();
		services.AddSingleton(_ => new RealNode(new RealLeaf()));
		var originalOrder = new[] { typeof(RealLeaf), typeof(RealNode) };

		DiCycleGuard.WrapFactoryDescriptors(services);

		Assert.Equal(originalOrder, [.. services.Select(d => d.ServiceType)]);
	}

	private sealed class CycleA(CycleB b)
	{
		public CycleB B { get; } = b;
	}

	private sealed class CycleB(CycleA a)
	{
		public CycleA A { get; } = a;
	}

	private sealed class FactoryCycleA(FactoryCycleB b)
	{
		public FactoryCycleB B { get; } = b;
	}

	private sealed class FactoryCycleB(FactoryCycleA a)
	{
		public FactoryCycleA A { get; } = a;
	}

	private sealed class RealLeaf
	{
	}

	private sealed class RealNode(RealLeaf leaf)
	{
		public RealLeaf Leaf { get; } = leaf;
	}
}
