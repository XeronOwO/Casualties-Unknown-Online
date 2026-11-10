using System.Globalization;

namespace CasualtiesUnknownOnline.AcceptanceSuite;

/// <summary>
/// The acceptance suite: one typed case per acceptance row, launched and driven against the real
/// game. The project exists so a case that names a reading the game does not expose fails at BUILD
/// time, not after a run — scripts cannot give that.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            Console.WriteLine("usage: CasualtiesUnknownOnline.AcceptanceSuite [--list | --suite <name|all> [--out <dir>] [--host <ws-url>]]");
            Console.WriteLine();
            Console.WriteLine("  --list          print the registered cases and exit");
            Console.WriteLine("  --suite all     run every case and write a report");
            Console.WriteLine("  --host          the desktop client's driver socket (default ws://127.0.0.1:18590)");
            return 64;
        }

        if (args[0] == "--list")
        {
            foreach (var acceptanceCase in CaseRegistry.All)
            {
                Console.WriteLine($"{acceptanceCase.Name}\t{acceptanceCase.Launch}");
            }

            return 0;
        }

        Console.Error.WriteLine("the runner lands with the case model and the driver; no case is registered yet");
        return 2;
    }
}

internal enum LaunchKind
{
    Warm,
    Fresh,
    Restart,
}

internal abstract class AcceptanceCase
{
    public abstract string Name { get; }

    public virtual LaunchKind Launch => LaunchKind.Warm;

    public abstract Task RunAsync(CaseContext context);
}

internal sealed class CaseContext
{
    public CaseContext(string outputDirectory)
    {
        OutputDirectory = outputDirectory;
    }

    public string OutputDirectory { get; }

    public List<string> Notes { get; } = [];
}

internal static class CaseRegistry
{
    public static IReadOnlyList<AcceptanceCase> All { get; } = [];
}
