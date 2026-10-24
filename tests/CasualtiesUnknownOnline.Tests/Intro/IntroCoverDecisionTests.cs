using System;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Intro;

/// <summary>
/// The intro cover's decision (ticket `launch-intro-cover-policy.md`, user's 2026-10-24 ruling): the local
/// preference governs an ordinary launch (off by default), and a launch through Steam's join action shows the
/// cover even with the preference on. The preference is read live, so a config edit needs no second flag.
/// </summary>
[Trait("Category", "Unit")]
public class IntroCoverDecisionTests
{
	[Fact]
	public void OrdinaryLaunch_WithThePreferenceOff_ShowsTheCover()
	{
		var decision = new IntroCoverDecision();
		decision.UsePreference(() => false);

		Assert.False(decision.SkipsTheCover);
		Assert.Contains("preference", decision.Describe());
		Assert.Contains("off", decision.Describe());
	}

	[Fact]
	public void OrdinaryLaunch_WithThePreferenceOn_SkipsTheCover()
	{
		var decision = new IntroCoverDecision();
		decision.UsePreference(() => true);

		Assert.True(decision.SkipsTheCover);
		Assert.Contains("preference is on", decision.Describe());
	}

	[Fact]
	public void JoinLaunch_ShowsTheCoverEvenWithThePreferenceOn()
	{
		// The user's clause: the join path keeps its own behaviour instead of inheriting the preference.
		var decision = new IntroCoverDecision();
		decision.UsePreference(() => true);
		decision.NoteDirectJoinLaunch();

		Assert.True(decision.IsDirectJoinLaunch);
		Assert.False(decision.SkipsTheCover);
		Assert.Contains("join action", decision.Describe());
	}

	[Fact]
	public void ThePreferenceIsReadLive()
	{
		// No copy of the value is taken: flipping the configuration applies to the next launch.
		var skip = false;
		var decision = new IntroCoverDecision();
		decision.UsePreference(() => skip);

		Assert.False(decision.SkipsTheCover);
		skip = true;
		Assert.True(decision.SkipsTheCover);
	}

	[Fact]
	public void UnwiredPreference_ShowsTheCover()
	{
		// A shell that never wired the preference answers "show": the default is the content warning staying on.
		var decision = new IntroCoverDecision();

		Assert.False(decision.SkipsTheCover);
		Assert.Throws<ArgumentNullException>(() => decision.UsePreference(null!));
	}
}
