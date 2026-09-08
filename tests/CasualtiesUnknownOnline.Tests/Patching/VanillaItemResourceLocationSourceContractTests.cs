using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Content;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The Game Adapter vanilla resource-id source. The test project never
/// compile-references GameAdapter (it binds Unity/game assemblies), so this
/// locks the seam and the pure table → entry mapping reflectively: injected mod
/// items are excluded, invalid ids are skipped, and a missing display name
/// falls back to the bare id. The real <c>Item.GlobalItems</c> read is a thin
/// projection over this method.
/// </summary>
public class VanillaItemResourceLocationSourceContractTests
{
	private const string TypeName = "CasualtiesUnknownOnline.GameAdapter.Content.VanillaItemResourceLocationSource";

	[Fact]
	public void GameAdapter_ImplementsResourceLocationSourceSeam()
	{
		var type = GameAssemblyHost.Adapter.GetType(TypeName, throwOnError: true)!;

		Assert.True(typeof(IResourceLocationSource).IsAssignableFrom(type),
			"VanillaItemResourceLocationSource must implement the Runtime resource-location seam.");
	}

	[Fact]
	public void BuildEntries_MapsVanillaItemsToCanonicalIdsWithLocalizedName()
	{
		var entries = Build([("fentanyl", "芬太尼"), ("bandage", "绷带")]);

		Assert.Equal(["cu:fentanyl", "cu:bandage"], entries.Select(e => e.Id.ToString()));
		Assert.Equal("芬太尼", entries.Single(e => e.Id.ToString() == "cu:fentanyl").DisplayName);
		Assert.All(entries, e => Assert.Equal(ModContentKind.Item, e.Kind));
	}

	[Fact]
	public void BuildEntries_ExcludesInjectedModItems()
	{
		var entries = Build([("fentanyl", "芬太尼"), ("woodensword", "Wooden Sword")], ["woodensword"]);

		var entry = Assert.Single(entries);
		Assert.Equal("cu:fentanyl", entry.Id.ToString());
	}

	[Fact]
	public void BuildEntries_SkipsIdsThatCannotFormACanonicalPath()
	{
		var entries = Build([("Bad Id", "Broken"), ("bad/id", "Broken"), ("UPPER", "Normalised"), ("bandage", "绷带")]);

		Assert.Equal(["cu:upper", "cu:bandage"], entries.Select(e => e.Id.ToString()));
	}

	[Fact]
	public void BuildEntries_FallsBackToTheBareIdWhenTheTableHasNoDisplayName()
	{
		var entries = Build([("bandage", null), ("fentanyl", "   ")]);

		Assert.Equal("bandage", entries.Single(e => e.Id.ToString() == "cu:bandage").DisplayName);
		Assert.Equal("fentanyl", entries.Single(e => e.Id.ToString() == "cu:fentanyl").DisplayName);
	}

	[Fact]
	public void BuildEntries_EmptyTable_IsEmpty() => Assert.Empty(Build([]));

	private static IReadOnlyList<ResourceLocationEntry> Build(
		(string Id, string? DisplayName)[] items,
		string[]? injectedItemIds = null)
	{
		var type = GameAssemblyHost.Adapter.GetType(TypeName, throwOnError: true)!;
		var method = type.GetMethod("BuildEntries", BindingFlags.NonPublic | BindingFlags.Static);
		Assert.NotNull(method);

		var pairs = new List<KeyValuePair<string, string>>(items.Length);
		foreach (var item in items)
		{
			pairs.Add(new KeyValuePair<string, string>(item.Id, item.DisplayName!));
		}

		var result = method!.Invoke(null, [pairs, new List<string>(injectedItemIds ?? []), NullLogger.Instance]);
		return Assert.IsAssignableFrom<IReadOnlyList<ResourceLocationEntry>>(result);
	}
}
