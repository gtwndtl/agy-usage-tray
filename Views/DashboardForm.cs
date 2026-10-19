using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using AgyUsageTray.Components;
using AgyUsageTray.Models;
using AgyUsageTray.Services;

namespace AgyUsageTray.Views;

public class DashboardForm : Form
{
    // DWM API for Native Windows 11 Rounded Corners and Drop Shadow
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 0x2;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public static readonly Color HeaderBg = Color.FromArgb(24, 24, 27);
    public static readonly Color ContentBg = Color.FromArgb(24, 24, 27);
    public static readonly Color FooterBg = Color.FromArgb(20, 20, 23);

    private readonly AppContext _appContext;
    private readonly Label _lblTitle;
    private readonly Label _lblLastUpdated;
    private readonly MiniBarCircleButton _btnFloatBar;
    private readonly RefreshCircleButton _btnRefresh;
    private readonly CloseCircleButton _btnClose;
    private readonly DoubleBufferedPanel _headerPanel;
    private readonly DoubleBufferedPanel _footerPanel;
    private readonly DoubleBufferedPanel _cardContainer;
    private readonly TableLayoutPanel _contentLayout;
    private readonly Label _lblStatus;
    private readonly ModernCheckBox _chkStartup;
    private readonly IntervalPillButton _btnInterval;
    private readonly ContextMenuStrip _intervalMenu;

    public DashboardForm(AppContext appContext)
    {
        _appContext = appContext;

        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Size = new Size(370, 440);
        BackColor = ContentBg;
        ForeColor = Color.White;
        Font = new Font("Segoe UI Variable Text", 9f);
        KeyPreview = true;

        ApplyNativeWindows11Styling();

        // 1. Header Panel (DoubleBuffered)
        _headerPanel = new DoubleBufferedPanel
        {
            Dock = DockStyle.Top,
            Height = 62,
            BackColor = HeaderBg,
            Padding = new Padding(16, 12, 16, 6)
        };
        _headerPanel.MouseDown += Header_MouseDown;

        // App Logo Icon Badge (28x28)
        var iconBadge = new AppLogoBadge
        {
            Location = new Point(16, 16),
            Size = new Size(28, 28),
            BackColor = HeaderBg,
            Cursor = Cursors.SizeAll
        };
        iconBadge.MouseDown += Header_MouseDown;

        _lblTitle = new Label
        {
            Text = "Antigravity",
            Font = new Font("Segoe UI Variable Display", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(244, 244, 245),
            BackColor = HeaderBg,
            Location = new Point(52, 13),
            AutoSize = true
        };
        _lblTitle.MouseDown += Header_MouseDown;

        _lblLastUpdated = new Label
        {
            Text = "Updating...",
            Font = new Font("Segoe UI Variable Text", 8f),
            ForeColor = Color.FromArgb(161, 161, 170),
            BackColor = HeaderBg,
            Location = new Point(53, 35),
            AutoSize = true
        };
        _lblLastUpdated.MouseDown += Header_MouseDown;

        // Floating HUD Mini Bar Button (Icon-only, 26x26)
        _btnFloatBar = new MiniBarCircleButton
        {
            Size = new Size(26, 26),
            Location = new Point(264, 17),
            BackColor = HeaderBg,
            Cursor = Cursors.Hand
        };
        _btnFloatBar.Click += (s, e) =>
        {
            _appContext.ShowFloatBar();
            Hide();
        };

        // Refresh Circle Button (Icon-only with 60 FPS Spin Animation, 26x26)
        _btnRefresh = new RefreshCircleButton
        {
            Size = new Size(26, 26),
            Location = new Point(296, 17),
            BackColor = HeaderBg,
            Cursor = Cursors.Hand
        };
        _btnRefresh.Click += async (s, e) =>
        {
            if (!_btnRefresh.IsSpinning)
                await _appContext.RefreshUsageAsync();
        };

        // Minimalist Close Button (Icon-only, 26x26)
        _btnClose = new CloseCircleButton
        {
            Size = new Size(26, 26),
            Location = new Point(328, 17),
            BackColor = HeaderBg,
            Cursor = Cursors.Hand
        };
        _btnClose.Click += (s, e) => Hide();

        // Header Action ToolTips
        var headerToolTip = new ToolTip
        {
            InitialDelay = 350,
            ReshowDelay = 150
        };
        headerToolTip.SetToolTip(_btnFloatBar, "Switch to Compact Floating HUD Bar");
        headerToolTip.SetToolTip(_btnRefresh, "Refresh Quota");
        headerToolTip.SetToolTip(_btnClose, "Close to Tray");

        _headerPanel.Controls.Add(iconBadge);
        _headerPanel.Controls.Add(_lblTitle);
        _headerPanel.Controls.Add(_lblLastUpdated);
        _headerPanel.Controls.Add(_btnFloatBar);
        _headerPanel.Controls.Add(_btnRefresh);
        _headerPanel.Controls.Add(_btnClose);

        // 2. Footer Panel (DoubleBuffered)
        _footerPanel = new DoubleBufferedPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            BackColor = FooterBg,
            Padding = new Padding(16, 8, 16, 8)
        };

        _chkStartup = new ModernCheckBox
        {
            Text = "Launch at login",
            Font = new Font("Segoe UI Variable Text", 8.25f),
            ForeColor = Color.FromArgb(161, 161, 170),
            BackColor = FooterBg,
            AutoSize = true,
            Location = new Point(16, 12),
            Checked = StartupService.IsRunOnStartup()
        };
        _chkStartup.CheckedChanged += (s, e) =>
        {
            StartupService.SetRunOnStartup(_chkStartup.Checked);
        };

        // Interval Menu
        _intervalMenu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            BackColor = Color.FromArgb(32, 32, 36),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Variable Text", 9f),
            RenderMode = ToolStripRenderMode.System
        };
        BuildIntervalMenuItems();

