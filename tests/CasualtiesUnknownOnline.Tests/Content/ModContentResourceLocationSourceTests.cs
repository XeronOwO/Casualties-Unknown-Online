using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Content;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
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
			new ModContentRegistration("mod.a", new StubContentDefinition("sword", ModContentKind.Item), "moda"),
			new ModContentRegistration("mod.b", new StubContentDefinition("sword", ModContentKind.Item), "modb"),
			new ModContentRegistration("mod.c", new StubContentDefinition("axe", ModContentKind.Item), "modc"));

		var entry = Assert.Single(source.Entries);

		Assert.Equal("modc:axe", entry.Id.ToString());
	}

	[Fact]
	public void Entries_UseCanonicalIdAndTypedDisplayName()
	{
		var item = new ModItemDefinition { Id = "sword", DisplayName = "Wooden Sword" };
		var source = CreateSource(
			new ModContentRegistration("mod.a", item, "mymod"));

		var entry = Assert.Single(source.Entries);

		Assert.Equal("mymod:sword", entry.Id.ToString());
		Assert.Equal(ModContentKind.Item, entry.Kind);
		Assert.Equal("Wooden Sword", entry.DisplayName);
	}

	[Fact]
	public void Entries_FallBackToRegisteredIdWhenKindHasNoTypedDisplayName()
	{
		var source = CreateSource(
			new ModContentRegistration("mod.a", new StubContentDefinition("healing.recipe", ModContentKind.Recipe), "mymod"));

		var entry = Assert.Single(source.Entries);

		Assert.Equal("mymod:healing.recipe", entry.Id.ToString());
		Assert.Equal("healing.recipe", entry.DisplayName);
	}

	[Fact]
	public void Entries_SkipLegacyRegistrationsWithoutNamespace()
	{
		var source = CreateSource(
			new ModContentRegistration("mod.a", new StubContentDefinition("sword", ModContentKind.Item)),
			new ModContentRegistration("mod.b", new StubContentDefinition("axe", ModContentKind.Item), "mymod"));

		var entry = Assert.Single(source.Entries);

		Assert.Equal("mymod:axe", entry.Id.ToString());
	}

	[Fact]
	public void Entries_SkipRegistrationWhoseIdCannotFormACanonicalPath()
	{
		// Registration validation refuses this today; the source stays defensive
		// so a future registry change cannot emit a non-addressable entry.
		var source = CreateSource(
			new ModContentRegistration("mod.a", new StubContentDefinition("Bad Id", ModContentKind.Item), "mymod"));

		Assert.Empty(source.Entries);
	}

	[Fact]
	public void Entries_AreStableAcrossCalls()
	{
		var source = CreateSource(
			new ModContentRegistration("mod.a", new StubContentDefinition("sword", ModContentKind.Item), "mymod"));

		Assert.Equal(
			source.Entries.Select(e => e.Id.ToString()),
			source.Entries.Select(e => e.Id.ToString()));
	}

	private sealed class FakeContentControl(IReadOnlyList<ModContentRegistration> entries) : IModContentControl
	{
		public IReadOnlyList<ModContentRegistration> Entries => entries;
	}
}
