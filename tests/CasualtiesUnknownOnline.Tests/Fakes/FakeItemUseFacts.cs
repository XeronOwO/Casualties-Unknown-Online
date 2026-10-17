using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Application.Kernel;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The game's item data as a test's own table: a definition this table says nothing
/// about answers <see cref="ItemUsePermission.Unknown"/>, which is the production
/// port's answer for an id the host's registry does not carry (and the answer a
/// build without a Game Adapter always gives). A case that wants a verdict seeds
/// the definition it is about, so the three values stay distinguishable instead of
/// a default deciding what a test means.
/// </summary>
internal sealed class FakeItemUseFacts : IKernelItemUseFacts
{
	private readonly Dictionary<string, ItemUsePermission> _permissions = new(StringComparer.Ordinal);

	/// <summary>The item's own data says <c>usable</c> is clear: a native use of it cannot have happened.</summary>
	internal FakeItemUseFacts Forbid(string definitionId) => Set(definitionId, ItemUsePermission.Forbidden);

	/// <summary>The item's own data says <c>usable</c> is set: the native use is the ordinary report.</summary>
	internal FakeItemUseFacts Permit(string definitionId) => Set(definitionId, ItemUsePermission.Permitted);

	/// <summary>This host holds no fact about that definition — a mod item it does not have.</summary>
	internal FakeItemUseFacts Unknown(string definitionId) => Set(definitionId, ItemUsePermission.Unknown);

	public ItemUsePermission NativeUse(string definitionId) =>
		_permissions.TryGetValue(definitionId, out var permission) ? permission : ItemUsePermission.Unknown;

	private FakeItemUseFacts Set(string definitionId, ItemUsePermission permission)
	{
		_permissions[definitionId] = permission;
		return this;
	}
}
