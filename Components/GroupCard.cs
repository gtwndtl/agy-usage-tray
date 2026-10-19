using System.Drawing.Drawing2D;
using System.Drawing.Text;
using AgyUsageTray.Models;

namespace AgyUsageTray.Components;

public class GroupCard : Panel
{
    private static readonly Color CardBg = Color.FromArgb(36, 36, 40); // Sleek elevated dark card #242428
    private readonly QuotaGroup _group;

    public GroupCard(QuotaGroup group)
    {
        _group = group;
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);

        BackColor = CardBg;
        ForeColor = Color.White;
        Margin = new Padding(0, 0, 0, 10);
        Padding = new Padding(14, 12, 14, 12);
        Dock = DockStyle.Top;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        BuildContent();
    }

    private void BuildContent()
    {
        SuspendLayout();
        Controls.Clear();

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = CardBg,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70f));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));

        // Group Title (Left)
        var lblTitle = new Label
        {
            Text = _group.Name,
            Font = new Font("Segoe UI Variable Display", 10.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(245, 245, 247),
            BackColor = CardBg,
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 0, 2)
        };
        mainLayout.Controls.Add(lblTitle, 0, 0);

        // Group Subtitle Badge (Right)
        string subBadge = GetCleanSubtitle(_group);
        if (!string.IsNullOrEmpty(subBadge))
        {
            var lblBadge = new Label
            {
                Text = subBadge,
                Font = new Font("Segoe UI Variable Text", 8f, FontStyle.Regular),
                ForeColor = Color.FromArgb(152, 152, 157),
                BackColor = CardBg,
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(0, 2, 0, 2)
            };
            mainLayout.Controls.Add(lblBadge, 1, 0);
        }

        // Buckets Container
        var bucketsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = CardBg,
            Margin = new Padding(0, 8, 0, 0),
            Padding = new Padding(0)
        };
        bucketsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        foreach (var bucket in _group.Buckets)
        {
            bucketsPanel.Controls.Add(CreateBucketControl(bucket));
        }

        mainLayout.Controls.Add(bucketsPanel, 0, 1);
        mainLayout.SetColumnSpan(bucketsPanel, 2);

        Controls.Add(mainLayout);
        ResumeLayout(true);
    }

    private static string GetCleanSubtitle(QuotaGroup group)
    {
        if (group.Name.Contains("Gemini Models", StringComparison.OrdinalIgnoreCase))
            return "Flash & Pro";
        if (group.Name.Contains("Claude", StringComparison.OrdinalIgnoreCase))
            return "Claude & GPT";
        if (group.Name.Contains("Argon", StringComparison.OrdinalIgnoreCase))
            return "Argon Engine";
        return string.Empty;
    }

    private static Control CreateBucketControl(QuotaBucket bucket)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 3,
            BackColor = CardBg,
            Margin = new Padding(0, 4, 0, 6),
            Padding = new Padding(0)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72f));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));

        // Window Label (Clean naming: "Weekly Quota", "5-Hour Window")
        string displayName = bucket.Name;
        if (displayName.EndsWith(" Remaining", StringComparison.OrdinalIgnoreCase))
            displayName = displayName[..^10];

        if (bucket.Window.Equals("5h", StringComparison.OrdinalIgnoreCase))
            displayName = "5-Hour Window";
        else if (bucket.Window.Equals("weekly", StringComparison.OrdinalIgnoreCase))
            displayName = "Weekly Quota";

        var lblName = new Label
        {
            Text = displayName,
            Font = new Font("Segoe UI Variable Text", 9f, FontStyle.Regular),
            ForeColor = Color.FromArgb(215, 215, 220),
            BackColor = CardBg,
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0)
        };
        panel.Controls.Add(lblName, 0, 0);

        // Percentage Badge (Pill style)
        var pct = bucket.RemainingPercentage;
        var badge = new PercentagePill(pct)
        {
            Dock = DockStyle.Right,
            Size = new Size(58, 20),
            Margin = new Padding(0, 0, 0, 2)
        };
        panel.Controls.Add(badge, 1, 0);

        // Smooth Quota Bar
        var bar = new QuotaBar
        {
            Percentage = pct,
            Dock = DockStyle.Fill,
            Height = 6,
            Margin = new Padding(0, 4, 0, 3)
        };
        panel.Controls.Add(bar, 0, 1);
        panel.SetColumnSpan(bar, 2);

        // Reset info with minimal typography
        var countdown = bucket.GetFormattedResetCountdown();
        var lblReset = new Label
        {
            Text = string.IsNullOrEmpty(countdown) ? " " : $"◷  {countdown}",
            Font = new Font("Segoe UI Variable Text", 7.75f, FontStyle.Regular),
            ForeColor = Color.FromArgb(142, 142, 147),
            BackColor = CardBg,
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 0, 4)
        };
        panel.Controls.Add(lblReset, 0, 2);
        panel.SetColumnSpan(lblReset, 2);

        return panel;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = 10;

        using var path = GetRoundedRectPath(rect, radius);
        using var brush = new SolidBrush(BackColor);
        using var pen = new Pen(Color.FromArgb(52, 52, 56), 1f); // Delicate 1px border

        g.FillPath(brush, path);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        if (rect.Width <= 0 || rect.Height <= 0) return path;

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

// Percentage Pill Badge with clean background clearing
internal class PercentagePill : Control
{
    private static readonly Color CardBg = Color.FromArgb(36, 36, 40);
    private readonly double _percentage;

    public PercentagePill(double percentage)
    {
        _percentage = percentage;
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = CardBg;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? CardBg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        Color fgColor = _percentage > 50 ? Color.FromArgb(48, 209, 88) :   // Mint/Green
                        _percentage >= 20 ? Color.FromArgb(255, 159, 10) : // Orange
                                            Color.FromArgb(255, 69, 58);   // Red

        Color bgColor = Color.FromArgb(28, fgColor.R, fgColor.G, fgColor.B); // Subtle tint

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = rect.Height / 2;

        using var path = GetCapsulePath(rect);
        using var brush = new SolidBrush(bgColor);
        using var pen = new Pen(Color.FromArgb(45, fgColor.R, fgColor.G, fgColor.B), 1f);

        g.FillPath(brush, path);
        g.DrawPath(pen, path);

        string text = $"{_percentage:0.#}%";
        using var font = new Font("Segoe UI Variable Text", 8.25f, FontStyle.Bold);
        TextRenderer.DrawText(g, text, font, rect, fgColor,
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
