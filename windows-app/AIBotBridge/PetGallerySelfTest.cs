namespace AIBotBridge;

internal static class PetGallerySelfTest
{
    internal static async Task Run(bool live)
    {
        var pixels = new byte[1536 * 1872 * 4];
        for (int y=0;y<1872;y++) for (int x=0;x<1536;x++)
        {
            int index=(y*1536+x)*4;
            pixels[index+2]=(byte)(x/192*30); pixels[index+1]=(byte)(y/208*30); pixels[index+3]=255;
        }
        var sheet = new GallerySheet(1536,1872,pixels);
        foreach(var owner in new[]{"claude","codex"}) foreach(var motion in PetGalleryService.Motions)
        {
            var animation=PetGalleryService.Animate(sheet,motion,owner);
            if(animation.Frames.Length!=motion.Frames || animation.Width!=(owner=="claude"?111:120) || animation.Height!=120)
                throw new Exception("Gallery role dimensions/frame count incorrect.");
            using var frame=animation.BitmapAt(0);
            if(Math.Abs(frame.GetPixel(animation.Width/2,60).G-motion.Row*30)>4)
                throw new Exception("Wrong gallery animation row.");
        }
        if(PetGalleryService.Allowed(new Uri("https://assets.petdex.dev.evil.invalid/sprite.webp")) || PetGalleryService.Allowed(new Uri("http://assets.petdex.dev/sprite.webp")))
            throw new Exception("Gallery URL boundary failed.");
        Directory.CreateDirectory("artifacts");
        using(var form=new PetGalleryForm(_=>{},false))
        {
            form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-20000,-20000);
            form.Show(); Application.DoEvents();
            using var bitmap=new Bitmap(form.Width,form.Height);
            form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));
            bitmap.Save(Path.Combine("artifacts","pet-gallery-layout.png")); form.Close();
        }
        Console.WriteLine("PET_GALLERY_SELF_TEST_OK 9 motions/2 roles/pixel rows/URL boundary/offline form");
        if(!live)return;
        // Offline form inspection installs a UI context, but this CLI has no message loop.
        SynchronizationContext.SetSynchronizationContext(null);
        using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var list=await PetGalleryService.Load(cancel.Token);
        var pet=list.Pets.FirstOrDefault(p=>p.Slug=="boba") ?? list.Pets[0];
        var real=await PetGalleryService.LoadSheet(pet,cancel.Token);
        var actual=PetGalleryService.Animate(real,PetGalleryService.Motions[7],"codex");
        using var preview=actual.BitmapAt(0);
        preview.Save(Path.Combine("artifacts","pet-gallery-live.png"));
        Console.WriteLine($"PET_GALLERY_LIVE_OK count={list.Pets.Length} cached={list.Cached} pet={pet.Slug} frames={actual.Frames.Length} size={actual.Width}x{actual.Height}; no selection or USB changed");
    }
}