        _btnInterval = new IntervalPillButton("⏱  10m ▾")
        {
            Size = new Size(72, 24),
            Location = new Point(282, 10),
            BackColor = FooterBg,
            Cursor = Cursors.Hand
        };
        _btnInterval.Click += (s, e) =>
        {
            _intervalMenu.Show(_btnInterval, new Point(0, _btnInterval.Height + 2));
        };

        _footerPanel.Controls.Add(_chkStartup);
        _footerPanel.Controls.Add(_btnInterval);

        // 3. Content Panel (Auto-fit, no scrollbars)
        _cardContainer = new DoubleBufferedPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = false,
            BackColor = ContentBg,
            Padding = new Padding(16, 6, 16, 6)
        };

        _contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = ContentBg,
            Padding = new Padding(0)
        };
        _contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        _lblStatus = new Label
        {
            Text = "Checking quota...",
            Font = new Font("Segoe UI Variable Text", 9.5f),
            ForeColor = Color.FromArgb(161, 161, 170),
            BackColor = ContentBg,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 60,
            Visible = true
        };
        _contentLayout.Controls.Add(_lblStatus);
        _cardContainer.Controls.Add(_contentLayout);

        Controls.Add(_cardContainer);
        Controls.Add(_footerPanel);
        Controls.Add(_headerPanel);
    }

    private void ApplyNativeWindows11Styling()
    {
        try
        {
            int cornerPreference = DWMWCP_ROUND;
            DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

            int darkMode = 1;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
        }
        catch { }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyNativeWindows11Styling();
    }

    private void BuildIntervalMenuItems()
    {
        var options = new (string Label, string ButtonText, int Index)[]
        {
            ("Every 5 minutes", "⏱  5m ▾", 0),
            ("Every 10 minutes", "⏱  10m ▾", 1),
            ("Every 15 minutes", "⏱  15m ▾", 2),
            ("Every 30 minutes", "⏱  30m ▾", 3),
            ("Every 1 hour", "⏱  1h ▾", 4),
            ("Manual Only", "⏱  Manual ▾", 5)
        };

        foreach (var (label, btnText, idx) in options)
        {
            var item = new ToolStripMenuItem(label)
            {
                Checked = idx == 1
            };
            item.Click += (s, e) =>
            {
                foreach (ToolStripMenuItem sib in _intervalMenu.Items)
                    sib.Checked = false;
                item.Checked = true;
                _btnInterval.Text = btnText;
                _appContext.SetIntervalFromIndex(idx);
            };
            _intervalMenu.Items.Add(item);
        }
    }

    private void Header_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }
    }

    public void AdjustHeightToContent()
    {
        if (InvokeRequired)
        {
            Invoke(AdjustHeightToContent);
            return;
        }

        SuspendLayout();

        int headerHeight = _headerPanel.Height;
        int footerHeight = _footerPanel.Height;
        int contentHeight = _contentLayout.GetPreferredSize(new Size(Width - 32, 0)).Height;
        if (contentHeight <= 0) contentHeight = 60;

        int desiredHeight = headerHeight + footerHeight + contentHeight + _cardContainer.Padding.Vertical + 4;

        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetWorkingArea(this);
        int maxHeight = (int)(workingArea.Height * 0.90);

        Height = Math.Min(desiredHeight, maxHeight);
        PositionNearTray();

        ResumeLayout(true);
        Invalidate(true);
        Update();
    }

    public void SetRefreshing(bool isRefreshing)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetRefreshing(isRefreshing));
            return;
        }

        _btnRefresh.IsSpinning = isRefreshing;
        if (isRefreshing && _contentLayout.Controls.Count == 0)
        {
            _lblStatus.Text = "Querying agy...";
            _lblStatus.Visible = true;
            _contentLayout.Controls.Add(_lblStatus);
            AdjustHeightToContent();
        }
    }

    public void UpdateData(AgyUsageRoot? data, string? error)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdateData(data, error));
            return;
        }

        _contentLayout.SuspendLayout();
        _contentLayout.Controls.Clear();

        if (error != null)
        {
            _lblLastUpdated.Text = "Failed to update";
            _lblLastUpdated.ForeColor = Color.FromArgb(248, 113, 113);
            _lblStatus.Text = error;
            _lblStatus.ForeColor = Color.FromArgb(248, 113, 113);
            _lblStatus.Visible = true;
            _contentLayout.Controls.Add(_lblStatus);
        }
        else if (data?.Command?.Data?.Groups != null && data.Command.Data.Groups.Count > 0)
        {
            _lblLastUpdated.Text = $"Updated at {DateTime.Now:h:mm tt}";
            _lblLastUpdated.ForeColor = Color.FromArgb(161, 161, 170);

            foreach (var group in data.Command.Data.Groups)
            {
                var card = new GroupCard(group);
                _contentLayout.Controls.Add(card);
            }
        }
        else
        {
            _lblStatus.Text = "No usage data found.";
            _lblStatus.Visible = true;
            _contentLayout.Controls.Add(_lblStatus);
        }

        _contentLayout.ResumeLayout(true);
        AdjustHeightToContent();
    }

    public void PositionNearTray()
    {
        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetWorkingArea(this);
        int margin = 16;
        int x = workingArea.Right - Width - margin;
        int y = workingArea.Bottom - Height - margin;
        Location = new Point(x, y);
    }

    public void ToggleVisibility()
    {
        if (Visible)
        {
            Hide();
        }
        else
        {
            AdjustHeightToContent();
            Show();
            BringToFront();
            Activate();
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Hide();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Subtle modern 1px outer frame
        using var pen = new Pen(Color.FromArgb(50, 50, 54), 1f);
        g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}

// DoubleBuffered Panel to eliminate all flickering and ghost trails
public class DoubleBufferedPanel : Panel
{
    public DoubleBufferedPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
    }
}

