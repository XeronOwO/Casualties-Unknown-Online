namespace CasualtiesUnknownOnline.AcceptanceHost;

/// <summary>
/// The acceptance environment facts: the gitignored local file every tool agrees on. One section is
/// read (the one whose heading names the acceptance environment) and each bullet becomes a key; the
/// same file the PowerShell tools read, so a machine value lives in exactly one place.
/// </summary>
internal sealed class AcceptanceFacts
{
	private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

	public bool IsEmpty => _values.Count == 0;

	public static AcceptanceFacts Load(string path)
	{
		var facts = new AcceptanceFacts();
		if (!File.Exists(path))
		{
			return facts;
		}
		var inSection = false;
		foreach (var raw in File.ReadLines(path))
		{
			var line = raw.Trim();
			if (line.StartsWith("##", StringComparison.Ordinal))
			{
				var heading = line.TrimStart('#').Trim();
				inSection = heading.Contains("acceptance environment", StringComparison.OrdinalIgnoreCase);
				continue;
			}

			if (!inSection)
			{
				continue;
			}

			var bullet = line.StartsWith("- ", StringComparison.Ordinal) ? line[2..].Trim() : line;
			var colon = bullet.IndexOf(':');
			if (colon <= 0)
			{
				continue;
			}

			var key = bullet[..colon].Trim();
			var value = bullet[(colon + 1)..].Trim();
			if (key.Length > 0)
			{
				facts._values[key] = value;
			}
		}

		return facts;
	}

	/// <summary>A passed value wins; otherwise the facts file; otherwise a named error, never a guess.</summary>
	public string Require(string passed, string key, string option)
	{
		if (!string.IsNullOrWhiteSpace(passed))
		{
			return passed;
		}

		if (_values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
		{
			return value;
		}

		throw new CommandFailure(64, $"{option} was not passed and the local facts file has no '{key}'.");
	}

	public string? Optional(string passed, string key)
	{
		if (!string.IsNullOrWhiteSpace(passed))
		{
			return passed;
		}

		return _values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
	}
}
