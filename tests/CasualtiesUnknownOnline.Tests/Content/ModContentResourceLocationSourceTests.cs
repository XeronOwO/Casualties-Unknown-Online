using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Content;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Content;

/// <summary>
/// The mod-content half of the resource vocabulary: only registrations whose
/// owning mod declared a namespace are addressable, the canonical id is
/// <c>namespace:registration-id</c>, and the display name comes from the typed
/// DTO when the kind has one (otherwise the registered id). A bare id that
/// several mods registered for the same kind is not advertised at all.
/// </summary>
public class ModContentResourceLocationSourceTests
{
	private static ModContentResourceLocationSource CreateSource(params ModContentRegistration[] entries) =>
		new(new FakeContentControl(entries), NullLogger<ModContentResourceLocationSource>.Instance);

	[Fact]
	public void Entries_SkipBareIdRegisteredBySeveralMods()
	{
		var source = CreateSource(
			new ModContentRegistration("mod.a", new ModContentDefinition("sword", ModContentKind.Item, [1]), "moda"),
			new ModContentRegistration("mod.b", new ModContentDefinition("sword", ModContentKind.Item, [2]), "modb"),
			new ModContentRegistration("mod.c", new ModContentDefinition("axe", ModContentKind.Item, [3]), "modc"));

		var entry = Assert.Single(source.Entries);

		Assert.Equal("modc:axe", entry.Id.ToString());
	}

	[Fact]
	public void Entries_UseCanonicalIdAndTypedDisplayName()
	{
		var itemPayload = new ModItemDefinition { DisplayName = "Wooden Sword" }.ToPayload();
		var source = CreateSource(
			new ModContentRegistration("mod.a", new ModContentDefinition("sword", ModContentKind.Item, itemPayload), "mymod"));

		var entry = Assert.Single(source.Entries);

		Assert.Equal("mymod:sword", entry.Id.ToString());
		Assert.Equal(ModContentKind.Item, entry.Kind);
		Assert.Equal("Wooden Sword", entry.DisplayName);
	}

	[Fact]
	public void Entries_FallBackToRegisteredIdWhenKindHasNoTypedDisplayName()
	{
		var source = CreateSource(
			new ModContentRegistration("mod.a", new ModContentDefinition("healing.recipe", ModContentKind.Recipe, [1, 2]), "mymod"));

		var entry = Assert.Single(source.Entries);

		Assert.Equal("mymod:healing.recipe", entry.Id.ToString());
		Assert.Equal("healing.recipe", entry.DisplayName);
	}

	[Fact]
	public void Entries_SkipLegacyRegistrationsWithoutNamespace()
	{
		var source = CreateSource(
			new ModContentRegistration("mod.a", new ModContentDefinition("sword", ModContentKind.Item, [1])),
			new ModContentRegistration("mod.b", new ModContentDefinition("axe", ModContentKind.Item, [2]), "mymod"));

		var entry = Assert.Single(source.Entries);

		Assert.Equal("mymod:axe", entry.Id.ToString());
	}

	[Fact]
	public void Entries_SkipRegistrationWhoseIdCannotFormACanonicalPath()
	{
		// Registration validation refuses this today; the source stays defensive
		// so a future registry change cannot emit a non-addressable entry.
		var source = CreateSource(
			new ModContentRegistration("mod.a", new ModContentDefinition("Bad Id", ModContentKind.Item, [1]), "mymod"));

		Assert.Empty(source.Entries);
	}

	[Fact]
	public void Entries_AreStableAcrossCalls()
	{
		var source = CreateSource(
			new ModContentRegistration("mod.a", new ModContentDefinition("sword", ModContentKind.Item, [1]), "mymod"));

		Assert.Equal(
			source.Entries.Select(e => e.Id.ToString()),
			source.Entries.Select(e => e.Id.ToString()));
	}

	private sealed class FakeContentControl(IReadOnlyList<ModContentRegistration> entries) : IModContentControl
	{
		public IReadOnlyList<ModContentRegistration> Entries => entries;
	}
}
