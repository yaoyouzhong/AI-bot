namespace AIBotBridge;

internal static class PetSelectionSelfTest
{
    internal static void Run()
    {
        var root = Path.Combine(Environment.CurrentDirectory, "artifacts", "pet-selection-" + Guid.NewGuid().ToString("N"));
        var store = new PetAnimationStore(root);
        var openedBeforeImport = new PetAnimationStore(root);
        var claude = new PetAnimation([120], [new byte[111 * 120 * 2]], 111, 120);
        var codex = new PetAnimation([120], [Enumerable.Repeat((byte)0xF8, 120 * 120 * 2).ToArray()], 120, 120);
        store.SaveLegacy("claude", claude); store.SaveLegacy("codex", codex);
        var recoveredPath = Path.Combine(root, "normal-profile");
        PetAnimationStore.RecoverRedirectedImports(root, recoveredPath);
        var recovered = new PetAnimationStore(recoveredPath);
        Equal(claude, recovered.Selection("claude")); Equal(codex, recovered.Selection("codex"));
        var chosen = new PetAnimation([200], [new byte[PetAnimation.FrameBytes]]);
        recovered.Select("codex", chosen);
        PetAnimationStore.RecoverRedirectedImports(root, recoveredPath);
        Equal(chosen, new PetAnimationStore(recoveredPath).Selection("codex"));
        if (!File.Exists(Path.Combine(root, "codex-legacy.apet"))) throw new InvalidOperationException("Recovery removed original import.");
        using (var bitmap = new Bitmap(240, 240))
        using (var graphics = Graphics.FromImage(bitmap))
            if (!openedBeforeImport.Draw(graphics, 64, 64, "codex")) throw new InvalidOperationException("Live mirror did not recover an imported pet.");
        Equal(claude, openedBeforeImport.Selection("claude")); Equal(codex, openedBeforeImport.Selection("codex"));
        var custom = new PetAnimation([240], [Enumerable.Repeat((byte)7, PetAnimation.FrameBytes).ToArray()]);
        store.Select("claude", custom);
        var restarted = new PetAnimationStore(root);
        Equal(custom, restarted.Selection("claude")); Equal(codex, restarted.Selection("codex"));
        var before = restarted.AllResources();
        restarted.RestoreDefault("claude");
        Equal(claude, restarted.Selection("claude")); Equal(codex, restarted.Selection("codex"));
        var after = new PetAnimationStore(root).AllResources();
        foreach (var kind in new[] { BinaryResourceKind.ClaudePetAnimation, BinaryResourceKind.CodexPetAnimation })
        {
            var old = before.Single(r => r.Kind == kind); var current = after.Single(r => r.Kind == kind);
            if ((old.Revision == current.Revision) != (kind == BinaryResourceKind.CodexPetAnimation))
                throw new InvalidOperationException("Restoring one pet changed the wrong resource revision.");
        }
        if (!File.Exists(Path.Combine(root, "claude-selected.apet.previous")))
            throw new InvalidOperationException("Previous selection was not preserved.");
        var missing = new PetAnimationStore(Path.Combine(root, "no-default"));
        missing.Select("codex", custom);
        try { missing.RestoreDefault("codex"); throw new Exception("Missing default silently accepted."); }
        catch (InvalidOperationException) { Equal(custom, missing.Selection("codex")); }
        Console.WriteLine("PET_SELECTION_SELF_TEST_OK independent slots/restart/restore/resource revisions/backup/missing-default; isolated artifacts only");
    }
    private static void Equal(PetAnimation expected, PetAnimation? actual)
    {
        if (actual is null || !expected.Encode().SequenceEqual(actual.Encode()))
            throw new InvalidOperationException("Pet selection mismatch.");
    }
}
