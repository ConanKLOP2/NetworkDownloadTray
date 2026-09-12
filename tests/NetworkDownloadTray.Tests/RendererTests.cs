using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
