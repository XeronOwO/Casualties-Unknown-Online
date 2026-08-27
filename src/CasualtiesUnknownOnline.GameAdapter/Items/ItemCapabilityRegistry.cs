using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The item capability registry: the complete inventory of item special
/// behaviors that are allowed to carry persistent state. The registry rejects
/// a capability that does not implement all five required surfaces because the
/// interface itself forces the shape; <see cref="AssertComplete"/> is the
/// runtime/test gate that fails when a duplicate, empty, or invalid entry is
/// registered.
/// </summary>
public sealed class ItemCapabilityRegistry(IEnumerable<IItemCapability> capabilities)
{
	private readonly IReadOnlyList<IItemCapability> _capabilities = [.. capabilities];

	/// <summary>The default Phase B registry (start with current features only).</summary>
	public static ItemCapabilityRegistry CreateDefault() =>
		new(
		[
			new SavedStateItemCapability(),
			new LiquidItemCapability(),
			new GunItemCapability(),
			new CustomDataItemCapability(),
		]);

	public IReadOnlyList<IItemCapability> Capabilities => _capabilities;

	public IReadOnlyList<string> Names => [.. _capabilities.Select(c => c.Name)];

	public IItemCapability? Find(string name) => _capabilities.FirstOrDefault(c => c.Name == name);

	public IEnumerable<IItemCapability> For(Item item) => _capabilities.Where(c => c.AppliesTo(item));

	/// <summary>
	/// Completeness contract: every registered capability has a non-empty unique
	/// name, and the five required surfaces are all present by interface
	/// contract. Throws when the registry is misconfigured.
	/// </summary>
	public void AssertComplete()
	{
		if (_capabilities.Count == 0)
		{
			throw new InvalidOperationException("Item capability registry is empty; at least one capability must be registered.");
		}

		var seen = new HashSet<string>();
		foreach (var capability in _capabilities)
		{
			if (string.IsNullOrWhiteSpace(capability.Name))
			{
				throw new InvalidOperationException("Item capability has an empty name.");
			}

			if (!seen.Add(capability.Name))
			{
				throw new InvalidOperationException($"Duplicate item capability '{capability.Name}'.");
			}

			// The interface itself is the five-surface contract; a concrete
			// capability cannot implement a partial version without a compiler
			// error. The uniqueness/name checks above are the only runtime gate
			// needed for now.
		}
	}
}
