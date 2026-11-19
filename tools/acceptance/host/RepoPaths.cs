namespace CasualtiesUnknownOnline.AcceptanceHost;

/// <summary>
/// The repository root, found by walking up from the executable until the solution file is beside the
/// current directory. No machine path is compiled in, so a checkout anywhere works.
/// </summary>
internal static class RepoPaths
{
	private const string Marker = "CasualtiesUnknownOnline.slnx";

	public static string RepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null)
		{
			if (File.Exists(Path.Combine(directory.FullName, Marker)))
			{
				return directory.FullName;
			}

			directory = directory.Parent;
		}

		throw new CommandFailure(2, $"could not find {Marker} above {AppContext.BaseDirectory}; run the host from the checkout");
	}
}
