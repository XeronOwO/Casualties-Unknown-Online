using System;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Handlers;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Locks the handler-context narrowing contract: every packet handler derives
/// from <see cref="PacketHandlerBase{TPacket, TContext}"/> with a narrow
/// capability interface as its second generic argument, and its
/// <c>Handle</c> method receives that exact interface — never the broad
/// <see cref="HandlerContext"/> composition root. This is the regression gate
/// for the backlog item "HandlerContext god-object: narrow per-domain handler
/// dependencies".
/// </summary>
public class HandlerContextNarrowingTests
{
	[Fact]
	public void EveryPacketHandlerUsesANarrowHandlerContext()
	{
		var handlerTypes = typeof(NetMessageRegistry).Assembly.GetTypes()
			.Where(t => !t.IsAbstract && !t.IsInterface && typeof(IPacketHandler).IsAssignableFrom(t))
			.ToArray();

		Assert.NotEmpty(handlerTypes);

		foreach (var handlerType in handlerTypes)
		{
			var genericBase = FindPacketHandlerBase(handlerType);
			Assert.True(genericBase is not null,
				$"{handlerType.Name} must derive from PacketHandlerBase<TPacket, TContext>.");

			var contextType = genericBase!.GetGenericArguments()[1];
			Assert.True(contextType.IsInterface,
				$"{handlerType.Name} handler context must be a capability interface, not {contextType.Name}.");
			Assert.True(contextType != typeof(HandlerContext),
				$"{handlerType.Name} must not expose the broad HandlerContext to business code.");
			Assert.True(contextType.IsAssignableFrom(typeof(HandlerContext)),
				$"HandlerContext must implement {contextType.Name} so the dispatcher can satisfy {handlerType.Name}.");

			var handle = handlerType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
				.FirstOrDefault(m => m.Name == "Handle" && m.GetParameters().Length == 3);
			Assert.True(handle is not null,
				$"{handlerType.Name} must override Handle(ulong, TMsg, TContext).");
			Assert.Equal(contextType, handle!.GetParameters()[2].ParameterType);
		}
	}

	private static Type? FindPacketHandlerBase(Type handlerType)
	{
		for (var current = handlerType.BaseType; current is not null; current = current.BaseType)
		{
			if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(PacketHandlerBase<,>))
			{
				return current;
			}
		}

		return null;
	}
}
