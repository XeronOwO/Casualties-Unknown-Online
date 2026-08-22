using System;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The L0 test face for the clone-inventory display path's component-state
/// decision: RemoteItemPresentation.IsGrapplingHookFired reads the wire's
/// component digest without touching Unity/game objects, so the "does a remote
/// clone show the fired sprite?" rule is unit-tested rather than only observed
/// at runtime. The adapter is compile-excluded from the test project, so the
/// helper is exercised reflectively (the same host as the other contract
/// tests).
/// </summary>
public class RemoteItemPresentationTests
{
	private static readonly Type Presentation = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.RemoteItemPresentation",
		throwOnError: true)!;

	private static bool IsGrapplingHookFired(CharacterItemMsg data)
	{
		var method = Presentation.GetMethod("IsGrapplingHookFired",
			BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("RemoteItemPresentation.IsGrapplingHookFired not found.");
		return (bool)method.Invoke(null, [data])!;
	}

	[Fact]
	public void FiredFlag_PresentAndTrue_ReturnsTrue()
	{
		var data = new CharacterItemMsg
		{
			Components =
			[
				new ComponentStateMsg
				{
					TypeName = "GrapplingHook",
					Fields =
					[
						new ComponentFieldMsg { Name = "fired", Kind = 3, BoolValue = true },
						new ComponentFieldMsg { Name = "hookLatched", Kind = 3, BoolValue = false },
						new ComponentFieldMsg { Name = "pulling", Kind = 3, BoolValue = false },
					],
				},
			],
		};

		Assert.True(IsGrapplingHookFired(data),
			"a GrapplingHook component state with fired=true must select the fired sprite.");
	}

	[Fact]
	public void FiredFlag_PresentButFalse_ReturnsFalse()
	{
		var data = new CharacterItemMsg
		{
			Components =
			[
				new ComponentStateMsg
				{
					TypeName = "GrapplingHook",
					Fields =
					[
						new ComponentFieldMsg { Name = "fired", Kind = 3, BoolValue = false },
					],
				},
			],
		};

		Assert.False(IsGrapplingHookFired(data),
			"fired=false must keep the normal sprite.");
	}

	[Fact]
	public void FiredFlag_MissingComponent_ReturnsFalse()
	{
		var data = new CharacterItemMsg
		{
			Components =
			[
				new ComponentStateMsg
				{
					TypeName = "CustomItemBehaviour",
					Fields =
					[
						new ComponentFieldMsg { Name = "state", Kind = 2, IntValue = 1 },
					],
				},
			],
		};

		Assert.False(IsGrapplingHookFired(data),
			"a non-GrapplingHook component must not select the fired sprite.");
	}
}
