using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using AgyUsageTray.Models;
using AgyUsageTray.Services;

namespace AgyUsageTray.Views;

public class FloatBarForm : Form
{
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

    private readonly AppContext _appContext;
    private AgyUsageRoot? _latestData;
    private bool _isRefreshing;
    private float _spinAngle;
    private readonly System.Windows.Forms.Timer _spinTimer;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolTip _toolTip;
    private int _lastToolTipBtn = -2;

    // Hover detection for interactive segments
    private Rectangle _rectRefresh;
    private Rectangle _rectExpand;
    private Rectangle _rectClose;
    private int _hoveredButton = -1; // 0: Refresh, 1: Expand, 2: Close

    public FloatBarForm(AppContext appContext)
    {
        _appContext = appContext;

        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Size = new Size(460, 36);
        BackColor = Color.FromArgb(20, 20, 24); // Frosted Dark Slate #141418
        ForeColor = Color.White;
        Font = new Font("Segoe UI Variable Text", 8.25f);
        Cursor = Cursors.SizeAll; // Entire bar is draggable
        Opacity = 0.94; // Modern developer translucent HUD

        ApplyNativeWindows11Styling();

        // Position near top-right of screen by default
        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetWorkingArea(this);
        Location = new Point(workingArea.Right - Width - 32, workingArea.Top + 32);

        // 60 FPS Spin Timer for Refresh Icon
        _spinTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _spinTimer.Tick += (s, e) =>
        {
            _spinAngle = (_spinAngle + 12f) % 360f;
            Invalidate();
        };

        // ToolTip
        _toolTip = new ToolTip
        {
            InitialDelay = 400,
            ReshowDelay = 200,
            AutoPopDelay = 4000
        };

        // Context Menu
        _contextMenu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            BackColor = Color.FromArgb(32, 32, 36),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Variable Text", 8.5f),
            RenderMode = ToolStripRenderMode.System
        };
        BuildContextMenu();
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

    private void BuildContextMenu()
    {
        var itemExpand = new ToolStripMenuItem("⛶  Open Full Dashboard");
        itemExpand.Click += (s, e) =>
        {
            _appContext.ShowDashboard();
            Hide();
        };

        var itemRefresh = new ToolStripMenuItem("↻  Refresh Now");
        itemRefresh.Click += async (s, e) => await _appContext.RefreshUsageAsync();

        var itemOpacity = new ToolStripMenuItem("👁  Opacity");
        AddOpacityOptions(itemOpacity);

        var itemTopMost = new ToolStripMenuItem("📌 Always on Top") { Checked = TopMost };
        itemTopMost.Click += (s, e) =>
        {
            TopMost = !TopMost;
            itemTopMost.Checked = TopMost;
        };

        var itemHide = new ToolStripMenuItem("✕  Hide to Tray");
        itemHide.Click += (s, e) => Hide();

        _contextMenu.Items.Add(itemExpand);
        _contextMenu.Items.Add(itemRefresh);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(itemOpacity);
        _contextMenu.Items.Add(itemTopMost);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(itemHide);
    }

    private void AddOpacityOptions(ToolStripMenuItem parent)
    {
        var options = new (string Label, double Value)[]
        {
            ("100%", 1.0),
            ("95% (Default)", 0.94),
            ("80% Translucent", 0.80),
            ("60% Stealth", 0.60)
        };

        foreach (var (lbl, val) in options)
        {
            var item = new ToolStripMenuItem(lbl) { Checked = Math.Abs(Opacity - val) < 0.05 };
            item.Click += (s, e) =>
            {
                foreach (ToolStripMenuItem sib in parent.DropDownItems) sib.Checked = false;
                item.Checked = true;
                Opacity = val;
            };
            parent.DropDownItems.Add(item);
        }
    }

