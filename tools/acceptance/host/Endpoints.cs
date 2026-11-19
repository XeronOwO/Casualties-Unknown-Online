using System.Net.Sockets;

namespace CasualtiesUnknownOnline.AcceptanceHost;

/// <summary>
/// Driver-endpoint readiness: a plain TCP connect to the ws:// URL's port. Port-only on purpose — the
/// plug-in's own answer is the suite's first handshake, so pre-screening it would cost one process per
/// client per poll.
/// </summary>
internal static class Endpoints
{
	public static bool IsOpen(string url, int timeoutMs = 400)
	{
		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
		{
			return false;
		}

		using var client = new TcpClient();
		try
		{
			return client.ConnectAsync(uri.Host, uri.Port).Wait(timeoutMs) && client.Connected;
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>One loop for every client: the slowest box decides the launch, never the sum of all of them.</summary>
	public static bool WaitAll(IReadOnlyList<(string Name, string Url)> clients, int seconds, Action<string> log)
	{
		var deadline = DateTime.UtcNow.AddSeconds(seconds);
		var pending = new List<string>();
		foreach (var (name, _) in clients)
		{
			pending.Add(name);
		}

		while (pending.Count > 0 && DateTime.UtcNow < deadline)
		{
			foreach (var name in pending.ToArray())
			{
				if (IsOpen(UrlOf(clients, name)))
				{
					log($"{name} endpoint up");
					pending.Remove(name);
				}
			}

			if (pending.Count > 0)
			{
				Thread.Sleep(250);
			}
		}

		return pending.Count == 0;
	}

	private static string UrlOf(IReadOnlyList<(string Name, string Url)> clients, string name)
	{
		foreach (var (candidate, url) in clients)
		{
			if (string.Equals(candidate, name, StringComparison.Ordinal))
			{
				return url;
			}
		}

		return string.Empty;
	}
}
