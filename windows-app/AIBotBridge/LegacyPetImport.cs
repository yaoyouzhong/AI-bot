using System.Globalization;
using System.Text.RegularExpressions;

namespace AIBotBridge;

// Reads user-selected local data only. No legacy source/assets are distributed.
internal static class LegacyPetImport
{
    internal static PetAnimation Read(string path, string owner)
    {
        if (owner is not ("claude" or "codex")) throw new ArgumentException("Invalid pet owner.");
        if (new FileInfo(path).Length > 4_000_000) throw new InvalidDataException("Sprite header too large.");
        var text = File.ReadAllText(path);
        int Dimension(string suffix) => int.Parse(Regex.Match(text,
            @"#define\s+" + owner.ToUpperInvariant() + "_SPRITE_" + suffix + @"\s+(\d+)").Groups[1].Value, CultureInfo.InvariantCulture);
        int width = Dimension("W"), height = Dimension("H"), count = Dimension("FRAMES");
        if (width is < 1 or > 120 || height is < 1 or > 120 || count is < 1 or > 8)
            throw new InvalidDataException("Unsupported sprite dimensions/count.");
        var frames = new byte[count][];
        for (int frame = 0; frame < count; frame++)
        {
            var match = Regex.Match(text, @"\b" + owner + "_sprite_" + frame + @"\[\d+\]\s+PROGMEM\s*=\s*\{([^}]+)\}");
            var words = match.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (!match.Success || words.Length != width * height) throw new InvalidDataException("Incomplete sprite frame.");
            var pixels = words.Select(w => w.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? ushort.Parse(w[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : ushort.Parse(w, CultureInfo.InvariantCulture)).ToArray();
            var target = new byte[width * height * 2];
            for (int index = 0; index < pixels.Length; index++)
            {
                var pixel = pixels[index];
                int offset = index * 2;
                // Legacy arrays are pre-swapped for TFT_eSPI swapBytes=false.
                // APET stores natural RGB565 little-endian, so undo that swap.
                target[offset] = (byte)(pixel >> 8); target[offset + 1] = (byte)pixel;
            }
            frames[frame] = target;
        }
        return new(Enumerable.Repeat((ushort)120, count).ToArray(), frames, width, height);
    }

    internal static void Run(string root)
    {
        // Validate both before writing either local cache.
        var pets = new[] { "claude", "codex" }.Select(owner => (Owner: owner,
            Animation: Read(Path.Combine(root, "firmware", "include", "img", owner + "_sprite.h"), owner))).ToArray();
        foreach (var pet in pets)
        {
            PetAnimationStore.Shared.SaveLegacy(pet.Owner, pet.Animation);
            Console.WriteLine($"LOCAL_PET_IMPORTED {pet.Owner} frames={pet.Animation.Frames.Length} size={pet.Animation.Width}x{pet.Animation.Height}");
        }
    }
    internal static void Audit(string root)
    {
        foreach(var owner in new[]{"claude","codex"})
        {
            var expected=Read(Path.Combine(root,"firmware","include","img",owner+"_sprite.h"),owner);
            var selected=PetAnimationStore.Shared.Selection(owner);
            if(selected is null || !expected.Encode().SequenceEqual(selected.Encode()))
                throw new InvalidDataException($"{owner}: current local animation differs from the legacy default; no data changed.");
            Console.WriteLine($"LEGACY_PET_PIXEL_AUDIT_OK owner={owner} size={expected.Width}x{expected.Height} frames={expected.Frames.Length}; every RGB565 pixel/delay matched, no device claim");
        }
    }
}
