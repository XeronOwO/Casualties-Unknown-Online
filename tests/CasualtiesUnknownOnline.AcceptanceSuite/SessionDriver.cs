using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite;

/// <summary>
/// The session the cases run inside. The cases judge the real game, so the suite owns the
/// session it judges: it asks the acceptance driver for the world state and starts a run when
/// no world is up, instead of judging whatever happened to be running.
/// </summary>
internal sealed class SessionDriver
{
    private static readonly TimeSpan StartBudget = TimeSpan.FromSeconds(60);

    private readonly Options _options;

    public SessionDriver(Options options)
    {
        _options = options;
    }

    /// <summary>Starts a run when no world is up and returns the note for the report.</summary>
    public async Task<string> EnsureInWorldAsync(CancellationToken cancellationToken)
    {
        var state = await ActionAsync("state", cancellationToken).ConfigureAwait(false);
        if (IsTrue(state, "inWorld"))
        {
            return "the host was already in a world";
        }

        await ActionAsync("start-run", cancellationToken).ConfigureAwait(false);
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < StartBudget)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken).ConfigureAwait(false);
            state = await ActionAsync("state", cancellationToken).ConfigureAwait(false);
            if (IsTrue(state, "inWorld"))
            {
                return "the suite started a fresh run";
            }
        }

        throw new InvalidOperationException("the suite could not start a world for the cases");
    }

    /// <summary>Runs one driver action and returns its JSON answer.</summary>
    public async Task<JsonElement> ActionAsync(string action, CancellationToken cancellationToken)
    {
        var script = Path.Combine(RepositoryPaths.Root, "tools", "acceptance", "drive-in-process.ps1");
        var start = new ProcessStartInfo("pwsh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(script);
        start.ArgumentList.Add("-Url");
        start.ArgumentList.Add(_options.HostUrl);
        start.ArgumentList.Add("-Action");
        start.ArgumentList.Add(action);

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"could not start the acceptance driver for '{action}'");
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"the acceptance driver answered '{action}' with exit {process.ExitCode}: {error.Trim()}");
        }

        var startIndex = output.IndexOf('{', StringComparison.Ordinal);
        if (startIndex < 0)
        {
            throw new InvalidOperationException($"the acceptance driver answered '{action}' without a JSON body: {output.Trim()}");
        }

        using var document = JsonDocument.Parse(output[startIndex..]);
        return document.RootElement.Clone();
    }

    private static bool IsTrue(JsonElement answer, string property)
    {
        if (answer.ValueKind == JsonValueKind.Object)
        {
            if (answer.TryGetProperty(property, out var value))
            {
                return value.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.String => string.Equals(value.GetString(), "true", StringComparison.OrdinalIgnoreCase),
                    _ => false,
                };
            }

            foreach (var child in answer.EnumerateObject())
            {
                if (IsTrue(child.Value, property))
                {
                    return true;
                }
            }
        }
        else if (answer.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in answer.EnumerateArray())
            {
                if (IsTrue(child, property))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
