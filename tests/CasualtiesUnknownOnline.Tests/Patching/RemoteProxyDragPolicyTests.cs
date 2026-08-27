using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Regression tests for the remote-backpack drag escape bug: a water bottle
/// shown in another player's inventory could be dragged, the remote backpack
/// view closed while the drag was still held, and the proxy then dropped into
/// the local backpack — ending up in both inventories. The release rule must
/// cancel every remote-clone display proxy that was not consumed by the
/// dedicated remote-take path, regardless of whether the remote view is still
/// open.
/// </summary>
public class RemoteProxyDragPolicyTests
{
	private static readonly Type Policy = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.RemoteProxyDragPolicy",
		throwOnError: true)!;

	private static bool ShouldCancel(bool isRemoteProxy, bool remoteTakeHandled)
	{
		var method = Policy.GetMethod("ShouldCancelProxyRelease",
			BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("RemoteProxyDragPolicy.ShouldCancelProxyRelease not found.");
		return (bool)method.Invoke(null, [isRemoteProxy, remoteTakeHandled])!;
	}

	[Fact]
	public void ProxyNotConsumedByTake_MustCancelEvenAfterRemoteViewClosed()
	{
		Assert.True(ShouldCancel(isRemoteProxy: true, remoteTakeHandled: false),
			"a display proxy released outside the remote-take path must be cancelled, not handed to the native/cross-player release.");
	}

	[Fact]
	public void ProxyConsumedByTake_DoesNotNeedAdditionalCancel()
	{
		Assert.False(ShouldCancel(isRemoteProxy: true, remoteTakeHandled: true),
			"the dedicated remote-backpack take path already consumed the proxy.");
	}

	[Fact]
	public void LocalItem_IsNeverCancelledByTheProxyRule()
	{
		Assert.False(ShouldCancel(isRemoteProxy: false, remoteTakeHandled: false),
			"local authoritative items must continue to use the original release flow.");
	}

	[Fact]
	public void LocalItemWithTakeHandled_IsNotCancelledByTheProxyRule()
	{
		Assert.False(ShouldCancel(isRemoteProxy: false, remoteTakeHandled: true),
			"the proxy rule never applies to non-proxy items.");
	}
}
