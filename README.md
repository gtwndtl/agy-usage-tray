# Antigravity HUD (Windows)

A modern, lightweight Windows System Tray & Floating HUD Monitor for Google Antigravity (`agy`) Quotas and Usage.

---

## ✨ Features

### 1. 💻 Floating Mini HUD Bar (Developer Mode)
- **Compact & Horizontal (32px)**: Designed specifically for programmers to glance at quota in real-time without obscuring code or IDEs.
- **Customizable Metric Visibility**: Right-click to show/hide specific models and windows (`Gemini 5h/Wk`, `Claude 5h/Wk`, `Argon Wk`).
- **Dynamic Auto-Resizing**: The HUD automatically sizes itself to fit your selected metrics with zero wasted space.
- **Draggable Anywhere**: Click and drag smoothly anywhere across multi-monitor setups.
- **Apple-Style Status Pills**: Color-coded badges (Mint Green > 50%, Amber Orange 20–50%, Coral Red < 20%).
- **Developer Controls**: Right-click context menu with Opacity levels (`100%`, `95%`, `80%`, `60%`) and **Always on Top** toggle.

### 2. 📊 Full Dashboard Flyout
- **Apple Control Center Aesthetic**: Frosted Dark Slate surface with Windows 11 DWM rounded corners.
- **Detailed Group Cards**: Visual capsule progress bars, countdown reset timers (`Resets in 3h 22m`), and percentage badges.
- **Interactive Header Controls**:
  - `═` **Mini HUD**: Switch to the compact horizontal floating bar.
  - `↻` **Refresh**: 60 FPS vector spinning animation.
  - `✕` **Close**: Minimize to system tray.
- **Auto-Refresh Scheduler**: Configurable intervals (5m, 10m, 15m, 30m, 1h, or manual).
- **Run at Startup**: Native Windows registry integration for automatic background launch.

### 3. 🔔 System Tray (Taskbar)
- Multi-resolution embedded application icon.
- Dynamic color tinting based on lowest quota level.
- Hover tooltip displaying a quick summary of all active quotas.

---

## 🚀 How to Run

1. Open `publish\AntigravityHUD.exe` directly, or
2. Build from source:
   ```bash
   dotnet build
   dotnet publish -c Release -o publish
   ```
