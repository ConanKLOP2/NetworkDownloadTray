using System.Drawing;
using System.Drawing.Imaging;

namespace NetworkDownloadTray.Services;

public static class TrayIconRenderer
{
    private static readonly IReadOnlyDictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        ['0'] = ["11111", "10001", "10011", "10101", "11001", "10001", "11111"],
        ['1'] = ["00100", "01100", "00100", "00100", "00100", "00100", "01110"],
        ['2'] = ["11110", "00001", "00001", "01110", "10000", "10000", "11111"],
        ['3'] = ["11110", "00001", "00001", "01110", "00001", "00001", "11110"],
        ['4'] = ["10010", "10010", "10010", "11111", "00010", "00010", "00010"],
        ['5'] = ["11111", "10000", "10000", "11110", "00001", "00001", "11110"],
        ['6'] = ["01110", "10000", "10000", "11110", "10001", "10001", "01110"],
        ['7'] = ["11111", "00001", "00010", "00100", "01000", "01000", "01000"],
        ['8'] = ["01110", "10001", "10001", "01110", "10001", "10001", "01110"],
        ['9'] = ["01110", "10001", "10001", "01111", "00001", "00001", "01110"],
        ['.'] = ["00000", "00000", "00000", "00000", "00000", "00100", "00100"]
    };

    public static Icon Create(double speed, bool measuring)
    {
        const int size = 16;
        string text = measuring ? "..." : DownloadSpeedCalculator.FormatMegabits(speed);
        int scale = 1;
        int glyphWidth = 5 * scale;
        int spacing = text.Length >= 3 ? 0 : scale;
        int totalWidth = text.Length * glyphWidth + (text.Length - 1) * spacing;
        int startX = Math.Max(0, (size - totalWidth) / 2);
        int startY = Math.Max(0, (size - 7 * scale) / 2 + 1);

        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.Transparent);

        for (int index = 0; index < text.Length; index++)
        {
            if (!Glyphs.TryGetValue(text[index], out string[]? glyph)) continue;
            int x = startX + index * (glyphWidth + spacing);
            for (int row = 0; row < glyph.Length; row++)
            for (int column = 0; column < glyph[row].Length; column++)
            if (glyph[row][column] == '1')
            for (int dy = 0; dy < scale; dy++)
            for (int dx = 0; dx < scale; dx++)
            {
                int px = x + column * scale + dx;
                int py = startY + row * scale + dy;
                if (px < size && py < size) bitmap.SetPixel(px, py, Color.FromArgb(255, 255, 210, 55));
            }
        }

        // Clone the icon before the bitmap is disposed. Returning Icon.FromHandle
        // directly would leave the returned icon dependent on the bitmap handle.
        using Icon temporaryIcon = Icon.FromHandle(bitmap.GetHicon());
        return (Icon)temporaryIcon.Clone();
    }
}
