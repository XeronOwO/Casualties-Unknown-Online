using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The generic content binder. It runs once after the first-frame mod discovery
/// (the mods have already registered their content in <c>Bind</c>) and routes
/// every opaque definition to the provider registered for that content kind.
/// This is the extensible skeleton for future recipe/tile/building/liquid
/// providers: the binder does not know game types and does not interpret
/// payloads; providers do.
/// </summary>
public sealed class ModContentBinder(
	IModContentControl control,
	IModsControl mods,
	IEnumerable<IContentBindingProvider> providers,
	ILogger<ModContentBinder> log) : ICuoService
{
	private readonly Dictionary<string, IContentBindingProvider> _providersByKind = BuildProviderMap(providers, log);
	private bool _bound;

	public void Initialize()
	{
	}

	public void Start()
	{
	}

	public void Update()
	{
		if (_bound)
		{
			return;
		}

		_bound = true;
		BindAll();
	}

	public void Stop()
	{
	}

	public void Dispose()
	{
	}

	private void BindAll()
	{
		var sharedContentModIds = new HashSet<string>(
			mods.CurrentModManifests
				.Where(m => IsSharedContentMode(m.NetworkMode))
				.Select(m => m.Id),
			StringComparer.Ordinal);

		foreach (var registration in control.Entries)
		{
			if (!sharedContentModIds.Contains(registration.ModId))
			{
				log.LogWarning(
					"[ModContentBinder] content {ModId}/{Id} was skipped because its mod is not a shared-content network mode.",
					registration.ModId, registration.Definition.Id);
				continue;
			}

			if (!_providersByKind.TryGetValue(registration.Definition.Kind, out var provider))
			{
				log.LogDebug(
					"[ModContentBinder] no provider for content kind {Kind}; {ModId}/{Id} stays opaque.",
					registration.Definition.Kind, registration.ModId, registration.Definition.Id);
				continue;
			}

			try
			{
				if (!provider.TryBind(registration))
				{
					log.LogWarning(
						"[ModContentBinder] provider for {Kind} refused {ModId}/{Id}; the entry is not bound.",
						registration.Definition.Kind, registration.ModId, registration.Definition.Id);
				}
			}
			catch (Exception ex)
			{
				log.LogError(ex,
					"[ModContentBinder] provider for {Kind} threw while binding {ModId}/{Id}; the entry is skipped.",
					registration.Definition.Kind, registration.ModId, registration.Definition.Id);
			}
		}
	}

	/// <summary>
	/// Static content must exist on every peer that can receive the content's
	/// runtime instances. Only modes whose handshake guarantees a matching mod
	/// copy on all players are eligible. HostOnly may legitimately differ per
	/// machine, so its content is not safe to bind into shared world state.
	/// </summary>
	private static bool IsSharedContentMode(NetworkMode mode) =>
		mode is NetworkMode.Synchronized or NetworkMode.Authoritative or NetworkMode.RequiresAllPlayers;

	private static Dictionary<string, IContentBindingProvider> BuildProviderMap(
		IEnumerable<IContentBindingProvider> providers,
		ILogger log)
	{
		var map = new Dictionary<string, IContentBindingProvider>(StringComparer.Ordinal);
		foreach (var provider in providers)
		{
			if (string.IsNullOrWhiteSpace(provider.Kind))
			{
				log.LogWarning("[ModContentBinder] ignored a content provider with an empty kind.");
				continue;
			}

			if (map.ContainsKey(provider.Kind))
			{
				log.LogWarning("[ModContentBinder] multiple providers for content kind {Kind}; the first registration wins.",
					provider.Kind);
				continue;
			}

			map.Add(provider.Kind, provider);
		}

		return map;
	}
}
