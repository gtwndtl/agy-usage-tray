using Microsoft.Win32;

namespace AgyUsageTray.Services;

public class HudSettings
{
    public bool ShowGemini5h { get; set; } = true;
    public bool ShowGeminiWk { get; set; } = true;
    public bool ShowClaude5h { get; set; } = true;
    public bool ShowClaudeWk { get; set; } = true;
    public bool ShowArgonWk { get; set; } = true;
    public double Opacity { get; set; } = 0.94;
    public bool TopMost { get; set; } = true;

    private const string RegSubKey = @"Software\AgyUsageTray";

    public static HudSettings Load()
    {
        var settings = new HudSettings();
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegSubKey);
            if (key != null)
            {
                settings.ShowGemini5h = Convert.ToInt32(key.GetValue("Hud_Gemini5h", 1)) == 1;
                settings.ShowGeminiWk = Convert.ToInt32(key.GetValue("Hud_GeminiWk", 1)) == 1;
                settings.ShowClaude5h = Convert.ToInt32(key.GetValue("Hud_Claude5h", 1)) == 1;
                settings.ShowClaudeWk = Convert.ToInt32(key.GetValue("Hud_ClaudeWk", 1)) == 1;
                settings.ShowArgonWk = Convert.ToInt32(key.GetValue("Hud_ArgonWk", 1)) == 1;

                if (double.TryParse(key.GetValue("Hud_Opacity", "0.94")?.ToString(), System.Globalization.CultureInfo.InvariantCulture, out double op))
                    settings.Opacity = Math.Clamp(op, 0.4, 1.0);

                settings.TopMost = Convert.ToInt32(key.GetValue("Hud_TopMost", 1)) == 1;
            }
        }
        catch { }
        return settings;
    }

    public void Save()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegSubKey);
            key.SetValue("Hud_Gemini5h", ShowGemini5h ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("Hud_GeminiWk", ShowGeminiWk ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("Hud_Claude5h", ShowClaude5h ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("Hud_ClaudeWk", ShowClaudeWk ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("Hud_ArgonWk", ShowArgonWk ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("Hud_Opacity", Opacity.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), RegistryValueKind.String);
            key.SetValue("Hud_TopMost", TopMost ? 1 : 0, RegistryValueKind.DWord);
        }
        catch { }
    }
}
