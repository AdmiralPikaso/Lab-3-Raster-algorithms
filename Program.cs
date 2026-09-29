namespace RasterAlgorithms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--check", StringComparer.OrdinalIgnoreCase))
        {
            SelfChecks.Run();
            return;
        }
        Application.Run(new MainForm());
    }
}
