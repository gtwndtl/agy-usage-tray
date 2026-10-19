using Microsoft.Win32;

namespace AgyUsageTray.Services;

public static class StartupService
{
    private const string AppName = "Antigravity HUD";
    private static readonly string[] LegacyNames = { "AgyUsageTray", "AntigravityHUD" };
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void MigrateLegacyKeys()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key == null) return;

            bool hadLegacy = false;
            foreach (var leg in LegacyNames)
            {
                if (key.GetValue(leg) != null)
                {
                    key.DeleteValue(leg, false);
                    hadLegacy = true;
                }
            }

            if (hadLegacy)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                }
            }
        }
        catch { }
    }

    public static bool IsRunOnStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            if (key == null) return false;
            if (key.GetValue(AppName) != null) return true;
            foreach (var leg in LegacyNames)
            {
                if (key.GetValue(leg) != null) return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public static bool SetRunOnStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key == null) return false;

            if (enable)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    foreach (var leg in LegacyNames)
                        key.DeleteValue(leg, false);
                    key.SetValue(AppName, $"\"{exePath}\"");
                    return true;
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
                foreach (var leg in LegacyNames)
                    key.DeleteValue(leg, false);
                return true;
            }
        }
        catch
        {
            // Ignore or log error
        }
        return false;
    }
}
