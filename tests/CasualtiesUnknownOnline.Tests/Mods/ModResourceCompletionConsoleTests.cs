using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.Content;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The Stage-1 seam on the real path: a stage registered through a LOADED mod's
/// <see cref="IModContext.ResourceCompletion"/> — discovery → mod context →
/// per-mod adapter → store → catalog — reaches the console's argument-suggestion
/// endpoint (<see cref="ICommandArgumentSuggestions"/>, the surface
/// <c>CommandConsoleService</c> serves its resource-location arguments from).
/// <see cref="ModResourceCompletionTests"/> covers the adapter and the catalog's
/// ranking directly; this test exists because "a mod can widen the console's
/// completion" is a claim only the production composition can prove.
/// </summary>
[Trait("Category", "Integration")]
public class ModResourceCompletionConsoleTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void AStageRegisteredOnALoadedMod_CompletesTheConsoleResourceArgument()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId, extraRegistrations: services =>
			services.AddSingleton<IResourceLocationSource>(new StubResourceSource(
				new ResourceLocationEntry(ContentId.Parse("cu:widget"), ModContentKind.Item, "微吉特"))));
		var completion = ContextOf(host).ResourceCompletion;
		var suggestions = host.Services.GetRequiredService<ICommandArgumentSuggestions>();

		// The built-in ranks answer on their own: an id prefix completes with no
		// stage registered, while the display name does not complete at all.
		Assert.Equal(
			["cu:widget"],
			suggestions.Suggest(CommandArgumentKind.ResourceLocation, "cu:wid").Select(s => s.Text));
		Assert.Empty(suggestions.Suggest(CommandArgumentKind.ResourceLocation, "wjt"));

		Assert.True(completion.TryRegisterMatchStage(
			"initials",
			new StubMatchStage((entry, prefix) => entry.DisplayName == "微吉特" && prefix == "wjt")));
		Assert.Equal(
			"cu:widget",
			Assert.Single(suggestions.Suggest(CommandArgumentKind.ResourceLocation, "wjt")).Text);

		// Unregistering takes the stage back off the console's path.
		Assert.True(completion.TryUnregisterMatchStage("initials"));
		Assert.Empty(suggestions.Suggest(CommandArgumentKind.ResourceLocation, "wjt"));
	}

	private static IModContext ContextOf(TestNode node) =>
		((TestDataMod)node.Services.GetRequiredService<ModService>().LoadedMods.Single(m => m is TestDataMod)).Context!;
}
