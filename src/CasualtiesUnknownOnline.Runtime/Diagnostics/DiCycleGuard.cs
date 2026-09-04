using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace CasualtiesUnknownOnline.Runtime.Diagnostics;

/// <summary>
/// Composition-root guard against accidental DI circular dependencies.
/// </summary>
/// <remarks>
/// Microsoft DI's <c>ValidateOnBuild</c> detects constructor/implementation-type
/// cycles before the provider is used. Factory-based cycles are not visible to
/// that static validation, so this guard wraps every factory's
/// <see cref="IServiceProvider"/> and records the current resolution chain. If a
/// factory re-enters a service already on the chain, it throws immediately with
/// the full path instead of letting the provider recurse until the process hangs.
/// </remarks>
internal static class DiCycleGuard
{
	[ThreadStatic]
	private static List<Type>? _resolutionStack;

	/// <summary>
	/// Wraps every factory descriptor so factory-mediated resolutions are
	/// re-entrancy-checked. Keeps the descriptor count, service type, lifetime,
	/// and registration order unchanged.
	/// </summary>
	internal static void WrapFactoryDescriptors(IServiceCollection services, Action<Exception>? onCycle = null)
	{
		var factoryServiceTypes = new HashSet<Type>();
		foreach (var descriptor in services)
		{
			if (descriptor.ImplementationFactory is not null)
			{
				factoryServiceTypes.Add(descriptor.ServiceType);
			}
		}

		for (var i = 0; i < services.Count; i++)
		{
			var descriptor = services[i];
			if (descriptor.ImplementationFactory is null)
			{
				continue;
			}

			var factory = descriptor.ImplementationFactory;
			var serviceType = descriptor.ServiceType;
			services[i] = new ServiceDescriptor(
				serviceType,
				provider =>
				{
					var guardedProvider = new GuardedProvider(provider, factoryServiceTypes, onCycle);
					using (Enter(serviceType, onCycle))
					{
						return factory(guardedProvider);
					}
				},
				descriptor.Lifetime);
		}
	}

	private static IDisposable Enter(Type serviceType, Action<Exception>? onCycle)
	{
		var stack = _resolutionStack;
		stack ??= [];
		_resolutionStack = stack;

		if (stack.Contains(serviceType))
		{
			var exception = new InvalidOperationException(
				$"A circular dependency was detected during CUO service resolution: {BuildChain(stack, serviceType)}");
			try
			{
				onCycle?.Invoke(exception);
			}
			catch
			{
				// Diagnostics must never replace the cycle failure itself.
			}

			throw exception;
		}

		stack.Add(serviceType);
		return new ResolutionScope(stack);
	}

	private static string BuildChain(IReadOnlyList<Type> stack, Type current)
	{
		var builder = new StringBuilder();
		for (var i = 0; i < stack.Count; i++)
		{
			if (i > 0)
			{
				builder.Append(" -> ");
			}

			builder.Append(stack[i].FullName ?? stack[i].Name);
		}

		builder.Append(" -> ");
		builder.Append(current.FullName ?? current.Name);
		return builder.ToString();
	}

	private sealed class GuardedProvider(
		IServiceProvider inner,
		HashSet<Type> factoryServiceTypes,
		Action<Exception>? onCycle) : IServiceProvider
	{
		private readonly IServiceProvider _inner = inner;
		private readonly HashSet<Type> _factoryServiceTypes = factoryServiceTypes;

		public object? GetService(Type serviceType)
		{
			// Factory services push themselves in their wrapper. Non-factory
			// services have no wrapper, so record the request here to produce a
			// complete diagnostic chain when a cycle re-enters a factory.
			if (_factoryServiceTypes.Contains(serviceType))
			{
				return _inner.GetService(serviceType);
			}

			using (Enter(serviceType, onCycle))
			{
				return _inner.GetService(serviceType);
			}
		}
	}

	private sealed class ResolutionScope(List<Type> stack) : IDisposable
	{
		private List<Type>? _stack = stack;

		public void Dispose()
		{
			if (_stack is not { } activeStack)
			{
				return;
			}

			activeStack.RemoveAt(activeStack.Count - 1);
			_stack = null;

			if (activeStack.Count == 0)
			{
				_resolutionStack = null;
			}
		}
	}
}
