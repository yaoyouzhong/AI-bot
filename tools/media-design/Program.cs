using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;
using AIBotBridge;

// Offline media conversion only. No bridge, settings, credentials or device access.
if (args.Length is 2 or 3 && args[0] == "--verify-gif")
{
    if (!PetAnimation.TryImport(args[1], out var animation, out _, out var error))
        throw new InvalidDataException(error);
    var decoded = PetAnimation.Decode(animation!.Encode());
    var distinct = decoded.Frames.Select(f => Convert.ToHexString(SHA256.HashData(f))).Distinct().Count();
    if (decoded.Frames.Length != 6 || distinct < 3) throw new InvalidDataException("Expected six changing frames.");
    if (args.Length == 3)
    {
        if (Directory.Exists(args[2])) throw new IOException("Use a new native-frame output directory.");
        Directory.CreateDirectory(args[2]);
        long elapsed = 0;
        for (int i = 0; i < decoded.Frames.Length; i++)
        {
            using var bitmap = decoded.BitmapAt(elapsed);
            bitmap.Save(Path.Combine(args[2], $"frame-{i:D2}.png"), ImageFormat.Png);
            elapsed += decoded.Delays[i];
        }
    }
    Console.WriteLine(JsonSerializer.Serialize(new { decoded.Width, decoded.Height, frames = decoded.Frames.Length,
        distinct, durationMs = decoded.Delays.Sum(d => (int)d), importedByExistingProductCode = true }));
    return;
}
if (args.Length != 2) throw new ArgumentException("Supply a 3x2 transparent sprite sheet and a new output directory, or --verify-gif <gif>.");
using var sheet = new Bitmap(args[0]);
if (sheet.Width % 3 != 0 || sheet.Height % 2 != 0 || sheet.Width / 3 != sheet.Height / 2)
    throw new InvalidDataException("Expected a 3x2 grid of square cells.");
var output = Path.GetFullPath(args[1]);
if (Directory.Exists(output)) throw new IOException("Use a new output directory.");
Directory.CreateDirectory(output);
int cell = sheet.Width / 3;
var bounds = new Rectangle[6];
for (int i = 0; i < 6; i++)
{
    int left = cell, top = cell, right = -1, bottom = -1;
    for (int y = 0; y < cell; y++) for (int x = 0; x < cell; x++)
    {
        if (sheet.GetPixel(i % 3 * cell + x, i / 3 * cell + y).A < 128) continue;
        left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y);
    }
    if (right < left || bottom < top || bottom >= cell - 2) throw new InvalidDataException("Missing or clipped sprite.");
    bounds[i] = Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
}
// Same scale for every frame, with an aligned bottom baseline; no independent zoom.
float ratio = Math.Min(224f / bounds.Max(r => r.Width), 224f / bounds.Max(r => r.Height));
var pixels = new byte[6][];
for (int i = 0; i < 6; i++)
{
    using var frame = new Bitmap(256, 256, PixelFormat.Format32bppArgb);
    var b = bounds[i];
    using (var g = Graphics.FromImage(frame))
    {
        g.Clear(Color.Transparent); g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        var source = new Rectangle(b.X + i % 3 * cell, b.Y + i / 3 * cell, b.Width, b.Height);
        g.DrawImage(sheet, new RectangleF((256 - b.Width * ratio) / 2, 240 - b.Height * ratio,
            b.Width * ratio, b.Height * ratio), source, GraphicsUnit.Pixel);
    }
    frame.Save(Path.Combine(output, $"frame-{i:D2}.png"), ImageFormat.Png);
    using var small = new Bitmap(112, 112);
    using (var g = Graphics.FromImage(small))
    {
        g.Clear(Color.Black); g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.DrawImage(frame, 0, 0, 112, 112);
    }
    pixels[i] = PetAssetImporter.EncodeRgb565(small);
}
var pet = new PetAnimation(Enumerable.Repeat((ushort)400, 6).ToArray(), pixels);
File.WriteAllBytes(Path.Combine(output, "preview.apet"), pet.Encode());
File.WriteAllText(Path.Combine(output, "frame-layout.json"), JsonSerializer.Serialize(new { cell,
    bounds = bounds.Select(r => new { r.X, r.Y, r.Width, r.Height }), ratio,
    frames = 6, durationMs = 2400 }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("OFFLINE_SPRITE_CONVERSION_OK six aligned frames; product code unchanged");
