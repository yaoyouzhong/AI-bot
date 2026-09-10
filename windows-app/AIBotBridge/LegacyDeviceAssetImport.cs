namespace AIBotBridge;

internal static class LegacyDeviceAssetImport
{
    internal static void Run(string directory,bool import=true)
    {
        var data=File.ReadAllBytes(Path.Combine(directory,"x.bin"));
        int count=data[0],size=120*120*2;
        if(count is <1 or >8 || data.Length!=1+count*size)throw new InvalidDataException("Unexpected legacy custom Codex sprite");
        var frames=new byte[count][];
        for(int f=0;f<count;f++) {
            frames[f]=new byte[size];
            for(int i=0;i<size;i+=2){frames[f][i]=data[1+f*size+i+1];frames[f][i+1]=data[1+f*size+i];}
        }
        var pet=new PetAnimation(Enumerable.Repeat((ushort)120,count).ToArray(),frames,120,120);
        using(var preview=pet.BitmapAt(0))preview.Save(Path.Combine(directory,"codex-restored-preview.png"));
        if(import)PetAnimationStore.Shared.Select("codex",pet);
        if(!PetAnimationStore.Shared.Selection("codex")!.Encode().SequenceEqual(pet.Encode()))throw new InvalidDataException("Selected pet differs from device backup");
        Console.WriteLine($"DEVICE_CUSTOM_CODEX_{(import?"IMPORTED":"VERIFIED")} frames={count} size=120x120 delay=120ms; source x.bin, default retained");
        var packed=File.ReadAllBytes(Path.Combine(directory,"stock-ui.rle"));
        var raw=new List<byte>(124800);int offset=0;
        while(offset<packed.Length) {
            byte h=packed[offset++];int run=(h&127)+1;
            if((h&128)!=0){byte hi=packed[offset++],lo=packed[offset++];for(int i=0;i<run;i++){raw.Add(lo);raw.Add(hi);}}
            else for(int i=0;i<run;i++){byte hi=packed[offset++],lo=packed[offset++];raw.Add(lo);raw.Add(hi);}
            if(raw.Count>124800)throw new InvalidDataException("Oversized stock cache");
        }
        if(raw.Count!=124800)throw new InvalidDataException("Incomplete stock cache");
        var old=raw.ToArray();
        using var bitmap=new Bitmap(156,400);
        for(int y=0;y<400;y++)for(int x=0;x<156;x++){int i=(y*156+x)*2,v=old[i]|old[i+1]<<8;bitmap.SetPixel(x,y,Color.FromArgb(((v>>11)&31)*255/31,((v>>5)&63)*255/63,(v&31)*255/31));}
        bitmap.Save(Path.Combine(directory,"stock-original-preview.png"));
        using var runtime=new BridgeRuntime(false);
        var names=runtime.Capture().Stocks?.Quotes.Select(q=>q.Name).ToArray()??[];
        var current=LocalizedTextResources.RenderStockNames(names);
        int diff=Enumerable.Range(0,62400).Count(i=>old[i*2]!=current[i*2]||old[i*2+1]!=current[i*2+1]);
        Console.WriteLine($"BACKUP_STOCK_PIXEL_DIFF {diff}/62400 names={names.Length}");
        var background=Task.Run(()=>LocalizedTextResources.RenderStockNames(names)).GetAwaiter().GetResult();
        int backgroundDiff=Enumerable.Range(0,62400).Count(i=>old[i*2]!=background[i*2]||old[i*2+1]!=background[i*2+1]);
        Console.WriteLine($"BACKGROUND_STOCK_PIXEL_DIFF {backgroundDiff}/62400");
        if(diff!=0||backgroundDiff!=0)throw new InvalidDataException("Stock rendering differs from actual legacy device cache");
    }
}
