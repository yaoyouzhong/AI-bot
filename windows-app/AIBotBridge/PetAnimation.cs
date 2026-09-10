using System.Buffers.Binary;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace AIBotBridge;

internal sealed record PetAnimation(ushort[] Delays, byte[][] Frames, int Width = 112, int Height = 112)
{
    internal const int FrameBytes = 112 * 112 * 2;
    private int PixelBytes => Width * Height * 2;
    internal byte[] Encode()
    {
        if (Width is < 1 or > 120 || Height is < 1 or > 120 || Frames.Length is < 1 or > 8 || Delays.Length != Frames.Length || Frames.Any(f => f.Length != PixelBytes) || Delays.Any(d => d is < 20 or > 60000))
            throw new InvalidDataException("Invalid pet animation frames/durations.");
        var data = new byte[12 + 2 * Frames.Length + PixelBytes * Frames.Length];
        "APET"u8.CopyTo(data); data[4] = (byte)(Width == 112 && Height == 112 ? 1 : 2); data[5] = (byte)Frames.Length;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6), (ushort)Width);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(8), (ushort)Height);
        for (int i = 0; i < Frames.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(12 + 2 * i), Delays[i]);
            Frames[i].CopyTo(data, 12 + 2 * Frames.Length + PixelBytes * i);
        }
        return data;
    }
    internal static PetAnimation Decode(byte[] data)
    {
        if (data.Length < 14 || !data.AsSpan(0, 4).SequenceEqual("APET"u8) || data[4] is not (1 or 2) || data[5] is < 1 or > 8 || data[10] != 0 || data[11] != 0)
            throw new InvalidDataException("Invalid APET resource.");
        int width = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(6)), height = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(8));
        int frameBytes = width * height * 2;
        if (width is < 1 or > 120 || height is < 1 or > 120 || (data[4] == 1 && (width != 112 || height != 112)) || data.Length != 12 + 2 * data[5] + frameBytes * data[5])
            throw new InvalidDataException("Invalid APET dimensions.");
        var delays = new ushort[data[5]]; var frames = new byte[data[5]][];
        for (int i = 0; i < frames.Length; i++)
        {
            delays[i] = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(12 + 2 * i));
            if (delays[i] is < 20 or > 60000) throw new InvalidDataException("Invalid APET delay.");
            frames[i] = data.AsSpan(12 + 2 * frames.Length + i * frameBytes, frameBytes).ToArray();
        }
        return new(delays, frames, width, height);
    }
    internal int FrameAt(long milliseconds)
    {
        var total = Delays.Sum(d => (int)d);
        var phase = (milliseconds % total + total) % total;
        for (int i = 0; i < Delays.Length; i++) { if (phase < Delays[i]) return i; phase -= Delays[i]; }
        return 0;
    }
    internal Bitmap BitmapAt(long milliseconds)
    {
        var data = Frames[FrameAt(milliseconds)]; var bitmap = new Bitmap(Width, Height);
        for (int y = 0, offset = 0; y < Height; y++) for (int x = 0; x < Width; x++, offset += 2)
        {
            int value = data[offset] | data[offset + 1] << 8;
            bitmap.SetPixel(x, y, Color.FromArgb(((value >> 11) & 31) * 255 / 31, ((value >> 5) & 63) * 255 / 63, (value & 31) * 255 / 31));
        }
        return bitmap;
    }
    internal static bool TryImport(string path, out PetAnimation? animation, out string notice, out string error)
    {
        animation = null;
        if (!PetAssetImporter.TryLoad(path, out var first, out notice, out error)) return false;
        try
        {
            using var image = Image.FromFile(path);
            int count = image.FrameDimensionsList.Contains(FrameDimension.Time.Guid) ? image.GetFrameCount(FrameDimension.Time) : 1;
            if (count > 1000) throw new InvalidDataException("GIF frame count exceeds 1000.");
            if (count == 1) { animation = new([200], [first]); return true; }
            var rawDelay = image.PropertyIdList.Contains(0x5100) ? image.GetPropertyItem(0x5100)?.Value : null;
            int kept = Math.Min(count, 8); var frames = new byte[kept][]; var delays = new ushort[kept];
            for (int slot = 0; slot < kept; slot++)
            {
                int start = slot * count / kept, end = (slot + 1) * count / kept, duration = 0;
                for (int i = start; i < end; i++) duration += rawDelay is not null && rawDelay.Length >= (i + 1) * 4 ? (int)Math.Clamp((long)BitConverter.ToUInt32(rawDelay, i * 4) * 10, 20L, 60000L) : 100;
                delays[slot] = (ushort)Math.Clamp(duration, 20, 60000);
                image.SelectActiveFrame(FrameDimension.Time, start);
                using var bitmap = new Bitmap(112, 112);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.Black);
                    var scale = Math.Min(112f / image.Width, 112f / image.Height);
                    g.DrawImage(image, (112 - image.Width * scale) / 2, (112 - image.Height * scale) / 2, image.Width * scale, image.Height * scale);
                }
                frames[slot] = PetAssetImporter.EncodeRgb565(bitmap);
            }
            animation = new(delays, frames); return true;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or ExternalException)
        { error = "无法解析桌宠动画：" + ex.Message; return false; }
    }
}
