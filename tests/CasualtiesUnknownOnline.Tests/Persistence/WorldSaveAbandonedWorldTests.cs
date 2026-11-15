using System.IO;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

public sealed class WorldSaveAbandonedWorldTests
{
	[Fact]
	public void SessionEndBeforeTheFirstCut_RemovesTheEmptyWorldFolder()
	{
		using var fixture = WorldSaveFixture.Create("abandon-empty");
		Assert.True(fixture.Service.TryBeginRun(false));
		var worldId = fixture.Service.CurrentWorldId;
		Assert.False(string.IsNullOrEmpty(worldId));
		var folder = Path.Combine(fixture.Repository.Workspace.Root, worldId);
		Assert.True(Directory.Exists(folder));

		fixture.Session.FireSessionEnded();

		Assert.Equal(string.Empty, fixture.Service.CurrentWorldId);
		Assert.False(Directory.Exists(folder));
	}
}
