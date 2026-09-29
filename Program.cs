namespace RasterAlgorithms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--check", StringComparer.OrdinalIgnoreCase))
        {
            SelfChecks.Run();
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
