using System;
using System.IO;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

internal static class RepositoryPaths
{
	internal static string Root { get; } = FindRoot();

	internal static string File(string relativePath) => Path.Combine(Root, relativePath);

	internal static string ReadText(string relativePath) => System.IO.File.ReadAllText(File(relativePath));

	private static string FindRoot()
	{
		var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
		while (directory is not null && !System.IO.File.Exists(Path.Combine(directory.FullName, "CasualtiesUnknownOnline.slnx")))
		{
			directory = directory.Parent;
		}

		if (directory is null)
		{
			throw new InvalidOperationException("could not locate repository root (CasualtiesUnknownOnline.slnx)");
		}

		return directory.FullName;
	}
}
