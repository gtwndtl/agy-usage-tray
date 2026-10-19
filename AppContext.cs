using System.Drawing.Drawing2D;
using AgyUsageTray.Models;
using AgyUsageTray.Services;
using AgyUsageTray.Views;

namespace AgyUsageTray;

public class AppContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _trayMenu;
    private readonly AgyCliService _cliService;
    private readonly DashboardForm _dashboard;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private bool _isRefreshing;
    private int _intervalMinutes = 10;

    public AppContext()
    {
        _cliService = new AgyCliService();
        _dashboard = new DashboardForm(this);

        // Build Tray Menu
        _trayMenu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9f),
            RenderMode = ToolStripRenderMode.System
        };

        var itemOpen = new ToolStripMenuItem("📊 Open Dashboard") { Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        itemOpen.Click += (s, e) => _dashboard.ToggleVisibility();

        var itemRefresh = new ToolStripMenuItem("↻ Refresh Now");
        itemRefresh.Click += async (s, e) => await RefreshUsageAsync();

        var itemInterval = new ToolStripMenuItem("⏱ Auto-Refresh");
        AddIntervalSubMenu(itemInterval);

        var itemStartup = new ToolStripMenuItem("🚀 Start with Windows")
        {
            Checked = StartupService.IsRunOnStartup()
        };
        itemStartup.Click += (s, e) =>
        {
            bool newState = !itemStartup.Checked;
            if (StartupService.SetRunOnStartup(newState))
            {
                itemStartup.Checked = newState;
            }
        };

        var itemExit = new ToolStripMenuItem("✕ Exit");
        itemExit.Click += (s, e) => ExitApp();

        _trayMenu.Items.Add(itemOpen);
        _trayMenu.Items.Add(itemRefresh);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(itemInterval);
        _trayMenu.Items.Add(itemStartup);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(itemExit);

        // Initialize Tray Icon
        _trayIcon = new NotifyIcon
        {
            Icon = CreateAppIcon(Color.FromArgb(56, 189, 248)), // Cyan/Sky
            Text = "Antigravity Quota: Initializing...",
            ContextMenuStrip = _trayMenu,
            Visible = true
        };

        _trayIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _dashboard.ToggleVisibility();
            }
        };

        // Refresh Timer
        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = _intervalMinutes * 60 * 1000
        };
        _refreshTimer.Tick += async (s, e) => await RefreshUsageAsync();
        _refreshTimer.Start();

        // Initial fetch
        _ = RefreshUsageAsync();
    }

    private void AddIntervalSubMenu(ToolStripMenuItem parent)
    {
        var intervals = new (string Label, int Minutes)[]
        {
            ("Every 5 minutes", 5),
            ("Every 10 minutes", 10),
            ("Every 15 minutes", 15),
            ("Every 30 minutes", 30),
            ("Every 1 hour", 60),
            ("Manual Only", 0)
        };

        foreach (var (label, mins) in intervals)
        {
            var item = new ToolStripMenuItem(label)
            {
                Checked = mins == _intervalMinutes
            };
            item.Click += (s, e) =>
            {
                foreach (ToolStripMenuItem sib in parent.DropDownItems)
                    sib.Checked = false;
                item.Checked = true;
                SetRefreshInterval(mins);
            };
            parent.DropDownItems.Add(item);
        }
    }

    public void SetIntervalFromIndex(int index)
    {
        int[] minutes = { 5, 10, 15, 30, 60, 0 };
        if (index >= 0 && index < minutes.Length)
        {
            SetRefreshInterval(minutes[index]);
        }
    }

    public void SetRefreshInterval(int minutes)
    {
        _intervalMinutes = minutes;
        _refreshTimer.Stop();
        if (minutes > 0)
        {
            _refreshTimer.Interval = minutes * 60 * 1000;
            _refreshTimer.Start();
        }
    }

    public async Task RefreshUsageAsync()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;
        _dashboard.SetRefreshing(true);

        try
        {
            var (data, error) = await _cliService.FetchUsageAsync();
            _dashboard.UpdateData(data, error);
            UpdateTray(data, error);
        }
        finally
        {
            _isRefreshing = false;
            _dashboard.SetRefreshing(false);
        }
    }

    private void UpdateTray(AgyUsageRoot? data, string? error)
    {
        if (error != null)
        {
            SetTrayTooltip("AGY Quota: Update error");
            _trayIcon.Icon = CreateAppIcon(Color.FromArgb(239, 68, 68)); // Red
            return;
        }

        if (data?.Command?.Data?.Groups == null)
        {
            SetTrayTooltip("AGY Quota: No data");
            return;
        }

        // Format compact summary for hover tooltip
        string geminiInfo = string.Empty;
        string claudeInfo = string.Empty;
        string argonInfo = string.Empty;
        double minPercentage = 100.0;

        foreach (var group in data.Command.Data.Groups)
        {
            if (group.Name.Contains("Gemini Models", StringComparison.OrdinalIgnoreCase))
            {
                var b5h = group.Buckets.FirstOrDefault(b => b.Window == "5h");
                var bwk = group.Buckets.FirstOrDefault(b => b.Window == "weekly");
                string p5h = b5h != null ? $"{b5h.RemainingPercentage:0}%" : "-";
                string pwk = bwk != null ? $"{bwk.RemainingPercentage:0}%" : "-";
                geminiInfo = $"Gemini: {p5h}(5h) / {pwk}(Wk)";

                if (b5h != null) minPercentage = Math.Min(minPercentage, b5h.RemainingPercentage);
                if (bwk != null) minPercentage = Math.Min(minPercentage, bwk.RemainingPercentage);
            }
            else if (group.Name.Contains("Claude", StringComparison.OrdinalIgnoreCase))
            {
                var b5h = group.Buckets.FirstOrDefault(b => b.Window == "5h");
                var bwk = group.Buckets.FirstOrDefault(b => b.Window == "weekly");
                string p5h = b5h != null ? $"{b5h.RemainingPercentage:0}%" : "-";
                string pwk = bwk != null ? $"{bwk.RemainingPercentage:0}%" : "-";
                claudeInfo = $"Claude: {p5h}(5h) / {pwk}(Wk)";

                if (b5h != null) minPercentage = Math.Min(minPercentage, b5h.RemainingPercentage);
                if (bwk != null) minPercentage = Math.Min(minPercentage, bwk.RemainingPercentage);
            }
            else if (group.Name.Contains("Argon", StringComparison.OrdinalIgnoreCase))
            {
                var bwk = group.Buckets.FirstOrDefault(b => b.Window == "weekly");
                string pwk = bwk != null ? $"{bwk.RemainingPercentage:0}%" : "-";
                argonInfo = $"Argon: {pwk}";

                if (bwk != null) minPercentage = Math.Min(minPercentage, bwk.RemainingPercentage);
            }
        }

        var lines = new List<string> { "AGY Quota:" };
        if (!string.IsNullOrEmpty(geminiInfo)) lines.Add(geminiInfo);
        if (!string.IsNullOrEmpty(claudeInfo)) lines.Add(claudeInfo);
        if (!string.IsNullOrEmpty(argonInfo)) lines.Add(argonInfo);

        SetTrayTooltip(string.Join("\n", lines));

        // Tint icon based on lowest remaining quota (Apple System Colors)
        Color iconColor = minPercentage > 50 ? Color.FromArgb(50, 173, 230) : // Apple Cyan #32ADE6
                          minPercentage >= 20 ? Color.FromArgb(255, 149, 0) : // Apple Orange #FF9500
                                               Color.FromArgb(255, 59, 48);   // Apple Red #FF3B30

        var oldIcon = _trayIcon.Icon;
        _trayIcon.Icon = CreateAppIcon(iconColor);
        oldIcon?.Dispose();
    }

    private void SetTrayTooltip(string text)
    {
        // Windows NotifyIcon.Text max length is 127 characters
        if (text.Length > 126)
            text = text[..123] + "...";

        _trayIcon.Text = text;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    private static Icon CreateAppIcon(Color accentColor)
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Circular background badge (Apple Dark Slate #1E1E22)
        using (var bgBrush = new SolidBrush(Color.FromArgb(30, 30, 34)))
        {
            g.FillEllipse(bgBrush, 1, 1, size - 2, size - 2);
        }

        // Outer ring with Apple accent color
        using (var ringPen = new Pen(accentColor, 2f))
        {
            g.DrawEllipse(ringPen, 2, 2, size - 5, size - 5);
        }

        // Stylized 'A' glyph in the center
        using var path = new GraphicsPath();
        path.AddLine(size / 2f, 7f, size - 9f, size - 9f);
        path.AddLine(size - 9f, size - 9f, 9f, size - 9f);
        path.CloseFigure();

        using (var glyphPen = new Pen(Color.White, 2f))
        {
            g.DrawPath(glyphPen, path);
        }

        // Crossbar
        using (var crossPen = new Pen(accentColor, 2f))
        {
            g.DrawLine(crossPen, 11f, 18f, 21f, 18f);
        }

        IntPtr hIcon = bitmap.GetHicon();
        var icon = (Icon)Icon.FromHandle(hIcon).Clone();
        DestroyIcon(hIcon);
        return icon;
    }

    public void ExitApp()
    {
        _refreshTimer.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _dashboard.Dispose();
        Application.Exit();
    }
}
