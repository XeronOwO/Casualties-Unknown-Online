using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Data;

/// <summary>
/// A named compound. Keys are unique (the parser refuses a repeat), case-sensitive, and keep
/// their written order.
/// </summary>
public sealed record SnbtCompound(IReadOnlyList<SnbtEntry> Entries) : SnbtValue
{
	public override string Kind => "a compound";

	public static SnbtCompound Empty { get; } = new([]);

	/// <summary>The value under <paramref name="key"/>, or false when the compound does not carry it.</summary>
	public bool TryGet(string key, out SnbtValue value)
	{
		foreach (var entry in Entries)
		{
			if (string.Equals(entry.Key, key, StringComparison.Ordinal))
			{
				value = entry.Value;
				return true;
			}
		}

		value = Empty;
		return false;
	}

	/// <summary>This compound with <paramref name="key"/> set to <paramref name="value"/>, in place if it already existed.</summary>
	public SnbtCompound With(string key, SnbtValue value)
	{
		var entries = new List<SnbtEntry>(Entries.Count + 1);
		var replaced = false;
		foreach (var entry in Entries)
		{
			if (string.Equals(entry.Key, key, StringComparison.Ordinal))
			{
				entries.Add(entry with { Value = value });
				replaced = true;
			}
			else
			{
				entries.Add(entry);
			}
		}

		if (!replaced)
		{
			entries.Add(new SnbtEntry(key, value));
		}

		return new SnbtCompound(entries);
	}

	/// <summary>This compound without <paramref name="key"/>; the entry is silently absent when it was not there.</summary>
	public SnbtCompound Without(string key)
	{
		var entries = new List<SnbtEntry>(Entries.Count);
		foreach (var entry in Entries)
		{
			if (!string.Equals(entry.Key, key, StringComparison.Ordinal))
			{
				entries.Add(entry);
			}
		}

		return new SnbtCompound(entries);
	}

	/// <summary>Whether this compound carries <paramref name="key"/>.</summary>
	public bool Has(string key)
	{
		foreach (var entry in Entries)
		{
			if (string.Equals(entry.Key, key, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}
}
