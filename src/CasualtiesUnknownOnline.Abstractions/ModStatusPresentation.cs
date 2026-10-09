using System;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The derived presentation rules a status declaration and its moodle template
/// feed. The declarations own the FACTS (which moodle they name, whether they
/// ask for one row per limb, the per-limb bindings and the two text templates);
/// resolving those facts into "how many rows", "which moodle for this limb" and
/// "what the row says" is the framework's rule and lives here once, so an
/// implementation that computes a fact does not have to restate the rule.
/// </summary>
public static class ModStatusPresentation
{
	extension(IModStatusDefinition status)
	{
		/// <summary>Whether this status asks for one moodle row per affected limb.</summary>
		public bool ShowsPerLimbMoodles => status.Scope == ModStatusScope.Limb && status.ShowPerLimbMoodles;

		/// <summary>
		/// Resolve the moodle id that should be presented for one affected limb.
		/// Returns the declaration's <c>MoodleId</c> for body-scoped statuses, when
		/// per-limb rows are disabled, or when no authored binding matches the limb
		/// name.
		/// </summary>
		public string ResolveMoodleId(string? limbName)
		{
			if (!status.ShowsPerLimbMoodles || limbName is null)
			{
				return status.MoodleId;
			}

			foreach (var binding in ModDeclarationCollections.OrEmpty(status.LimbMoodles))
			{
				if (binding is null)
				{
					continue;
				}

				if (string.Equals(binding.LimbName, limbName, StringComparison.OrdinalIgnoreCase))
				{
					return binding.MoodleId;
				}
			}

			return status.MoodleId;
		}
	}

	extension(IModMoodleDefinition moodle)
	{
		/// <summary>
		/// Format the moodle title for one affected limb using the declaration's
		/// name template. When no format is authored the plain display name is
		/// returned. <c>{name}</c> and <c>{limb}</c> tokens are replaced by the
		/// title and the limb name.
		/// </summary>
		public string FormatLimbDisplayName(string limbName)
		{
			if (string.IsNullOrWhiteSpace(limbName) || string.IsNullOrWhiteSpace(moodle.LimbDisplayNameFormat))
			{
				return moodle.DisplayName;
			}

			return moodle.LimbDisplayNameFormat
				.Replace("{name}", moodle.DisplayName)
				.Replace("{limb}", limbName);
		}

		/// <summary>
		/// Format the moodle description for one affected limb using the
		/// declaration's description template. When no format is authored the plain
		/// description is returned. <c>{description}</c> and <c>{limb}</c> tokens are
		/// replaced by the description and the limb name.
		/// </summary>
		public string FormatLimbDescription(string limbName)
		{
			if (string.IsNullOrWhiteSpace(limbName) || string.IsNullOrWhiteSpace(moodle.LimbDescriptionFormat))
			{
				return moodle.Description;
			}

			return moodle.LimbDescriptionFormat
				.Replace("{description}", moodle.Description)
				.Replace("{limb}", limbName);
		}
	}
}
