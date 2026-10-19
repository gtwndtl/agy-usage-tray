using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace AgyUsageTray.Components;

public class QuotaBar : Control
{
    private double _percentage = 100.0;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double Percentage
    {
        get => _percentage;
        set
        {
            _percentage = Math.Clamp(value, 0.0, 100.0);
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color TrackColor { get; set; } = Color.FromArgb(48, 48, 54); // Modern Frosted Track

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color NormalColor { get; set; } = Color.FromArgb(48, 209, 88); // Apple Mint/Green #30D158

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color WarningColor { get; set; } = Color.FromArgb(255, 159, 10); // Apple Orange #FF9F0A

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color DangerColor { get; set; } = Color.FromArgb(255, 69, 58); // Apple Red #FF453A

    public QuotaBar()
    {
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(36, 36, 40);
        Height = 7;
        MinimumSize = new Size(50, 5);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = ClientRectangle;
        if (rect.Width <= 0 || rect.Height <= 0) return;

        // Background Track Capsule
        using (var trackPath = GetCapsulePath(rect))
        using (var trackBrush = new SolidBrush(TrackColor))
        {
            g.FillPath(trackBrush, trackPath);
        }

        // Progress Capsule
        if (_percentage > 0)
        {
            int fillWidth = (int)Math.Round(rect.Width * (_percentage / 100.0));
            if (fillWidth > 0)
            {
                var fillRect = new Rectangle(rect.X, rect.Y, Math.Min(fillWidth, rect.Width), rect.Height);
                Color fillColor = _percentage > 50 ? NormalColor :
                                  _percentage >= 20 ? WarningColor : DangerColor;

                using var fillPath = GetCapsulePath(fillRect);
                using var fillBrush = new SolidBrush(fillColor);
                g.FillPath(fillBrush, fillPath);
            }
        }
    }

    private static GraphicsPath GetCapsulePath(Rectangle rect)
    {
        var path = new GraphicsPath();
        if (rect.Width <= 0 || rect.Height <= 0) return path;

        int diameter = Math.Min(rect.Height, rect.Width);

        if (rect.Width <= diameter)
        {
            path.AddEllipse(rect);
            return path;
        }

        var arc = new Rectangle(rect.X, rect.Y, diameter, diameter);
        path.AddArc(arc, 90, 180); // Left cap

        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 180); // Right cap

        path.CloseFigure();
        return path;
    }
}
