using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace CasualtiesUnknownOnline.AcceptanceSuite;

/// <summary>
/// One wall-clock timeline for the whole batch: every group, setup, case and the steps the cases
/// already record as readings or notes. Durations alone cannot show a gap between two rows; the
/// absolute local start/end can. The text is intentionally verbose: the report is a diagnostic
/// artifact, not a dashboard.
/// </summary>
internal sealed class Timeline
{
	private readonly List<string> _lines = [];
	private readonly Stack<(string Label, Stopwatch Clock)> _scopes = new();

	public IReadOnlyList<string> Lines => _lines;

	public void Begin(string label)
	{
		_lines.Add($"{Indent()}{Now()} {label} start");
		_scopes.Push((label, Stopwatch.StartNew()));
	}

	public void End(string label, string? result = null)
	{
		if (_scopes.Count == 0)
		{
			Step($"{label} end (no open scope)");
			return;
		}

		var scope = _scopes.Pop();
		var suffix = string.IsNullOrEmpty(result) ? string.Empty : $" result {result}";
		_lines.Add($"{Indent()}{Now()} {scope.Label} end duration {scope.Clock.Elapsed.TotalSeconds:F1}s{suffix}");
	}

	public void Step(string message) => _lines.Add($"{Indent()}{Now()} {message}");

	public string Render()
	{
		var builder = new StringBuilder();
		builder.AppendLine("## Timeline");
		builder.AppendLine();
		builder.AppendLine("```text");
		foreach (var line in _lines)
		{
			builder.AppendLine(line);
		}

		builder.AppendLine("```");
		return builder.ToString();
	}

	private string Indent() => new(' ', _scopes.Count * 2);

	private static string Now() => DateTimeOffset.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
}
