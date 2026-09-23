using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using NetworkDownloadTray.Services;

namespace NetworkDownloadTray.Tests;

public class RendererTests
{
    [Theory]
    [InlineData("0",16)] [InlineData("46",16)] [InlineData("403",16)] [InlineData("9999",16)]
    [InlineData("...",16)] [InlineData("-",16)] [InlineData("88",20)] [InlineData("999",24)]
    [InlineData("8888",32)] [InlineData("46",48)] [InlineData("46",64)]
    public void EncodedAlphaAndMaskMatchEveryPixel(string text, int size)
    {
        bool[,] pixels = TrayIconRenderer.RenderPixels(text, size);
        byte[] ico = TrayIconRenderer.EncodeIco(text, size);
        int stride = ((size + 31) / 32) * 4;
        int maskStart = 62 + size * size * 4;
        Assert.Equal(1, BitConverter.ToUInt16(ico, 2));
        Assert.Equal(size, ico[6]);
        Assert.Equal(maskStart + stride * size, ico.Length);
        int lit = 0;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int row = size - 1 - y;
            byte alpha = ico[62 + (row * size + x) * 4 + 3];
            bool transparent = (ico[maskStart + row * stride + x / 8] & (0x80 >> (x % 8))) != 0;
            Assert.Equal(pixels[y,x] ? 255 : 0, alpha);
            Assert.Equal(!pixels[y,x], transparent);
            if (pixels[y,x]) lit++;
        }
        Assert.True(lit > 0);
        Assert.False(pixels[0,0]);
        using var icon = TrayIconRenderer.Create(text, size);
        using var bitmap = icon.ToBitmap();
        Assert.Equal(size, bitmap.Width);
        Assert.Equal(0, bitmap.GetPixel(0,0).A);
    }

    // Captured from the MemoryStream/BinaryWriter encoder at 7dc41c6; output must stay byte-identical.
    [Theory]
    [InlineData("0", 16, "0A7AD68D3B77296398500EF4A9D01CA4D2127A35EDA77804782884F7B4BF8C5F")]
    [InlineData("46", 16, "589E4BFE4A892F13615B8AAB64D1A590C2D20754307EC8E7A7BE136D2208A9C6")]
    [InlineData("403", 16, "2E4514B33488B8336209906A33348AE17085A1F140D1223CBF2D9FF07B538049")]
    [InlineData("9999", 16, "244D2561967468191D96A85B6D9351ED3B451154C8ECB399229890A3B8CE0016")]
    [InlineData("...", 16, "976FFC2FBF4E74591C12C2F60129E445BF154FA2F45E21E8AB3F1BE57968C1BC")]
    [InlineData("-", 16, "50288D5765296F994537906E448CFCA07B1B49316236D3281D77D2496FE288CB")]
    [InlineData("88", 20, "937C97F7E6F54FA2F8B6A0BA0E630E94E6C0B951FC47F133B06BD78570ED70E3")]
    [InlineData("999", 24, "5B02D23E180D3F996FBBD566BC0A9DE7F2B9C832B2D0F519CA41FC74DCE7B479")]
    [InlineData("8888", 32, "24302F57B23B92D55B4CB25C621F8C84E498EB6173F5F3A2FAE29C1F92356B8F")]
    [InlineData("46", 48, "CD821327B0B24574B25ECD4F67EA285B9F42BCAA2779E628C0C1ED5CCCC6E910")]
    [InlineData("46", 64, "2395CCF91F07936EC6204E98C140A6A18D5257CF8632EBB5252C92E8BB6D8D68")]
    public void EncodedIcoMatchesGoldenHash(string text, int size, string sha256) =>
        Assert.Equal(sha256, Convert.ToHexString(SHA256.HashData(TrayIconRenderer.EncodeIco(text, size))));

    [Fact]
    public void FourthDigitIsRenderedAndInvalidInputsAreRejected()
    {
        bool[,] pixels = TrayIconRenderer.RenderPixels("8888");
        for (int digit = 0; digit < 4; digit++)
        {
            int lit = 0;
            for (int y = 0; y < 16; y++)
            for (int x = digit * 4; x < digit * 4 + 3; x++)
                if (pixels[y,x]) lit++;
            Assert.True(lit > 0);
        }
        Assert.Throws<ArgumentException>(() => TrayIconRenderer.RenderPixels("12345"));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrayIconRenderer.RenderPixels("1", 8));
        Assert.Equal("9999", TrayIconRenderer.DisplayText(15000, false));
        Assert.Equal("-", TrayIconRenderer.DisplayText(15, false, false));
    }

    [DllImport("user32.dll")] private static extern int GetGuiResources(IntPtr process, int flags);
    [Fact]
    public void RepeatedIconCreationReleasesNativeResources()
    {
        using var process = Process.GetCurrentProcess();
        using (var warmup = TrayIconRenderer.Create("46")) { }
        int before = GetGuiResources(process.Handle, 0) + GetGuiResources(process.Handle, 1);
        for (int i = 0; i < 2000; i++)
        {
            using var icon = TrayIconRenderer.Create((i % 1000).ToString());
            _ = icon.Handle;
        }
        int after = GetGuiResources(process.Handle, 0) + GetGuiResources(process.Handle, 1);
        Assert.InRange(after - before, -10, 10);
    }
}
