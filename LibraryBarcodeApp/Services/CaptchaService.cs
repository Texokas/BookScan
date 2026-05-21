using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace LibraryBarcodeApp.Services;

public sealed class CaptchaService
{
    private readonly Random _random = new();

    public string GenerateCaptcha()
    {
        // Avoid ambiguous symbols to reduce false mismatches.
        const string chars = "23456789ABCDEFGHJKMNPQRTUVWXY";
        var length = _random.Next(4, 7); // 4..6

        var sb = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            sb.Append(chars[_random.Next(chars.Length)]);
        }

        return sb.ToString();
    }

    public byte[] GenerateCaptchaImage(string code)
    {
        const int width = 200;
        const int height = 60;

        using var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var bg = RandomLightColor();
        using (var bgBrush = new SolidBrush(bg))
        {
            g.FillRectangle(bgBrush, 0, 0, width, height);
        }

        // Noise points
        for (var i = 0; i < 450; i++)
        {
            var x = _random.Next(width);
            var y = _random.Next(height);
            var c = RandomMidColor();
            bmp.SetPixel(x, y, c);
        }

        // Distortion lines
        for (var i = 0; i < 8; i++)
        {
            using var pen = new Pen(RandomMidColor(), _random.Next(1, 3));
            pen.DashStyle = _random.NextDouble() > 0.6 ? DashStyle.Dash : DashStyle.Solid;
            g.DrawBezier(
                pen,
                new Point(_random.Next(0, width / 3), _random.Next(height)),
                new Point(_random.Next(width / 3, width * 2 / 3), _random.Next(height)),
                new Point(_random.Next(width / 3, width * 2 / 3), _random.Next(height)),
                new Point(_random.Next(width * 2 / 3, width), _random.Next(height)));
        }

        // Draw characters with random rotation
        var charCount = Math.Max(1, code.Length);
        var cellWidth = width / charCount;

        using var font = new Font("Segoe UI", 28, FontStyle.Bold, GraphicsUnit.Pixel);

        for (var i = 0; i < code.Length; i++)
        {
            var ch = code[i].ToString();
            var angle = _random.Next(-25, 26);

            var x = i * cellWidth + _random.Next(6, 14);
            var y = _random.Next(6, 18);

            using var brush = new SolidBrush(RandomDarkColor());

            var state = g.Save();
            g.TranslateTransform(x + 10, y + 16);
            g.RotateTransform(angle);
            g.DrawString(ch, font, brush, -10, -16);
            g.Restore(state);
        }

        // Final overlay (subtle)
        using (var pen = new Pen(Color.FromArgb(120, RandomMidColor()), 1))
        {
            g.DrawRectangle(pen, 0, 0, width - 1, height - 1);
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    public bool ValidateCaptcha(string inputCode, string generatedCode)
    {
        var input = NormalizeCaptcha(inputCode);
        var generated = NormalizeCaptcha(generatedCode);
        return string.Equals(input, generated, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCaptcha(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var upper = input.Trim().ToUpperInvariant();
        var sb = new StringBuilder(upper.Length);
        foreach (var ch in upper)
        {
            sb.Append(ch switch
            {
                // Normalize look-alike Cyrillic letters to Latin.
                'А' => 'A',
                'В' => 'B',
                'С' => 'C',
                'Е' => 'E',
                'Н' => 'H',
                'К' => 'K',
                'М' => 'M',
                'О' => 'O',
                'Р' => 'P',
                'Т' => 'T',
                'У' => 'Y',
                'Х' => 'X',
                _ => ch
            });
        }

        return sb.ToString();
    }

    private Color RandomLightColor() =>
        Color.FromArgb(255,
            _random.Next(210, 256),
            _random.Next(210, 256),
            _random.Next(210, 256));

    private Color RandomMidColor() =>
        Color.FromArgb(255,
            _random.Next(80, 200),
            _random.Next(80, 200),
            _random.Next(80, 200));

    private Color RandomDarkColor() =>
        Color.FromArgb(255,
            _random.Next(20, 120),
            _random.Next(20, 120),
            _random.Next(20, 120));
}

