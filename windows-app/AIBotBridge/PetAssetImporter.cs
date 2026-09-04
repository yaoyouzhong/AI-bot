using System.Drawing.Drawing2D;

namespace AIBotBridge;

internal static class PetAssetImporter
{
    internal const int Width = 112;
    internal const int Height = 112;

    internal static bool TryLoad(string path, out byte[] data, out string licenseFile, out string error)
    {
        data = Array.Empty<byte>();
        licenseFile = string.Empty;
        error = string.Empty;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length is <= 0 or > 20 * 1024 * 1024)
            {
                error = "图片不存在、为空或超过 20 MB。";
                return false;
            }
            var directory = info.DirectoryName ?? string.Empty;
            var candidates = new[]
            {
                Path.Combine(directory, "LICENSE"),
                Path.Combine(directory, "LICENSE.txt"),
                Path.Combine(directory, Path.GetFileNameWithoutExtension(path) + ".license.txt")
            };
            licenseFile = candidates.FirstOrDefault(candidate =>
            {
                try
                {
                    var license = new FileInfo(candidate);
                    return license.Exists && license.Length is > 0 and <= 64 * 1024;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return false;
                }
            }) ?? string.Empty;
            if (licenseFile.Length == 0)
            {
                error = "同目录缺少 LICENSE、LICENSE.txt 或同名 .license.txt，无法确认素材可再分发。";
                return false;
            }

            using var source = Image.FromFile(path);
            if (source.Width <= 0 || source.Height <= 0 || source.Width > 4096 || source.Height > 4096)
            {
                error = "图片尺寸必须在 1×1 到 4096×4096 之间。";
                return false;
            }
            using var target = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(target);
            graphics.Clear(Color.Black);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var scale = Math.Min((double)Width / source.Width, (double)Height / source.Height);
            var width = (float)(source.Width * scale);
            var height = (float)(source.Height * scale);
            graphics.DrawImage(source, (Width - width) / 2, (Height - height) / 2, width, height);
            data = EncodeRgb565(target);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            error = "无法读取图片：" + ex.Message;
            return false;
        }
    }

    internal static byte[] EncodeRgb565(Bitmap bitmap)
    {
        var result = new byte[bitmap.Width * bitmap.Height * 2];
        var offset = 0;
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var color = bitmap.GetPixel(x, y);
            var value = (ushort)(((color.R & 0xF8) << 8) | ((color.G & 0xFC) << 3) | (color.B >> 3));
            result[offset++] = (byte)value;
            result[offset++] = (byte)(value >> 8);
        }
        return result;
    }
}
