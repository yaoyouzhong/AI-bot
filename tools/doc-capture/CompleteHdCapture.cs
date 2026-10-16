using System.Net;
namespace AIBotBridge;

// Public documentation only: real complete archives in the isolated test profile,
// read-only published release fixture; never uses accounts, devices or installers.
internal static class CompleteHdCapture
{
    internal static void Run(string output,string delivery)
    {
        foreach(string category in new[]{"Painting","Calligraphy"}) {
            var imported=GalleryHdPack.Install(Path.Combine(delivery,$"AI-bot-Gallery-HD-{category}-2026.10.16.zip"));
            if(imported.Works!=366)throw new InvalidDataException("Expected actual complete collection");
        }
        string releases=File.ReadAllText(Path.Combine(delivery,"update-releases.json"));
        var handler=new UpdateSelfTest.Handler{Reply=(_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(releases,System.Text.Encoding.UTF8,"application/json")})};
        using var service=new UpdateService(handler);
        using var form=new GalleryPackForm(service,_=>throw new InvalidOperationException("Documentation must not open links"));
        form.StartPosition=FormStartPosition.Manual;form.Location=new(-30000,-30000);form.ShowInTaskbar=false;
        Exception? failure=null;
        form.Shown+=async(_,_)=>{try {
            var deadline=DateTime.UtcNow.AddSeconds(30);
            var import=form.Controls.Find("gallery-import",true).OfType<Button>().Single();
            while(service.CheckedAt is null||!import.Enabled){if(DateTime.UtcNow>deadline)throw new TimeoutException();await Task.Delay(20);}
            using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new(Point.Empty,form.Size));
            image.Save(Path.Combine(output,"gallery-packs.png"));
        }catch(Exception ex){failure=ex;}finally{form.Close();}};
        Application.Run(form);if(failure is not null)throw failure;
        Console.WriteLine("COMPLETE_HD_MEDIA_OK actual 366+366 imports; native controls; offline public release metadata; no device writes");
    }
}