// Minimalist App Logo Badge (28x28)
internal class AppLogoBadge : Control
{
    public AppLogoBadge()
    {
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int size = Math.Min(Width, Height);
        var rect = new Rectangle(0, 0, size - 1, size - 1);

        using (var brush = new SolidBrush(Color.FromArgb(39, 39, 42)))
        {
            g.FillEllipse(brush, rect);
        }

        using (var ringPen = new Pen(Color.FromArgb(56, 189, 248), 1.5f))
        {
            g.DrawEllipse(ringPen, 1, 1, size - 3, size - 3);
        }

        using var path = new GraphicsPath();
        path.AddLine(size / 2f, 6f, size - 8f, size - 8f);
        path.AddLine(size - 8f, size - 8f, 8f, size - 8f);
        path.CloseFigure();

        using (var glyphPen = new Pen(Color.White, 1.8f))
        {
            g.DrawPath(glyphPen, path);
        }

        using (var crossPen = new Pen(Color.FromArgb(56, 189, 248), 1.8f))
        {
            g.DrawLine(crossPen, 10f, 16f, size - 10f, 16f);
        }
    }
}

// Minimalist Circular Refresh Button with 60 FPS Spinning Animation (Icon-Only, 26x26)
internal class RefreshCircleButton : Control
{
    private bool _isHovered;
    private bool _isSpinning;
    private float _rotationAngle;
    private readonly System.Windows.Forms.Timer _spinTimer;

