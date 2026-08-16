using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace CasualtiesUnknownOnline.Runtime.Configuration;

/// <summary>
/// A simple <c>IOptionsMonitor&lt;T&gt;</c> whose value is set programmatically.
/// The production plugin replaces this default registration with the BepInEx
/// config-backed monitor; tests and callers with no live config use this one
/// (and can call <see cref="Set"/> to exercise a hot change).
/// </summary>
public sealed class MutableOptionsMonitor<T>(T initialValue) : IOptionsMonitor<T>
{
	private readonly object _sync = new();
	private readonly List<Listener> _listeners = [];
	private T _current = initialValue;

	public T CurrentValue
	{
		get
		{
			lock (_sync)
			{
				return _current;
			}
		}
	}

	/// <summary>Replace the current value and notify every listener (the config-entry change path).</summary>
	public void Set(T value)
	{
		Action<T, string>[] snapshot;
		lock (_sync)
		{
			_current = value;
			snapshot = [.. _listeners.ConvertAll(l => l.Callback)];
		}

		foreach (var listener in snapshot)
		{
			listener(value, Options.DefaultName);
		}
	}

	public T Get(string? name) => CurrentValue;

	public IDisposable OnChange(Action<T, string> listener)
	{
		if (listener is null)
		{
			throw new ArgumentNullException(nameof(listener));
		}

		lock (_sync)
		{
			var token = new Listener(listener);
			_listeners.Add(token);
			return new RemoveListener(this, token);
		}
	}

	private void Remove(Listener token)
	{
		lock (_sync)
		{
			_listeners.Remove(token);
		}
	}

	private sealed class Listener(Action<T, string> callback)
	{
		internal Action<T, string> Callback { get; } = callback;
	}

	private sealed class RemoveListener(MutableOptionsMonitor<T> owner, Listener token) : IDisposable
	{
		private MutableOptionsMonitor<T>? _owner = owner;

		public void Dispose()
		{
			_owner?.Remove(token);
			_owner = null;
		}
	}
}
