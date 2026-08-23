using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Networking;

/// <summary>
/// A TCP-based non-Steam transport for LAN / port-forward / VPN hosting.
/// The host is always logical peer id <see cref="HostPeerId"/>; each guest
/// generates a random 64-bit logical id and sends it as a transport-level hello
/// immediately after connecting, so the transport can route by stable ids
/// without Steam persona/SteamID. Frame format: [int32 length][payload].
/// Reliable/unreliable are not distinguished by TCP — all frames are reliable;
/// this is a safe degradation for the first IP-direct slice.
/// </summary>
public sealed class IpDirectTransport(ILogger<IpDirectTransport> log) : INetworkTransport, ICuoService, IDisposable
{
	public const ulong HostPeerId = 1;

	private const int MaxFrameSize = 1024 * 1024;
	private const int ConnectTimeoutMs = 5000;
	private const int HelloTimeoutMs = 5000;

	private readonly object _sync = new();
	private readonly Dictionary<ulong, IpConnection> _connections = [];
	private readonly ConcurrentQueue<(ulong Sender, byte[] Data)> _incoming = new();
	private readonly ConcurrentQueue<ulong> _disconnected = new();

	private TcpListener? _listener;
	private Thread? _acceptThread;
	private volatile bool _running;
	private bool _isHost;
	private ulong _localPeerId;
	private int _boundPort;

	/// <summary>Raised on the Unity main thread via <see cref="Poll"/>.</summary>
	public event Action<ulong, byte[]>? MessageReceived;

	/// <summary>Raised on the Unity main thread via <see cref="Poll"/> when a peer's TCP connection closes.</summary>
	public event Action<ulong>? PeerDisconnected;

	public bool IsRunning => _running;

	public bool IsHost => _isHost;

	/// <summary>The local logical peer id. Host = 1; guest = the random id sent in the connect hello.</summary>
	public ulong LocalPeerId => _localPeerId;

	/// <summary>The actual bound port after <see cref="StartHost"/> (0 when not hosting or when port 0 was requested).</summary>
	public int BoundPort => _boundPort;

	/// <summary>Remote logical peer ids currently connected (guest list on the host; host id on the guest).</summary>
	public IReadOnlyCollection<ulong> ActiveRemotePeers
	{
		get
		{
			lock (_sync)
			{
				return [.. _connections.Keys];
			}
		}
	}

	public bool StartHost(int port, out string error)
	{
		lock (_sync)
		{
			if (_running)
			{
				error = "IP direct session is already running.";
				return false;
			}
		}

		TcpListener listener;
		try
		{
			listener = new TcpListener(IPAddress.Any, port);
			listener.Start();
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return false;
		}

		var boundPort = ((IPEndPoint)listener.LocalEndpoint).Port;
		lock (_sync)
		{
			_listener = listener;
			_isHost = true;
			_localPeerId = HostPeerId;
			_boundPort = boundPort;
			_running = true;
		}

		_acceptThread = new Thread(AcceptLoop)
		{
			IsBackground = true,
			Name = "CUO-IpAccept",
		};
		_acceptThread.Start();
		log.LogInformation("IP direct host started on port {Port} (peer id {Peer}).", port, HostPeerId);
		error = "";
		return true;
	}

	public bool Connect(string host, int port, out string error)
	{
		lock (_sync)
		{
			if (_running)
			{
				error = "IP direct session is already running.";
				return false;
			}
		}

		var client = new TcpClient();
		try
		{
			var async = client.BeginConnect(host, port, null, null);
			if (!async.AsyncWaitHandle.WaitOne(ConnectTimeoutMs))
			{
				client.Close();
				error = $"Connection to {host}:{port} timed out.";
				return false;
			}

			client.EndConnect(async);
		}
		catch (Exception ex)
		{
			client.Close();
			error = ex.Message;
			return false;
		}

		var localPeerId = GenerateGuestPeerId();
		try
		{
			var stream = client.GetStream();
			var hello = new byte[8];
			WriteUInt64(hello, 0, localPeerId);
			stream.Write(hello, 0, hello.Length);
			stream.Flush();
			var connection = new IpConnection(IpDirectTransport.HostPeerId, client, stream);
			lock (_sync)
			{
				_connections[IpDirectTransport.HostPeerId] = connection;
				_isHost = false;
				_localPeerId = localPeerId;
				_running = true;
			}

			StartReadLoop(IpDirectTransport.HostPeerId, client, stream);
		}
		catch (Exception ex)
		{
			client.Close();
			error = ex.Message;
			return false;
		}

		log.LogInformation("IP direct connected to {Host}:{Port} as peer {Peer}.", host, port, localPeerId);
		error = "";
		return true;
	}