    public bool IsSpinning
    {
        get => _isSpinning;
        set
        {
            if (_isSpinning != value)
            {
                _isSpinning = value;
                if (_isSpinning)
                {
                    _spinTimer.Start();
                }
                else
                {
                    _spinTimer.Stop();
                    _rotationAngle = 0f;
                }
                Invalidate();
            }
        }
    }

    public RefreshCircleButton()
    {
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
        ForeColor = Color.FromArgb(212, 212, 216);

        _spinTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60 FPS
        _spinTimer.Tick += (s, e) =>
        {
            _rotationAngle = (_rotationAngle + 12f) % 360f;
            Invalidate();
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _spinTimer.Stop();
            _spinTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        base.OnPaint(pevent);
        var g = pevent.Graphics;
        g.Clear(Parent?.BackColor ?? BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        if (_isHovered)
        {
            using var bgBrush = new SolidBrush(Color.FromArgb(63, 63, 70));
            g.FillEllipse(bgBrush, rect);
        }

        // Center of circle
        float cx = Width / 2f;
        float cy = Height / 2f;
        float radius = 5.2f;

        var state = g.Save();
        g.TranslateTransform(cx, cy);
        g.RotateTransform(_rotationAngle);

        Color iconColor = _isSpinning ? Color.FromArgb(56, 189, 248) :
                          _isHovered ? Color.White : Color.FromArgb(212, 212, 216);

        using (var iconPen = new Pen(iconColor, 1.5f))
        {
            iconPen.StartCap = LineCap.Round;
            iconPen.EndCap = LineCap.Round;
            g.DrawArc(iconPen, -radius, -radius, radius * 2f, radius * 2f, 45, 270);

            // Vector arrowhead
            using var arrowBrush = new SolidBrush(iconColor);
            var arrowPoints = new PointF[]
            {
                new PointF(-1f, -radius - 3f),
                new PointF(3.5f, -radius),
                new PointF(-1f, -radius + 3f)
            };
            g.FillPolygon(arrowBrush, arrowPoints);
        }
        g.Restore(state);
    }
}

// Close Circle Button (24x24, Clean background erase)
internal class CloseCircleButton : Control
{
    private bool _isHovered;

    public CloseCircleButton()
    {
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        if (_isHovered)
        {
            using var bgBrush = new SolidBrush(Color.FromArgb(63, 63, 70));
            g.FillEllipse(bgBrush, rect);
        }

        Color glyphColor = _isHovered ? Color.White : Color.FromArgb(161, 161, 170);
        using var pen = new Pen(glyphColor, 1.5f);

        int cx = Width / 2;
        int cy = Height / 2;
        int span = 4;
        g.DrawLine(pen, cx - span, cy - span, cx + span, cy + span);
        g.DrawLine(pen, cx - span, cy + span, cx + span, cy - span);
    }
}

// Floating HUD Mini Bar Button (26x26, Clean background erase)
internal class MiniBarCircleButton : Control
{
    private bool _isHovered;

    public MiniBarCircleButton()
    {
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        if (_isHovered)
        {
            using var bgBrush = new SolidBrush(Color.FromArgb(63, 63, 70));
            g.FillEllipse(bgBrush, rect);
        }

        Color glyphColor = _isHovered ? Color.White : Color.FromArgb(161, 161, 170);
        using var pen = new Pen(glyphColor, 1.4f);

        // Draw horizontal floating bar icon (minimal capsule with split preview)
        int cx = Width / 2;
        int cy = Height / 2;
        var barRect = new Rectangle(cx - 7, cy - 4, 14, 8);
        using (var fillBrush = new SolidBrush(Color.FromArgb(42, 42, 48)))
        {
            g.FillRoundedRectangle(fillBrush, barRect, 2);
        }
        g.DrawRoundedRectangle(pen, barRect, 2);

        // Inner mini indicator
        using var dotBrush = new SolidBrush(glyphColor);
        g.FillRectangle(dotBrush, cx - 4, cy - 1, 3, 2);
        g.FillRectangle(dotBrush, cx, cy - 1, 3, 2);
    }
}

// Modern Interval Pill Button (Clean background erase)
internal class IntervalPillButton : Control
{
    private bool _isHovered;

    public IntervalPillButton(string text)
    {
        Text = text;
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
        ForeColor = Color.FromArgb(212, 212, 216);
        Font = new Font("Segoe UI Variable Text", 8.25f);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        base.OnPaint(pevent);
        var g = pevent.Graphics;
        g.Clear(Parent?.BackColor ?? BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        Color bg = _isHovered ? Color.FromArgb(48, 48, 54) : Color.FromArgb(36, 36, 40);

        using (var path = GetCapsulePath(rect))
        using (var brush = new SolidBrush(bg))
        using (var pen = new Pen(Color.FromArgb(63, 63, 70), 1f))
        {
            g.FillPath(brush, path);
            g.DrawPath(pen, path);
        }

        TextRenderer.DrawText(g, Text, Font, rect, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private static GraphicsPath GetCapsulePath(Rectangle rect)
    {
        var path = new GraphicsPath();
        int diameter = rect.Height;
        var arc = new Rectangle(rect.X, rect.Y, diameter, diameter);

        path.AddArc(arc, 90, 180);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 180);
        path.CloseFigure();
        return path;
    }
}

// Modern Clean CheckBox (Clean background erase)
internal class ModernCheckBox : Control
{
    private bool _checked;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked != value)
            {
                _checked = value;
                Invalidate();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public event EventHandler? CheckedChanged;

    public ModernCheckBox()
    {
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Checked = !Checked;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        int boxSize = 15;
        int boxY = (Height - boxSize) / 2;
        var boxRect = new Rectangle(0, boxY, boxSize, boxSize);

        if (_checked)
        {
            using var fillBrush = new SolidBrush(Color.FromArgb(14, 165, 233)); // Sky 500
            g.FillRoundedRectangle(fillBrush, boxRect, 4);

            using var checkPen = new Pen(Color.White, 1.8f);
            g.DrawLines(checkPen, new Point[]
            {
                new Point(boxRect.X + 3, boxRect.Y + 8),
                new Point(boxRect.X + 6, boxRect.Y + 11),
                new Point(boxRect.X + 12, boxRect.Y + 4)
            });
        }
        else
        {
            using var borderPen = new Pen(Color.FromArgb(82, 82, 91), 1.5f);
            using var bgBrush = new SolidBrush(Color.FromArgb(39, 39, 42));
            g.FillRoundedRectangle(bgBrush, boxRect, 4);
            g.DrawRoundedRectangle(borderPen, boxRect, 4);
        }

        var textRect = new Rectangle(boxSize + 8, 0, Width - boxSize - 8, Height);
        TextRenderer.DrawText(g, Text, Font, textRect, ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle rect, int radius)
    {
        using var path = CreateRoundedRectanglePath(rect, radius);
        g.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics g, Pen pen, Rectangle rect, int radius)
    {
        using var path = CreateRoundedRectanglePath(rect, radius);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int diameter = radius * 2;
        var arc = new Rectangle(rect.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
