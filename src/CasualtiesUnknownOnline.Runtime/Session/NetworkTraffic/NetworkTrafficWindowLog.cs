using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;

/// <summary>Formats one <see cref="NetworkTrafficWindow"/> for the periodic log.</summary>
internal static class NetworkTrafficWindowLog
{
	internal static string Format(NetworkTrafficWindow window)
	{
		var builder = new StringBuilder();
		builder.Append("send ").Append(window.SendCount).Append("f/").Append(window.SendBytes).Append("B");
		if (window.FailedSendCount > 0)
		{
			builder.Append(" fail ").Append(window.FailedSendCount).Append("f/").Append(window.FailedSendBytes).Append("B");
		}

		builder.Append("; recv ").Append(window.ReceiveCount).Append("f/").Append(window.ReceiveBytes).Append("B");

		var peers = window.ByPeer.Values
			.OrderByDescending(p => p.SendBytes + p.ReceiveBytes)
			.ThenBy(p => p.SteamId)
			.ToList();
		if (peers.Count > 0)
		{
			builder.Append("; peers: ").Append(string.Join(", ", peers.Select(FormatPeer)));
		}

		var top = BuildTopMessages(window);
		if (top.Count > 0)
		{
			builder.Append("; top: ").Append(string.Join(", ", top.Select(x => $"{x.Direction} {x.Msg}={x.Bytes}B")));
		}

		return builder.ToString();
	}

	private static string FormatPeer(NetworkTrafficWindow.PeerTraffic peer)
	{
		var fail = peer.FailedSendCount > 0 ? $" fail{peer.FailedSendCount}f/{peer.FailedSendBytes}B" : "";
		return $"{peer.SteamId} send={peer.SendBytes}B/{peer.SendCount}f recv={peer.ReceiveBytes}B/{peer.ReceiveCount}f{fail}";
	}

	private static List<(NetworkTrafficDirection Direction, NetMsg Msg, long Bytes)> BuildTopMessages(NetworkTrafficWindow window)
	{
		var result = new List<(NetworkTrafficDirection Direction, NetMsg Msg, long Bytes)>();
		foreach (var kv in window.SendByMessage)
		{
			result.Add((NetworkTrafficDirection.Send, kv.Key, kv.Value.Bytes));
		}

		foreach (var kv in window.ReceiveByMessage)
		{
			result.Add((NetworkTrafficDirection.Receive, kv.Key, kv.Value.Bytes));
		}

		return
		[
			.. result
				.OrderByDescending(x => x.Bytes)
				.ThenBy(x => x.Msg)
				.Take(10),
		];
	}
}