	public bool SendTo(ulong peerId, byte[] data, bool reliable)
	{
		if (!_running)
		{
			return false;
		}

		IpConnection connection;
		lock (_sync)
		{
			if (!_connections.TryGetValue(peerId, out connection!))
			{
				return false;
			}
		}

		try
		{
			var frame = new byte[4 + data.Length];
			WriteInt32(frame, 0, data.Length);
			Buffer.BlockCopy(data, 0, frame, 4, data.Length);
			lock (connection.WriteLock)
			{
				connection.Stream.Write(frame, 0, frame.Length);
				connection.Stream.Flush();
			}

			return true;
		}
		catch (Exception ex)
		{
			log.LogWarning(ex, "IP direct send to {Peer} failed — closing the peer connection.", peerId);
			DisconnectPeer(peerId);
			return false;
		}
	}

	/// <summary>Drains incoming frames and disconnect notifications on the Unity main thread.</summary>
	public void Poll()
	{
		while (_incoming.TryDequeue(out var message))
		{
			MessageReceived?.Invoke(message.Sender, message.Data);
		}

		while (_disconnected.TryDequeue(out var peer))
		{
			PeerDisconnected?.Invoke(peer);
		}
	}

	public void Disconnect()
	{
		TcpListener? listener;
		List<IpConnection> connections;
		lock (_sync)
		{
			_running = false;
			listener = _listener;
			_listener = null;
			connections = [.. _connections.Values];
			_connections.Clear();
			_isHost = false;
			_localPeerId = 0;
			_boundPort = 0;
		}

		try
		{
			listener?.Stop();
		}
		catch (Exception ex)
		{
			log.LogDebug(ex, "IP direct listener stop failed (already closed?).");
		}

		foreach (var connection in connections)
		{
			connection.Close();
		}

		log.LogInformation("IP direct session disconnected.");
	}

	public void Dispose() => Disconnect();

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Update() => Poll();

	void ICuoService.Stop() => Disconnect();

	private void AcceptLoop()
	{
		while (_running)
		{
			TcpClient client;
			try
			{
				client = _listener!.AcceptTcpClient();
			}
			catch
			{
				return; // listener stopped
			}

			try
			{
				var stream = client.GetStream();
				var hello = ReadExactly(stream, 8, HelloTimeoutMs);
				if (hello is null)
				{
					client.Close();
					continue;
				}

				var peerId = ReadUInt64(hello, 0);
				if (peerId == 0 || peerId == HostPeerId)
				{
					log.LogWarning("IP direct guest sent invalid peer id {Peer} — rejected.", peerId);
					client.Close();
					continue;
				}

				var connection = new IpConnection(peerId, client, stream);
				lock (_sync)
				{
					if (!_running || _connections.ContainsKey(peerId))
					{
						client.Close();
						continue;
					}

					_connections[peerId] = connection;
				}

				StartReadLoop(peerId, client, stream);
				log.LogInformation("IP direct guest {Peer} connected from {Endpoint}.",
					peerId, client.Client.RemoteEndPoint);
			}
			catch (Exception ex)
			{
				log.LogDebug(ex, "IP direct accept/hello failed for a connection.");
				try
				{
					client.Close();
				}
				catch
				{
					// already closed
				}
			}
		}
	}

