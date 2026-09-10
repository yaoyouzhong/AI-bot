namespace AIBotBridge;

internal static class PetAnimationSelfTest
{
    internal static void Run()
    {
        using var red = new Bitmap(112, 112); using var blue = new Bitmap(112, 112);
        using (var g = Graphics.FromImage(red)) g.Clear(Color.Red);
        using (var g = Graphics.FromImage(blue)) g.Clear(Color.Blue);
        var animation = new PetAnimation([100, 300], [PetAssetImporter.EncodeRgb565(red), PetAssetImporter.EncodeRgb565(blue)]);
        var bytes = animation.Encode(); var decoded = PetAnimation.Decode(bytes);
        if (decoded.FrameAt(0) != 0 || decoded.FrameAt(99) != 0 || decoded.FrameAt(100) != 1 || decoded.FrameAt(399) != 1 || decoded.FrameAt(400) != 0)
            throw new InvalidOperationException("Animation duration/boundary/loop failed.");
        using var first = decoded.BitmapAt(0); using var second = decoded.BitmapAt(100);
        if (first.GetPixel(0, 0).R != 255 || second.GetPixel(0, 0).B != 255) throw new InvalidOperationException("RGB565 endian mismatch.");
        var chunks = BinaryResourceProtocol.CreateChunks(BinaryResourceKind.PetAnimation, bytes, 17);
        var rebuilt = chunks.SelectMany(chunk => BinaryResourceProtocol.DecodeWire(chunk.WireBytes).Payload).ToArray();
        if (!rebuilt.SequenceEqual(bytes)) throw new InvalidOperationException("Animation chunk roundtrip failed.");
        foreach (var invalid in new[] { bytes[..^1], bytes.Concat(new byte[] { 0 }).ToArray(), new byte[14] })
        {
            try { PetAnimation.Decode(invalid); throw new InvalidOperationException("Malformed animation accepted."); }
            catch (InvalidDataException) { }
        }
        Console.WriteLine($"PET_ANIMATION_SELF_TEST_OK frames=2 chunks={chunks.Count} timing/endian/CRC/invalid-length; no user cache or USB changed");
        foreach (var width in new[] { 111, 120 })
        {
            var original = new byte[width * 120 * 2];
            for (int i = 0; i < original.Length; i++) original[i] = (byte)(i * 17);
            var sized = new PetAnimation([120], [original], width, 120);
            var roundtrip = PetAnimation.Decode(sized.Encode());
            foreach (var kind in new[] { BinaryResourceKind.ClaudePetAnimation, BinaryResourceKind.CodexPetAnimation })
            {
                var packets = BinaryResourceProtocol.CreateChunks(kind, sized.Encode(), 29);
                var decodedPackets = packets.Select(p => BinaryResourceProtocol.DecodeWire(p.WireBytes)).ToArray();
                if (decodedPackets.Any(p => p.Kind != kind) || !decodedPackets.SelectMany(p => p.Payload).SequenceEqual(sized.Encode()))
                    throw new InvalidOperationException("Provider pet resource kind/payload changed in transit.");
            }
            using var rendered = roundtrip.BitmapAt(0);
            if (roundtrip.Width != width || roundtrip.Height != 120 || rendered.Width != width || rendered.Height != 120 || !roundtrip.Frames[0].SequenceEqual(original))
                throw new InvalidOperationException("Original-size pet pixels/dimensions changed.");
            var invalid = sized.Encode(); invalid[6] = 121;
            try { PetAnimation.Decode(invalid); throw new InvalidOperationException("Oversized frame accepted."); }
            catch (InvalidDataException) { }
        }
        Console.WriteLine("PET_ORIGINAL_SIZE_SELF_TEST_OK 111x120/120x120 pixel-preserving APET v2; v1 retained");
    }
}
