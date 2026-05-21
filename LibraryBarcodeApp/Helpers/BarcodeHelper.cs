using System.Drawing;
using System.IO;
using ZXing.Windows.Compatibility;

namespace LibraryBarcodeApp.Helpers;

public static class BarcodeHelper
{
    public static string? DecodeBarcodeFromImage(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return null;
        }

        using var bitmap = (Bitmap)Image.FromFile(imagePath);
        var reader = new BarcodeReader { Options = { TryHarder = true } };
        var result = reader.Decode(bitmap);
        return result?.Text;
    }
}
