using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The player world-blood presentation bridge contract: the coordinator that
/// reports a locally-spawned decal and the replay helper that reproduces it on
/// a peer's world must keep their surfaces stable. The adapter is loaded
/// reflectively because it references Unity/game types.
/// </summary>
public class WorldBloodPresentationTests
{
	private const string SyncTypeName =
		"CasualtiesUnknownOnline.GameAdapter.World.WorldBloodSync";
	private const string ReplayTypeName =
		"CasualtiesUnknownOnline.GameAdapter.World.WorldBloodReplay";

	[Fact]
	public void WorldBloodSync_HasReportSurface()
	{
		var type = GameAssemblyHost.Adapter.GetType(SyncTypeName, throwOnError: false)
			?? throw new InvalidOperationException("WorldBloodSync type not found in the adapter assembly.");

		var report = type.GetMethod("Report", BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("WorldBloodSync.Report not found.");
		var parameters = report.GetParameters();
		Assert.Equal(2, parameters.Length);
		Assert.Equal("UnityEngine.Vector2", parameters[0].ParameterType.FullName);
		Assert.Equal("System.Boolean", parameters[1].ParameterType.FullName);
		Assert.Equal(typeof(void), report.ReturnType);

		Assert.NotNull(type.GetMethod("BindToSession", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
		Assert.NotNull(type.GetMethod("Unbind", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
	}

	[Fact]
	public void BleedParticleWorldBloodPatch_HasPrefixAndPostfix()
	{
		var type = GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.Patches.BleedParticleWorldBloodPatch", throwOnError: false)
			?? throw new InvalidOperationException("BleedParticleWorldBloodPatch type not found in the adapter assembly.");

		Assert.NotNull(type.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
		Assert.NotNull(type.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
	}

	[Fact]
	public void WorldBloodReplay_HasPlaySurface()
	{
		var type = GameAssemblyHost.Adapter.GetType(ReplayTypeName, throwOnError: false)
			?? throw new InvalidOperationException("WorldBloodReplay type not found in the adapter assembly.");

		var play = type.GetMethod("Play", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("WorldBloodReplay.Play not found.");
		var parameters = play.GetParameters();
		Assert.Single(parameters);
		Assert.Equal("CasualtiesUnknownOnline.Runtime.Protocol.Messages.WorldBloodSpawnMsg", parameters[0].ParameterType.FullName);
		Assert.Equal(typeof(void), play.ReturnType);
	}
}
