using System;
using System.Collections.Generic;
using System.Globalization;

namespace AvernesSowtware
{
    public sealed partial class AvernesSowtwareScript
    {
        private void DrawMenu()
        {
            List<MenuEntry> entries = GetCurrentEntries();
            if (entries.Count == 0) return;

            float scale = ClampFloat(_settings.MenuScale, 0.75f, 1.30f);
            float width = ClampFloat(_settings.MenuWidth, 0.28f, 0.44f) * scale;
            float x = ClampFloat(_settings.MenuX, 0.005f, 0.995f - width);
            float rowHeight = 0.038f * scale;
            int visible = Math.Min(GetVisibleMenuRows(), entries.Count);
            float headerHeight = 0.105f * scale;
            float footerHeight = 0.038f * scale;
            float height = 0.008f * scale + headerHeight + visible * rowHeight + footerHeight;
            float top = ClampFloat(_settings.MenuY, 0.005f, Math.Max(0.005f, 0.995f - height));
            float fontScale = ClampFloat(_settings.MenuFontScale, 0.70f, 1.40f);
            int alpha = ClampInt(_settings.MenuOpacity, 100, 255);
            int accentR, accentG, accentB;
            GetMenuThemeColor(out accentR, out accentG, out accentB);

            // Solid, high-contrast surfaces; no decorative rails or floating controls.
            DrawRect(x + width / 2.0f, top + height / 2.0f, width, height, 7, 8, 12, alpha);
            DrawRect(x + width / 2.0f, top + headerHeight / 2.0f, width - 0.004f * scale,
                headerHeight - 0.004f * scale, 18, 20, 27, 255);
            DrawRect(x + width / 2.0f, top + headerHeight, width - 0.020f * scale,
                0.0015f * scale, 72, 76, 88, 255);
            DrawRect(x + width / 2.0f, top + 0.0015f * scale, width, 0.003f * scale,
                accentR, accentG, accentB, 255);

            // Minimal clean header: remove the crosshair-like decorative glyph.
            float titleX = x + 0.022f * scale;
            DrawMenuText("AvernesSowtware", titleX, top + 0.014f * scale,
                0.190f * scale * fontScale, 250, 251, 255, 255);
            string headerDetails = "v" + ProductVersion + "   " + _currentPage.ToUpperInvariant();
            DrawMenuText(FitMenuRowText(String.Empty, headerDetails, String.Empty,
                width - 0.060f * scale, scale, fontScale), titleX, top + 0.056f * scale,
                0.108f * scale * fontScale, 205, 211, 222, 255);

            for (int row = 0; row < visible; row++)
            {
                int itemIndex = _scrollOffset + row;
                if (itemIndex < 0 || itemIndex >= entries.Count) continue;
                MenuEntry item = entries[itemIndex];
                float y = top + headerHeight + row * rowHeight;
                bool selected = itemIndex == _selectedIndex;
                float rowCenter = y + rowHeight / 2.0f;

                DrawRect(x + width / 2.0f, rowCenter, width - 0.014f * scale,
                    rowHeight - 0.0025f * scale,
                    selected ? 54 : 23, selected ? 22 : 26, selected ? 29 : 33,
                    selected ? 255 : 235);
                if (selected)
                    DrawRect(x + 0.006f * scale, rowCenter, 0.0030f * scale,
                        rowHeight - 0.006f * scale, accentR, accentG, accentB, 255);

                string number = (itemIndex + 1).ToString("00", CultureInfo.InvariantCulture);
                DrawMenuText(number, x + 0.017f * scale, y + 0.008f * scale,
                    0.166f * scale * fontScale,
                    selected ? 255 : 190, selected ? 238 : 199, selected ? 240 : 211, 255);

                string suffix = item.Value == null ? (item.ChildPage == null ? String.Empty : "  >") : "  [" + item.Value() + "]";
                string rowText = FitMenuRowText(String.Empty, item.Title, suffix,
                    width - 0.070f * scale, scale, fontScale);
                DrawMenuText(rowText, x + 0.064f * scale, y + 0.008f * scale,
                    0.164f * scale * fontScale,
                    selected ? 255 : 238, selected ? 246 : 240, selected ? 248 : 246, 255);
            }

            float footerY = top + height - 0.027f * scale;
            DrawMenuText("UP/DOWN  ENTER  BACKSPACE  F11",
                x + 0.014f * scale, footerY,
                0.086f * scale * fontScale, 180, 185, 196, 255);
            DrawMenuText((_selectedIndex + 1).ToString("00", CultureInfo.InvariantCulture) +
                " / " + entries.Count.ToString("00", CultureInfo.InvariantCulture),
                x + width - 0.060f * scale, footerY,
                0.090f * scale * fontScale, 236, 202, 207, 255);
        }

        private string FitMenuRowText(string marker, string title, string suffix, float width, float scale, float fontScale)
        {
            if (title == null) title = String.Empty;
            if (suffix == null) suffix = String.Empty;
            int maxChars = Math.Max(18, (int)((width - 0.008f * scale) * 158.0f /
                Math.Max(0.70f, scale * fontScale)));
            if (marker.Length + title.Length + suffix.Length <= maxChars) return marker + title + suffix;

            int suffixLimit = Math.Max(0, maxChars / 3);
            if (suffix.Length > suffixLimit)
                suffix = suffix.Substring(0, Math.Max(0, suffixLimit - 3)) + "...";
            int titleLimit = Math.Max(8, maxChars - marker.Length - suffix.Length - 3);
            if (title.Length > titleLimit)
                title = title.Substring(0, Math.Max(0, titleLimit - 3)).TrimEnd() + "...";
            return marker + title + suffix;
        }

        private void DrawMenuText(string text, float x, float y, float scale, int r, int g, int b, int a)
        {
            int style = ClampInt(_settings.MenuFontStyle, 0, 3);
            int font = style == 1 ? 2 : (style == 2 ? 4 : (style == 3 ? 1 : 0));
            DrawTextWithFont(text, x, y, scale, r, g, b, a, font);
        }
    }
}
