using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.DependencyInjection;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The live scene a test composition hands the Runtime through
/// <see cref="ILiveTrapLayoutSource"/>: <see cref="LiveScene"/> is the table the
/// fake "scans", so a test can make the host's last-derived table disagree with
/// the scene (the phantom-entity scenario) and assert WHICH one a send carried.
/// </summary>
internal sealed class FakeLiveLayoutSource(IWorldControl world) : ILiveTrapLayoutSource
{
	/// <summary>The live scene's entries — what a refresh replaces the host's own table with. An empty list means "the scan saw nothing".</summary>
	internal List<TrapLayoutEntryMsg> LiveScene { get; } = [];

	/// <summary>How many times the Runtime asked for a re-derive.</summary>
	internal int Refreshes { get; private set; }

	/// <summary>Register this fake as the composition's live-scene port (every node of an <c>ItemSimWorld</c>).</summary>
	internal static void Register(IServiceCollection services)
	{
		services.AddSingleton<FakeLiveLayoutSource>();
		services.AddSingleton<ILiveTrapLayoutSource>(p => p.GetRequiredService<FakeLiveLayoutSource>());
	}

	public void RefreshFromLiveScene()
	{
		Refreshes++;
		world.ReplaceTrapLayout([.. LiveScene]); // host-guarded in the registry: a guest's fake is a no-op
	}
}
