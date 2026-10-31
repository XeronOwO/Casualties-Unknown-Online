using System.IO;
using Microsoft.Extensions.Logging;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.ModExample;

/// <summary>
/// The sample's own sentences: one embedded catalogue per language, registered at bind time so an answer a player reads is rendered by that player's client.
/// </summary>
/// <remarks>
/// Its own type because a catalogue is DATA and the mod's bind method is the demonstration's flow: combining them made a sample nobody could read and pushed the
/// mod class past the aggregate line gate.
/// </remarks>
internal static class SampleCatalogues
{
	/// <summary>The languages this sample speaks. A new language is a new file plus a line here — the same shape the framework's own tables use.</summary>
	private static readonly string[] Languages = ["en", "zh"];

	internal static void Register(IModContext context)
	{
		foreach (var language in Languages)
		{
			var name = $"{typeof(SampleCatalogues).Namespace}.Localization.{language}.json";
			using var stream = typeof(SampleCatalogues).Assembly.GetManifestResourceStream(name);
			if (stream is null)
			{
				context.Logger.LogWarning("[Example] no {Language} catalogue embedded as {Name}.", language, name);
				continue;
			}

			using var reader = new StreamReader(stream);
			context.Localisation.RegisterCatalog(language, reader.ReadToEnd());
		}
	}
}