	private void StartReadLoop(ulong peerId, TcpClient client, NetworkStream stream)
	{
		var thread = new Thread(() => ReadLoop(peerId, client, stream))
		{
			IsBackground = true,
			Name = $"CUO-IpRead-{peerId}",
		};
		thread.Start();
	}

	private void ReadLoop(ulong peerId, TcpClient client, NetworkStream stream)
	{
		try
		{
			while (_running)
			{
				var lengthBytes = ReadExactly(stream, 4, 0);
				if (lengthBytes is null)
				{
					break;
				}

				var length = ReadInt32(lengthBytes, 0);
				if (length <= 0 || length > MaxFrameSize)
				{
					log.LogWarning("IP direct peer {Peer} sent an invalid frame length {Length} — closing.", peerId, length);
					break;
				}

				var payload = ReadExactly(stream, length, 0);
				if (payload is null)
				{
					break;
				}

				_incoming.Enqueue((peerId, payload));
			}
		}
		catch (Exception ex)
		{
			log.LogDebug(ex, "IP direct read loop for peer {Peer} ended.", peerId);
		}
		finally
		{
			DisconnectPeer(peerId);
		}
	}

	private void DisconnectPeer(ulong peerId)
	{
		lock (_sync)
		{
			if (_connections.TryGetValue(peerId, out var connection))
			{
				_connections.Remove(peerId);
				connection.Close();
			}
		}

		_disconnected.Enqueue(peerId);
	}

	private static byte[]? ReadExactly(NetworkStream stream, int count, int timeoutMs)
	{
		var buffer = new byte[count];
		var read = 0;
		while (read < count)
		{
			if (timeoutMs > 0 && !stream.DataAvailable)
			{
				var deadline = Environment.TickCount + timeoutMs;
				while (!stream.DataAvailable && Environment.TickCount < deadline)
				{
					Thread.Sleep(10);
				}

				if (!stream.DataAvailable)
				{
					return null;
				}
			}

			var n = stream.Read(buffer, read, count - read);
			if (n == 0)
			{
				return null;
			}

			read += n;
		}

		return buffer;
	}

	private static ulong GenerateGuestPeerId()
	{
		var bytes = new byte[8];
		using (var rng = RandomNumberGenerator.Create())
		{
			rng.GetBytes(bytes);
		}

		var id = BitConverter.ToUInt64(bytes, 0);
		if (id == 0 || id == HostPeerId)
		{
			id ^= 0x9E3779B97F4A7C15UL;
		}

		return id;
	}

	private static void WriteInt32(byte[] buffer, int offset, int value)
	{
		buffer[offset] = (byte)(value >> 24);
		buffer[offset + 1] = (byte)(value >> 16);
		buffer[offset + 2] = (byte)(value >> 8);
		buffer[offset + 3] = (byte)value;
	}

	private static int ReadInt32(byte[] buffer, int offset) =>
		(buffer[offset] << 24) | (buffer[offset + 1] << 16) | (buffer[offset + 2] << 8) | buffer[offset + 3];

	private static void WriteUInt64(byte[] buffer, int offset, ulong value)
	{
		for (var i = 0; i < 8; i++)
		{
			buffer[offset + i] = (byte)(value >> (56 - (i * 8)));
		}
	}

	private static ulong ReadUInt64(byte[] buffer, int offset)
	{
		ulong value = 0;
		for (var i = 0; i < 8; i++)
		{
			value = (value << 8) | buffer[offset + i];
		}

		return value;
	}

	private sealed class IpConnection : IDisposable
	{
		internal IpConnection(ulong peerId, TcpClient client, NetworkStream stream)
		{
			PeerId = peerId;
			Client = client;
			Stream = stream;
		}

		internal ulong PeerId { get; }

		internal TcpClient Client { get; }

		internal NetworkStream Stream { get; }

		internal object WriteLock { get; } = new();

		internal void Close()
		{
			try
			{
				Stream.Close();
			}
			catch
			{
				// already closed
			}

			try
			{
				Client.Close();
			}
			catch
			{
				// already closed
			}
		}

		public void Dispose() => Close();
	}
}
