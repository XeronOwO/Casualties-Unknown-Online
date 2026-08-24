using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The hostile trader swing presentation bridge contract: the coordinator that
/// reports the local swing and the replay helper that reproduces it on a
/// peer's same-position trader must keep their surfaces stable. The adapter is
/// loaded reflectively because it references Unity/game types.
/// </summary>
public class TraderSwingPresentationTests
{
	private const string SyncTypeName =
		"CasualtiesUnknownOnline.GameAdapter.World.TraderSwingSync";
	private const string ReplayTypeName =
		"CasualtiesUnknownOnline.GameAdapter.World.TraderSwingReplay";

	[Fact]
	public void TraderSwingSync_HasReportSurface()
	{
		var type = GameAssemblyHost.Adapter.GetType(SyncTypeName, throwOnError: false)
			?? throw new InvalidOperationException("TraderSwingSync type not found in the adapter assembly.");

		var report = type.GetMethod("Report", BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("TraderSwingSync.Report not found.");
		var parameters = report.GetParameters();
		Assert.Single(parameters);
		Assert.Equal("TraderScript", parameters[0].ParameterType.FullName);
		Assert.Equal(typeof(void), report.ReturnType);

		Assert.NotNull(type.GetMethod("BindToSession", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
		Assert.NotNull(type.GetMethod("Unbind", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
	}

	[Fact]
	public void TraderSwingReplay_HasPlaySurface()
	{
		var type = GameAssemblyHost.Adapter.GetType(ReplayTypeName, throwOnError: false)
			?? throw new InvalidOperationException("TraderSwingReplay type not found in the adapter assembly.");

		var play = type.GetMethod("Play", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("TraderSwingReplay.Play not found.");
		var parameters = play.GetParameters();
		Assert.Equal(2, parameters.Length);
		Assert.Equal("TraderScript", parameters[0].ParameterType.FullName);
		Assert.Equal("CasualtiesUnknownOnline.Runtime.Protocol.Messages.TraderSwingMsg", parameters[1].ParameterType.FullName);
		Assert.Equal(typeof(void), play.ReturnType);
	}
}
