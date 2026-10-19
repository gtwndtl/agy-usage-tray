namespace AgyUsageTray;

internal static class Program
{
    private const string MutexName = "AntigravityHUD_SingleInstance_Mutex";

    [STAThread]
    static void Main()
    {
        var logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
        try
        {
            File.WriteAllText(logFile, $"[{DateTime.Now}] Starting...\n");

            using var mutex = new Mutex(true, MutexName, out bool isNewInstance);
            File.AppendAllText(logFile, $"[{DateTime.Now}] Mutex acquired? {isNewInstance}\n");

            if (!isNewInstance)
            {
                File.AppendAllText(logFile, $"[{DateTime.Now}] Already running, exiting.\n");
                return;
            }

            File.AppendAllText(logFile, $"[{DateTime.Now}] Initializing ApplicationConfiguration...\n");
            ApplicationConfiguration.Initialize();

            File.AppendAllText(logFile, $"[{DateTime.Now}] Creating AppContext...\n");
            var context = new AppContext();

            File.AppendAllText(logFile, $"[{DateTime.Now}] Running Application.Run...\n");
            Application.Run(context);

            File.AppendAllText(logFile, $"[{DateTime.Now}] Application.Run exited normally.\n");
        }
        catch (Exception ex)
        {
            File.AppendAllText(logFile, $"[{DateTime.Now}] Exception: {ex}\n");
        }
    }
}