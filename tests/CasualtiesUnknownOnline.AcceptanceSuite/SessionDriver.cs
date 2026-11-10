using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite;

/// <summary>
/// The session the cases run inside. The cases judge the real game, so the suite owns the
/// session it judges: it asks the acceptance driver for the world state of the host and of the
/// member, starts a run when no world is up, and joins the member to the host's IP session,
/// instead of judging whatever happened to be running.
/// </summary>
internal sealed class SessionDriver
{
    private static readonly TimeSpan StartBudget = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan JoinBudget = TimeSpan.FromSeconds(30);

    private readonly Options _options;

    public SessionDriver(Options options)
    {
        _options = options;
    }

    /// <summary>Starts a run when no world is up and returns the note for the report.</summary>
    public async Task<string> EnsureInWorldAsync(CancellationToken cancellationToken)
    {
        var state = await ActionAsync(_options.HostUrl, "state", cancellationToken).ConfigureAwait(false);
        if (IsTrue(state, "inWorld"))
        {
            return "the host was already in a world";
        }

        await ActionAsync(_options.HostUrl, "start-run", cancellationToken).ConfigureAwait(false);
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < StartBudget)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken).ConfigureAwait(false);
            state = await ActionAsync(_options.HostUrl, "state", cancellationToken).ConfigureAwait(false);
            if (IsTrue(state, "inWorld"))
            {
                return "the suite started a fresh run";
            }
        }

        throw new InvalidOperationException("the suite could not start a world for the cases");
    }

    /// <summary>Joins the member to the host's IP session when it is not in the world.</summary>
    public async Task<string> EnsureMemberAsync(CancellationToken cancellationToken)
    {
        var state = await ActionAsync(_options.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
        if (IsTrue(state, "inWorld"))
        {
            return "the member was already in a world";
        }

        await ActionAsync(_options.MemberUrl, "click", cancellationToken, ("-Control", "home.transport.ip")).ConfigureAwait(false);
        await ActionAsync(_options.MemberUrl, "set-text", cancellationToken, ("-Control", "ip.address"), ("-Text", "127.0.0.1")).ConfigureAwait(false);
        await ActionAsync(_options.MemberUrl, "set-text", cancellationToken, ("-Control", "ip.port"), ("-Text", "7777")).ConfigureAwait(false);
        await ActionAsync(_options.MemberUrl, "click", cancellationToken, ("-Control", "ip.join")).ConfigureAwait(false);

        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < JoinBudget)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken).ConfigureAwait(false);
            state = await ActionAsync(_options.MemberUrl, "state", cancellationToken).ConfigureAwait(false);
            if (IsTrue(state, "inWorld"))
            {
                return "the suite joined the member to the host's session";
            }
        }

        throw new InvalidOperationException("the suite could not join the member to the host's session");
    }

    /// <summary>Runs one driver action against one client and returns its JSON answer.</summary>
    public async Task<JsonElement> ActionAsync(string url, string action, CancellationToken cancellationToken, params (string Name, string Value)[] extraArguments)
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
        start.ArgumentList.Add(url);
        start.ArgumentList.Add("-Action");
        start.ArgumentList.Add(action);
        foreach (var (name, value) in extraArguments)
        {
            start.ArgumentList.Add(name);
            start.ArgumentList.Add(value);
        }

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
                if (child.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array && IsTrue(child.Value, property))
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
