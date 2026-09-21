using System;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.PinyinSearch.Core;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.Content;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using CasualtiesUnknownOnline.Tests.Mods;
using CasualtiesUnknownOnline.Tests.Patching;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.PinyinSearch;

/// <summary>
/// The mod's console half on the PRODUCTION path (ticket acceptance): the real
/// <c>[CuoMod]</c> entry point binds to a loaded mod's
/// <see cref="IModContext"/>, registers its stage through the framework's
/// completion seam, and the console's resource-location completion then answers
/// pinyin input with the canonical id. The stage-level rows live in
/// <see cref="PinyinSearchStageTests"/>; this test exists because "the mod
/// widens the console" is a claim only the production composition can prove.
/// </summary>
[Collection(GameAssemblyCollection.Name)]
[Trait("Category", "Integration")]
public class PinyinSearchModConsoleTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static readonly ResourceLocationEntry Fentanyl =
		new(ContentId.Parse("cu:fentanyl"), ModContentKind.Item, "芬太尼");

	[Theory]
	[InlineData("fent")]
	[InlineData("ftn")]
	[InlineData("芬太")]
	[InlineData("cu:fent")]
	public void Suggest_ReachesTheCanonicalId_FromPinyinInitialsChineseAndIdPrefixes(string query)
	{
		var host = Host(out var context);
		var suggestions = host.Services.GetRequiredService<ICommandArgumentSuggestions>();

		Assert.True(context.ResourceCompletion.IsMatchStageRegistered("pinyin"));

		var suggestion = Assert.Single(suggestions.Suggest(CommandArgumentKind.ResourceLocation, query));

		Assert.Equal("cu:fentanyl", suggestion.Text);
	}

	[Fact]
	public void Suggest_SwitchOff_KeepsTheNativeRanks()
	{
		var host = Host(out _, enabled: false);
		var suggestions = host.Services.GetRequiredService<ICommandArgumentSuggestions>();

		Assert.Empty(suggestions.Suggest(CommandArgumentKind.ResourceLocation, "ftn"));
		Assert.Equal(
			["cu:fentanyl"],
			suggestions.Suggest(CommandArgumentKind.ResourceLocation, "cu:fent").Select(s => s.Text));
	}

	/// <summary>
	/// A host whose composition carries the fentanyl entry, with the mod's real
	/// entry point bound to a loaded mod's context (the same context path
	/// <c>ModResourceCompletionConsoleTests</c> uses).
	/// </summary>
	private static TestNode Host(out IModContext context, bool enabled = true)
	{
		Switch(enabled);
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId, extraRegistrations: services =>
			services.AddSingleton<IResourceLocationSource>(new StubResourceSource(Fentanyl)));

		context = ContextOf(host);
		new PinyinSearchMod().Bind(context);
		return host;
	}

	/// <summary>Binds the mod's switch to a throwaway config file; the surfaces read it live.</summary>
	private static void Switch(bool enabled)
	{
		var path = Path.Combine(Path.GetTempPath(), "cuo-tests", $"pinyin-{Guid.NewGuid():N}.cfg");
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		PinyinSearchConfig.Bind(new ConfigFile(path, saveOnInit: true), enabled);
	}

	private static IModContext ContextOf(TestNode node) =>
		((TestDataMod)node.Services.GetRequiredService<ModService>().LoadedMods.Single(m => m is TestDataMod)).Context!;
}
