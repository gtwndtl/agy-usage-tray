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
    private readonly HudSettings _settings;
    private AgyUsageRoot? _latestData;
    private bool _isRefreshing;
    private float _spinAngle;
    private readonly System.Windows.Forms.Timer _spinTimer;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolTip _toolTip;
    private int _hoveredButton = -1; // 0: Refresh, 1: Expand, 2: Close
    private int _lastToolTipBtn = -2;

    private Rectangle _rectRefresh;
    private Rectangle _rectExpand;
    private Rectangle _rectClose;

    public FloatBarForm(AppContext appContext)
    {
        _appContext = appContext;
        _settings = HudSettings.Load();

        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = _settings.TopMost;
        Height = 32;
        BackColor = Color.FromArgb(20, 20, 24); // Frosted Dark Slate #141418
        ForeColor = Color.White;
        Font = new Font("Segoe UI Variable Text", 8.25f);
        Cursor = Cursors.SizeAll;
        Opacity = _settings.Opacity;

        ApplyNativeWindows11Styling();

        // 60 FPS Spin Timer for Refresh Button
        _spinTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _spinTimer.Tick += (s, e) =>
        {
            _spinAngle = (_spinAngle + 12f) % 360f;
            Invalidate(_rectRefresh);
        };

        _toolTip = new ToolTip
        {
            InitialDelay = 250,
            ReshowDelay = 150,
            AutoPopDelay = 3000
        };

        _contextMenu = new ContextMenuStrip
        {
            ShowImageMargin = true,
            BackColor = Color.FromArgb(32, 32, 36),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Variable Text", 9f),
            RenderMode = ToolStripRenderMode.System
        };
        BuildContextMenu();

        // Initial sizing & screen positioning
        RecalculateLayout(repositionToTopRight: true);
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
        _contextMenu.Items.Clear();

        var itemExpand = new ToolStripMenuItem("⛶  Full Dashboard");
        itemExpand.Click += (s, e) =>
        {
            _appContext.ShowDashboard();
            Hide();
        };

        var itemRefresh = new ToolStripMenuItem("↻  Refresh");
        itemRefresh.Click += async (s, e) => await _appContext.RefreshUsageAsync();

        // Visible Metrics SubMenu
        var itemMetrics = new ToolStripMenuItem("📊 Visible Metrics");
        AddMetricToggle(itemMetrics, "Gemini (5-Hour)", _settings.ShowGemini5h, val => _settings.ShowGemini5h = val);
        AddMetricToggle(itemMetrics, "Gemini (Weekly)", _settings.ShowGeminiWk, val => _settings.ShowGeminiWk = val);
        AddMetricToggle(itemMetrics, "Claude & GPT (5-Hour)", _settings.ShowClaude5h, val => _settings.ShowClaude5h = val);
        AddMetricToggle(itemMetrics, "Claude & GPT (Weekly)", _settings.ShowClaudeWk, val => _settings.ShowClaudeWk = val);
        AddMetricToggle(itemMetrics, "Argon (Weekly)", _settings.ShowArgonWk, val => _settings.ShowArgonWk = val);

        // Opacity SubMenu
        var itemOpacity = new ToolStripMenuItem("👁 Opacity");
        AddOpacityOptions(itemOpacity);

        // Always on top
        var itemTopMost = new ToolStripMenuItem("📌 Always on Top") { Checked = TopMost };
        itemTopMost.Click += (s, e) =>
        {
            TopMost = !TopMost;
            itemTopMost.Checked = TopMost;
            _settings.TopMost = TopMost;
            _settings.Save();
        };

        var itemHide = new ToolStripMenuItem("✕  Close to Tray");
        itemHide.Click += (s, e) => Hide();

        _contextMenu.Items.Add(itemExpand);
        _contextMenu.Items.Add(itemRefresh);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(itemMetrics);
        _contextMenu.Items.Add(itemOpacity);
        _contextMenu.Items.Add(itemTopMost);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(itemHide);
    }

    private void AddMetricToggle(ToolStripMenuItem parent, string text, bool initialValue, Action<bool> onChange)
    {
        var item = new ToolStripMenuItem(text) { Checked = initialValue };
        item.Click += (s, e) =>
        {
            item.Checked = !item.Checked;
            onChange(item.Checked);
            _settings.Save();
            RecalculateLayout(repositionToTopRight: false);
        };
        parent.DropDownItems.Add(item);
    }

    private void AddOpacityOptions(ToolStripMenuItem parent)
    {
        var options = new (string Label, double Value)[]
        {
            ("100%", 1.0),
            ("95%", 0.94),
            ("80%", 0.80),
            ("60%", 0.60)
        };

        foreach (var (lbl, val) in options)
        {
            var item = new ToolStripMenuItem(lbl) { Checked = Math.Abs(Opacity - val) < 0.05 };
            item.Click += (s, e) =>
            {
                foreach (ToolStripMenuItem sib in parent.DropDownItems) sib.Checked = false;
                item.Checked = true;
                Opacity = val;
                _settings.Opacity = val;
                _settings.Save();
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
        RecalculateLayout(repositionToTopRight: false);
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
        Invalidate(_rectRefresh);
    }

    private void RecalculateLayout(bool repositionToTopRight)
    {
        using var tempBmp = new Bitmap(1, 1);
        using var g = Graphics.FromImage(tempBmp);

        int contentWidth = MeasureContentWidth(g);
        int actionWidth = 72; // 3 buttons + spacing
        int newWidth = Math.Max(180, contentWidth + actionWidth + 16);

        var screen = Screen.FromPoint(Location);
        var workArea = screen.WorkingArea;

        int newX = Location.X;
        int newY = Location.Y;

        if (repositionToTopRight)
        {
            newX = workArea.Right - newWidth - 24;
            newY = workArea.Top + 24;
        }
        else
        {
            // Keep on screen if width expands
            if (newX + newWidth > workArea.Right - 8)
                newX = workArea.Right - newWidth - 8;
            if (newX < workArea.Left + 8)
                newX = workArea.Left + 8;
        }

        Size = new Size(newWidth, 32);
        Location = new Point(newX, newY);
        Invalidate();
    }

    private int MeasureContentWidth(Graphics g)
    {
        int w = 12; // Initial left margin
        bool hasAny = false;

        using var fontTitle = new Font("Segoe UI Variable Display", 8f, FontStyle.Bold);
        using var fontPill = new Font("Segoe UI Variable Text", 7.5f, FontStyle.Bold);

        // Gemini
        if (_settings.ShowGemini5h || _settings.ShowGeminiWk)
        {
            w += TextRenderer.MeasureText(g, "Gemini", fontTitle, Size.Empty, TextFormatFlags.NoPadding).Width + 6;
            if (_settings.ShowGemini5h) w += 48;
            if (_settings.ShowGeminiWk) w += 50;
            hasAny = true;
        }

        // Claude
        if (_settings.ShowClaude5h || _settings.ShowClaudeWk)
        {
            if (hasAny) w += 16; // Divider space
            w += TextRenderer.MeasureText(g, "Claude", fontTitle, Size.Empty, TextFormatFlags.NoPadding).Width + 6;
            if (_settings.ShowClaude5h) w += 48;
            if (_settings.ShowClaudeWk) w += 50;
            hasAny = true;
        }

        // Argon
        if (_settings.ShowArgonWk)
        {
            if (hasAny) w += 16; // Divider space
            w += TextRenderer.MeasureText(g, "Argon", fontTitle, Size.Empty, TextFormatFlags.NoPadding).Width + 6;
            w += 40;
            hasAny = true;
        }

        if (!hasAny)
        {
            w += TextRenderer.MeasureText(g, "No metrics selected", fontTitle, Size.Empty, TextFormatFlags.NoPadding).Width;
        }

        return w;
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
        if (!_rectRefresh.Contains(PointToClient(Cursor.Position)) &&
            !_rectExpand.Contains(PointToClient(Cursor.Position)) &&
            !_rectClose.Contains(PointToClient(Cursor.Position)))
        {
            _appContext.ShowDashboard();
            Hide();
        }
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
                0 => "Refresh",
                1 => "Full View",
                2 => "Close",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(tip))
                _toolTip.RemoveAll();
            else
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
            _lastToolTipBtn = -2;
            _toolTip.RemoveAll();
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
        using (var borderPen = new Pen(Color.FromArgb(46, 46, 52), 1f))
        {
            g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
        }

        int curX = 12;
        int centerY = Height / 2;
        bool hasDrawnAny = false;

        using var fontTitle = new Font("Segoe UI Variable Display", 8f, FontStyle.Bold);

        // 1. Gemini
        if (_settings.ShowGemini5h || _settings.ShowGeminiWk)
        {
            var (gemini5h, geminiWk) = ExtractGroupBuckets("Gemini Models");
            TextRenderer.DrawText(g, "Gemini", fontTitle, new Point(curX, centerY - 6), Color.FromArgb(228, 228, 231));
            curX += TextRenderer.MeasureText(g, "Gemini", fontTitle, Size.Empty, TextFormatFlags.NoPadding).Width + 6;

            if (_settings.ShowGemini5h)
            {
                string txt = gemini5h.HasValue ? $"5h {gemini5h.Value:0}%" : "5h -";
                curX = DrawPill(g, curX, centerY, txt, gemini5h ?? 100);
            }
            if (_settings.ShowGeminiWk)
            {
                string txt = geminiWk.HasValue ? $"Wk {geminiWk.Value:0}%" : "Wk -";
                curX = DrawPill(g, curX, centerY, txt, geminiWk ?? 100);
            }
            hasDrawnAny = true;
        }

        // 2. Claude
        if (_settings.ShowClaude5h || _settings.ShowClaudeWk)
        {
            if (hasDrawnAny)
            {
                DrawDivider(g, curX + 7);
                curX += 16;
            }

            var (claude5h, claudeWk) = ExtractGroupBuckets("Claude");
            TextRenderer.DrawText(g, "Claude", fontTitle, new Point(curX, centerY - 6), Color.FromArgb(228, 228, 231));
            curX += TextRenderer.MeasureText(g, "Claude", fontTitle, Size.Empty, TextFormatFlags.NoPadding).Width + 6;

            if (_settings.ShowClaude5h)
            {
                string txt = claude5h.HasValue ? $"5h {claude5h.Value:0}%" : "5h -";
                curX = DrawPill(g, curX, centerY, txt, claude5h ?? 100);
            }
            if (_settings.ShowClaudeWk)
            {
                string txt = claudeWk.HasValue ? $"Wk {claudeWk.Value:0}%" : "Wk -";
                curX = DrawPill(g, curX, centerY, txt, claudeWk ?? 100);
            }
            hasDrawnAny = true;
        }

        // 3. Argon
        if (_settings.ShowArgonWk)
        {
            if (hasDrawnAny)
            {
                DrawDivider(g, curX + 7);
                curX += 16;
            }

            var (_, argonWk) = ExtractGroupBuckets("Argon");
            TextRenderer.DrawText(g, "Argon", fontTitle, new Point(curX, centerY - 6), Color.FromArgb(228, 228, 231));
            curX += TextRenderer.MeasureText(g, "Argon", fontTitle, Size.Empty, TextFormatFlags.NoPadding).Width + 6;

            string txt = argonWk.HasValue ? $"{argonWk.Value:0}%" : "-";
            curX = DrawPill(g, curX, centerY, txt, argonWk ?? 100);
            hasDrawnAny = true;
        }

        if (!hasDrawnAny)
        {
            TextRenderer.DrawText(g, "Right click to choose metrics", fontTitle, new Point(curX, centerY - 6), Color.FromArgb(161, 161, 170));
        }

        // Action Cluster Divider & Buttons
        int actionDividerX = Width - 74;
        DrawDivider(g, actionDividerX);
        DrawActionButtons(g);
    }

    private int DrawPill(Graphics g, int x, int centerY, string label, double percentage)
    {
        Color textColor = percentage > 50 ? Color.FromArgb(48, 209, 88) :   // Mint Green
                          percentage >= 20 ? Color.FromArgb(255, 159, 10) : // Amber Orange
                                             Color.FromArgb(255, 69, 58);   // Coral Red

        Color bgTint = Color.FromArgb(32, textColor.R, textColor.G, textColor.B);

        using var font = new Font("Segoe UI Variable Text", 7.5f, FontStyle.Bold);
        var size = TextRenderer.MeasureText(g, label, font, Size.Empty, TextFormatFlags.NoPadding);
        int pillW = size.Width + 10;
        int pillH = 18;
        int pillY = centerY - (pillH / 2);

        var rect = new Rectangle(x, pillY, pillW, pillH);
        using (var brush = new SolidBrush(bgTint))
        {
            FillRoundedRectangle(g, brush, rect, 4);
        }
        using (var pen = new Pen(Color.FromArgb(55, textColor.R, textColor.G, textColor.B), 1f))
        {
            DrawRoundedRectangle(g, pen, rect, 4);
        }

        TextRenderer.DrawText(g, label, font, rect, textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        return x + pillW + 4;
    }

    private static void DrawDivider(Graphics g, int x)
    {
        using var pen = new Pen(Color.FromArgb(44, 44, 50), 1f);
        g.DrawLine(pen, x, 7, x, 25);
    }

    private void DrawActionButtons(Graphics g)
    {
        int y = (Height - 20) / 2;
        int btnSize = 20;
        int startX = Width - 68;

        _rectRefresh = new Rectangle(startX, y, btnSize, btnSize);
        _rectExpand = new Rectangle(startX + 21, y, btnSize, btnSize);
        _rectClose = new Rectangle(startX + 42, y, btnSize, btnSize);

        // 1. Refresh Button
        DrawMiniButton(g, _rectRefresh, 0, () =>
        {
            float cx = _rectRefresh.X + btnSize / 2f;
            float cy = _rectRefresh.Y + btnSize / 2f;
            float r = 4.2f;

            var st = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(_spinAngle);

            Color col = _isRefreshing ? Color.FromArgb(56, 189, 248) :
                        _hoveredButton == 0 ? Color.White : Color.FromArgb(145, 145, 155);

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
        DrawMiniButton(g, _rectExpand, 1, () =>
        {
            Color col = _hoveredButton == 1 ? Color.White : Color.FromArgb(145, 145, 155);
            using var pen = new Pen(col, 1.3f);
            int cx = _rectExpand.X + btnSize / 2;
            int cy = _rectExpand.Y + btnSize / 2;
            int span = 3;
            g.DrawRectangle(pen, cx - span, cy - span, span * 2, span * 2);
            g.DrawLine(pen, cx - 1, cy - 1, cx + span, cy - span);
        });

        // 3. Close Button (✕)
        DrawMiniButton(g, _rectClose, 2, () =>
        {
            Color col = _hoveredButton == 2 ? Color.FromArgb(255, 69, 58) : Color.FromArgb(145, 145, 155);
            using var pen = new Pen(col, 1.3f);
            int cx = _rectClose.X + btnSize / 2;
            int cy = _rectClose.Y + btnSize / 2;
            int span = 3;
            g.DrawLine(pen, cx - span, cy - span, cx + span, cy + span);
            g.DrawLine(pen, cx - span, cy + span, cx + span, cy - span);
        });
    }

    private void DrawMiniButton(Graphics g, Rectangle rect, int btnIndex, Action drawIcon)
    {
        if (_hoveredButton == btnIndex)
        {
            using var bgBrush = new SolidBrush(Color.FromArgb(46, 46, 52));
            FillRoundedRectangle(g, bgBrush, rect, 4);
        }

        drawIcon();
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

    private static void FillRoundedRectangle(Graphics g, Brush brush, Rectangle rect, int radius)
    {
        using var path = CreateRoundedPath(rect, radius);
        g.FillPath(brush, path);
    }

    private static void DrawRoundedRectangle(Graphics g, Pen pen, Rectangle rect, int radius)
    {
        using var path = CreateRoundedPath(rect, radius);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
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
