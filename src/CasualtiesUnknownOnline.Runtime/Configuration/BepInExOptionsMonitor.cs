using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Microsoft.Extensions.Options;

namespace CasualtiesUnknownOnline.Runtime.Configuration;

/// <summary>
/// The BepInEx ConfigFile → <c>IOptionsMonitor&lt;T&gt;</c> bridge (the
/// config-options decision, 2026-08-09): <c>readValue</c> rebuilds the options
/// snapshot from the bound <c>ConfigEntry</c> values, and every
/// <c>ConfigFile.SettingChanged</c> for one of the watched definitions
/// re-reads it and notifies the monitor listeners. BepInEx owns persistence,
/// schema display and the file; CUO owns the strongly typed hot-reload
/// surface. <c>readValue</c> must never throw for an arbitrary config value —
/// the plugin's factory parses defensively and falls back to defaults.
/// </summary>
public sealed class BepInExOptionsMonitor<T> : IOptionsMonitor<T>, IDisposable
{
	private readonly ConfigFile _configFile;
	private readonly Func<T> _readValue;
	private readonly HashSet<ConfigDefinition> _watchedDefinitions;
	private readonly object _sync = new();
	private readonly List<Listener> _listeners = [];
	private T _current;
	private bool _disposed;

	public BepInExOptionsMonitor(ConfigFile configFile, Func<T> readValue, params ConfigDefinition[] watchedDefinitions)
	{
		_configFile = configFile ?? throw new ArgumentNullException(nameof(configFile));
		_readValue = readValue ?? throw new ArgumentNullException(nameof(readValue));
		_watchedDefinitions = [.. (watchedDefinitions ?? throw new ArgumentNullException(nameof(watchedDefinitions)))];
		_current = readValue();
		configFile.SettingChanged += OnSettingChanged;
	}

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

	public void Dispose()
	{
		lock (_sync)
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			_listeners.Clear();
		}

		_configFile.SettingChanged -= OnSettingChanged;
	}

	private void OnSettingChanged(object sender, SettingChangedEventArgs args)
	{
		if (!_watchedDefinitions.Contains(args.ChangedSetting.Definition))
		{
			return;
		}

		var value = _readValue();
		Action<T, string>[] snapshot;
		lock (_sync)
		{
			if (_disposed)
			{
				return;
			}

			_current = value;
			snapshot = [.. _listeners.ConvertAll(l => l.Callback)];
		}

		foreach (var listener in snapshot)
		{
			listener(value, Options.DefaultName);
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

	private sealed class RemoveListener(BepInExOptionsMonitor<T> owner, Listener token) : IDisposable
	{
		private BepInExOptionsMonitor<T>? _owner = owner;

		public void Dispose()
		{
			_owner?.Remove(token);
			_owner = null;
		}
	}
}
