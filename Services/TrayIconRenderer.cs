using System.Buffers.Binary;
using System.Drawing;
using System.IO;

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

    private static readonly string[][] CompactGlyphs =
    [
        ["111","101","101","101","101","101","111"],
        ["010","110","010","010","010","010","111"],
        ["110","001","001","010","100","100","111"],
        ["110","001","001","110","001","001","110"],
        ["101","101","101","111","001","001","001"],
        ["111","100","100","110","001","001","110"],
        ["011","100","100","111","101","101","111"],
        ["111","001","001","010","010","100","100"],
        ["111","101","101","111","101","101","111"],
        ["111","101","101","111","001","001","110"]
    ];

    public static string DisplayText(double speed, bool measuring, bool available = true) =>
        !available ? "-" : measuring ? "..." :
        DownloadSpeedCalculator.FormatMegabits(Math.Min(9999, speed));

    private static readonly string[] DashGlyph = ["00000","00000","00000","11111","00000","00000","00000"];

    // Icon(Stream) copies the bytes into its own buffer, so the stream can be disposed.
    public static Icon Create(string text, int size = 16)
    {
        using var stream = new MemoryStream(EncodeIco(text, size), writable: false);
        return new Icon(stream, size, size);
    }

    public static bool[,] RenderPixels(string text, int size = 16)
    {
        if (size < 16 || size > 64) throw new ArgumentOutOfRangeException(nameof(size));
        if (string.IsNullOrEmpty(text) || text.Length > 4 ||
            text.Any(c => !Glyphs.ContainsKey(c) && c != '-'))
            throw new ArgumentException("Use at most four digits, dots or a dash.", nameof(text));
        bool compact = text.Length == 4;
        int width = compact ? 3 : 5;
        int spacing = text.Length == 3 ? 0 : 1;
        int startX = (16 - (text.Length * width + (text.Length - 1) * spacing)) / 2;
        const int startY = 5; // Preserve the accepted 16px glyph placement.
        var logical = new bool[16, 16];
        for (int i = 0; i < text.Length; i++)
        {
            string[] glyph = text[i] == '-' ? DashGlyph
                : compact && char.IsAsciiDigit(text[i]) ? CompactGlyphs[text[i] - '0'] : Glyphs[text[i]];
            for (int y = 0; y < 7; y++)
            for (int x = 0; x < width; x++)
                logical[startY + y, startX + i * (width + spacing) + x] = glyph[y][x] == '1';
        }
        if (size == 16) return logical;
        var pixels = new bool[size, size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
            pixels[y, x] = logical[y * 16 / size, x * 16 / size];
        return pixels;
    }

    // ICO DIB: explicit BGRA alpha AND a 1-bit transparency mask. No GetHicon
    // conversion, disk I/O, or shared URI cache involved.
    public static byte[] EncodeIco(string text, int size = 16)
    {
        bool[,] pixels = RenderPixels(text, size);
        int maskStride = ((size + 31) / 32) * 4;
        int imageBytes = size * size * 4 + maskStride * size;
        var ico = new byte[22 + 40 + imageBytes];
        Span<byte> s = ico;
        // ICONDIR + ICONDIRENTRY (reserved, width/height, colors, reserved stay zero).
        BinaryPrimitives.WriteUInt16LittleEndian(s[2..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(s[4..], 1);
        s[6] = (byte)size; s[7] = (byte)size;
        BinaryPrimitives.WriteUInt16LittleEndian(s[10..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(s[12..], 32);
        BinaryPrimitives.WriteInt32LittleEndian(s[14..], 40 + imageBytes);
        BinaryPrimitives.WriteInt32LittleEndian(s[18..], 22);
        // BITMAPINFOHEADER (compression, resolution, palette fields stay zero).
        BinaryPrimitives.WriteInt32LittleEndian(s[22..], 40);
        BinaryPrimitives.WriteInt32LittleEndian(s[26..], size);
        BinaryPrimitives.WriteInt32LittleEndian(s[30..], size * 2);
        BinaryPrimitives.WriteUInt16LittleEndian(s[34..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(s[36..], 32);
        BinaryPrimitives.WriteInt32LittleEndian(s[42..], imageBytes);
        int p = 62;
        for (int y = size - 1; y >= 0; y--)
        for (int x = 0; x < size; x++, p += 4)
            if (pixels[y, x]) BinaryPrimitives.WriteUInt32LittleEndian(s[p..], 0xFFFFD237u);
        for (int y = size - 1; y >= 0; y--, p += maskStride)
        for (int x = 0; x < size; x++)
            if (!pixels[y, x]) ico[p + x / 8] |= (byte)(0x80 >> (x % 8));
        return ico;
    }
}