    public void UpdateData(AgyUsageRoot? data)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdateData(data));
            return;
        }

        _latestData = data;
        Invalidate();
    }

    public void SetRefreshing(bool isRefreshing)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetRefreshing(isRefreshing));
            return;
        }

        _isRefreshing = isRefreshing;
        if (isRefreshing)
            _spinTimer.Start();
        else
        {
            _spinTimer.Stop();
            _spinAngle = 0f;
        }
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButtons.Left)
        {
            if (_rectRefresh.Contains(e.Location))
            {
                if (!_isRefreshing) _ = _appContext.RefreshUsageAsync();
                return;
            }
            if (_rectExpand.Contains(e.Location))
            {
                _appContext.ShowDashboard();
                Hide();
                return;
            }
            if (_rectClose.Contains(e.Location))
            {
                Hide();
                return;
            }

            // Drag window anywhere else
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }
        else if (e.Button == MouseButtons.Right)
        {
            _contextMenu.Show(this, e.Location);
        }
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        base.OnDoubleClick(e);
        _appContext.ShowDashboard();
        Hide();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        int prevHover = _hoveredButton;
        if (_rectRefresh.Contains(e.Location))
        {
            _hoveredButton = 0;
            Cursor = Cursors.Hand;
        }
        else if (_rectExpand.Contains(e.Location))
        {
            _hoveredButton = 1;
            Cursor = Cursors.Hand;
        }
        else if (_rectClose.Contains(e.Location))
        {
            _hoveredButton = 2;
            Cursor = Cursors.Hand;
        }
        else
        {
            _hoveredButton = -1;
            Cursor = Cursors.SizeAll;
        }

        if (_hoveredButton != _lastToolTipBtn)
        {
            _lastToolTipBtn = _hoveredButton;
            string tip = _hoveredButton switch
            {
                0 => "Refresh Quota (Click)",
                1 => "Expand to Full Dashboard (Double Click)",
                2 => "Hide to Tray",
                _ => "Antigravity Quota HUD • Drag to reposition • Right-click for options"
            };
            _toolTip.SetToolTip(this, tip);
        }

        if (prevHover != _hoveredButton) Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoveredButton != -1)
        {
            _hoveredButton = -1;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        // Subtle 1px outer frame
        using (var borderPen = new Pen(Color.FromArgb(48, 48, 54), 1f))
        {
            g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
        }

        // 1. Antigravity Glyph & Status Dot (X: 10)
        DrawBrandBadge(g, 10, Height / 2f);

        // Extract Quotas
        var (gemini5h, geminiWk) = ExtractGroupBuckets("Gemini Models");
        var (claude5h, claudeWk) = ExtractGroupBuckets("Claude");
        var (_, argonWk) = ExtractGroupBuckets("Argon");

        // 2. Segment 1: Gemini (X: 36 .. 160)
        DrawModelSegment(g, 36, "Gemini", gemini5h, geminiWk, 120);

        // Divider
        DrawDivider(g, 162);

        // 3. Segment 2: Claude & GPT (X: 170 .. 294)
        DrawModelSegment(g, 170, "Claude", claude5h, claudeWk, 120);

        // Divider
        DrawDivider(g, 296);

        // 4. Segment 3: Argon (X: 304 .. 380)
        DrawArgonSegment(g, 304, argonWk, 80);

        // Divider
        DrawDivider(g, 390);

        // 5. Action Buttons (X: 396 .. 454)
        DrawActionButtons(g, 396);
    }

    private (double? b5h, double? bwk) ExtractGroupBuckets(string groupPattern)
    {
        if (_latestData?.Command?.Data?.Groups == null) return (null, null);

        var grp = _latestData.Command.Data.Groups
            .FirstOrDefault(g => g.Name.Contains(groupPattern, StringComparison.OrdinalIgnoreCase));

        if (grp == null) return (null, null);

        var b5h = grp.Buckets.FirstOrDefault(b => b.Window == "5h")?.RemainingPercentage;
        var bwk = grp.Buckets.FirstOrDefault(b => b.Window == "weekly")?.RemainingPercentage;
        return (b5h, bwk);
    }

    private static void DrawDivider(Graphics g, int x)
    {
        using var pen = new Pen(Color.FromArgb(48, 48, 54), 1f);
        g.DrawLine(pen, x, 7, x, 29);
    }

    private void DrawBrandBadge(Graphics g, float x, float cy)
    {
        float sz = 16f;
        float y = cy - (sz / 2f);

        using (var bgBrush = new SolidBrush(Color.FromArgb(32, 32, 36)))
        {
            g.FillEllipse(bgBrush, x, y, sz, sz);
        }

        // Outer ring
        Color ringColor = GetLowestStatusColor();
        using (var ringPen = new Pen(ringColor, 1.2f))
        {
            g.DrawEllipse(ringPen, x, y, sz, sz);
        }

        // 'A' Glyph
        using var path = new GraphicsPath();
        path.AddLine(x + sz / 2f, y + 3.5f, x + sz - 4.5f, y + sz - 4.5f);
        path.AddLine(x + sz - 4.5f, y + sz - 4.5f, x + 4.5f, y + sz - 4.5f);
        path.CloseFigure();

        using (var whitePen = new Pen(Color.White, 1.2f))
        {
            g.DrawPath(whitePen, path);
        }
    }

    private Color GetLowestStatusColor()
    {
        if (_latestData?.Command?.Data?.Groups == null) return Color.FromArgb(56, 189, 248);

        double minPct = 100.0;
        foreach (var grp in _latestData.Command.Data.Groups)
        {
            foreach (var b in grp.Buckets)
                minPct = Math.Min(minPct, b.RemainingPercentage);
        }

        return minPct > 50 ? Color.FromArgb(48, 209, 88) :   // Mint Green
               minPct >= 20 ? Color.FromArgb(255, 159, 10) : // Orange
                              Color.FromArgb(255, 69, 58);   // Red
    }

    private static void DrawModelSegment(Graphics g, int x, string name, double? p5h, double? pwk, int width)
    {
        using var fontBold = new Font("Segoe UI Variable Display", 8.25f, FontStyle.Bold);
        using var fontRegular = new Font("Segoe UI Variable Text", 7.5f, FontStyle.Regular);

        // Title
        TextRenderer.DrawText(g, name, fontBold, new Point(x, 4), Color.FromArgb(240, 240, 245));

        // Stats: "5h: 88%  Wk: 96%"
        string str5h = p5h.HasValue ? $"{p5h.Value:0}%" : "-";
        string strWk = pwk.HasValue ? $"{pwk.Value:0}%" : "-";

        string stats = $"5h:{str5h}  Wk:{strWk}";
        var statsColor = Color.FromArgb(170, 170, 178);
        TextRenderer.DrawText(g, stats, fontRegular, new Point(x, 19), statsColor);

        // Micro Progress Bar (Width 36px, Height 3px at top-right of segment)
        double lowest = Math.Min(p5h ?? 100, pwk ?? 100);
        DrawMicroBar(g, x + width - 40, 8, 38, 3, lowest);
    }

    private static void DrawArgonSegment(Graphics g, int x, double? pwk, int width)
    {
        using var fontBold = new Font("Segoe UI Variable Display", 8.25f, FontStyle.Bold);
        using var fontRegular = new Font("Segoe UI Variable Text", 7.5f, FontStyle.Regular);

        TextRenderer.DrawText(g, "Argon", fontBold, new Point(x, 4), Color.FromArgb(240, 240, 245));

        string strWk = pwk.HasValue ? $"{pwk.Value:0}%" : "-";
        TextRenderer.DrawText(g, $"Wk:{strWk}", fontRegular, new Point(x, 19), Color.FromArgb(170, 170, 178));

        DrawMicroBar(g, x + width - 36, 8, 32, 3, pwk ?? 100);
    }

    private static void DrawMicroBar(Graphics g, int x, int y, int w, int h, double percentage)
    {
        var rect = new Rectangle(x, y, w, h);
        using (var bgBrush = new SolidBrush(Color.FromArgb(42, 42, 46)))
        {
            g.FillRectangle(bgBrush, rect);
        }

        if (percentage > 0)
        {
            int fillW = (int)Math.Round(w * (Math.Clamp(percentage, 0, 100) / 100.0));
            if (fillW > 0)
            {
                Color fillColor = percentage > 50 ? Color.FromArgb(48, 209, 88) :
                                  percentage >= 20 ? Color.FromArgb(255, 159, 10) :
                                                     Color.FromArgb(255, 69, 58);
                using var fillBrush = new SolidBrush(fillColor);
                g.FillRectangle(fillBrush, x, y, fillW, h);
            }
        }
    }

    private void DrawActionButtons(Graphics g, int x)
    {
        int y = 8;
        int btnSize = 19;
        int gap = 3;

        _rectRefresh = new Rectangle(x, y, btnSize, btnSize);
        _rectExpand = new Rectangle(x + btnSize + gap, y, btnSize, btnSize);
        _rectClose = new Rectangle(x + (btnSize + gap) * 2, y, btnSize, btnSize);

        // 1. Refresh Button
        DrawMiniIconButton(g, _rectRefresh, 0, () =>
        {
            float cx = _rectRefresh.X + btnSize / 2f;
            float cy = _rectRefresh.Y + btnSize / 2f;
            float r = 4.2f;

            var st = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(_spinAngle);

            Color col = _isRefreshing ? Color.FromArgb(56, 189, 248) :
                        _hoveredButton == 0 ? Color.White : Color.FromArgb(170, 170, 178);

            using (var pen = new Pen(col, 1.3f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawArc(pen, -r, -r, r * 2f, r * 2f, 45, 270);

                using var arrowBrush = new SolidBrush(col);
                var arrow = new PointF[] { new PointF(-1f, -r - 2.5f), new PointF(2.8f, -r), new PointF(-1f, -r + 2.5f) };
                g.FillPolygon(arrowBrush, arrow);
            }
            g.Restore(st);
        });

        // 2. Expand Button (⛶ / ↗)
        DrawMiniIconButton(g, _rectExpand, 1, () =>
        {
            Color col = _hoveredButton == 1 ? Color.White : Color.FromArgb(170, 170, 178);
            using var pen = new Pen(col, 1.3f);
            int cx = _rectExpand.X + btnSize / 2;
            int cy = _rectExpand.Y + btnSize / 2;
            int span = 3;
            g.DrawRectangle(pen, cx - span, cy - span, span * 2, span * 2);
            g.DrawLine(pen, cx - 1, cy - 1, cx + span, cy - span);
        });

        // 3. Close/Hide Button (✕)
        DrawMiniIconButton(g, _rectClose, 2, () =>
        {
            Color col = _hoveredButton == 2 ? Color.FromArgb(255, 69, 58) : Color.FromArgb(170, 170, 178);
            using var pen = new Pen(col, 1.3f);
            int cx = _rectClose.X + btnSize / 2;
            int cy = _rectClose.Y + btnSize / 2;
            int span = 3;
            g.DrawLine(pen, cx - span, cy - span, cx + span, cy + span);
            g.DrawLine(pen, cx - span, cy + span, cx + span, cy - span);
        });
    }

    private void DrawMiniIconButton(Graphics g, Rectangle rect, int btnIndex, Action drawIcon)
    {
        if (_hoveredButton == btnIndex)
        {
            using var bgBrush = new SolidBrush(Color.FromArgb(50, 50, 56));
            g.FillEllipse(bgBrush, rect);
        }

        drawIcon();
    }
}
