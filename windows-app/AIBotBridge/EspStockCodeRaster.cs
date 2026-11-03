using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AIBotBridge;

// The same licensed coverage table and integer blending as StockCodeFont.h.
// It affects the Windows mirror only: no font resources or animations are sent.
internal static class EspStockCodeRaster
{
    internal const int Width=56,Height=16;
    private static readonly byte[] Glyphs=Load();
    private static byte[] Load(){
        using var stream=typeof(EspStockCodeRaster).Assembly.GetManifestResourceStream("AIBotBridge.StockCodeGlyphs")
            ??throw new InvalidDataException("ESP stock glyph resource missing");
        using var reader=new StreamReader(stream);
        var source=reader.ReadToEnd();int start=source.IndexOf("stockCodeGlyphs[95][64]",StringComparison.Ordinal);
        if(start<0)throw new InvalidDataException("ESP stock glyph table invalid");
        var bytes=Regex.Matches(source[start..],@"0x([0-9a-fA-F]{2})\b")
            .Select(m=>byte.Parse(m.Groups[1].Value,NumberStyles.HexNumber,CultureInfo.InvariantCulture)).ToArray();
        if(bytes.Length!=95*64)throw new InvalidDataException("ESP stock glyph table size invalid");
        return bytes;
    }
    internal static ushort[] Raster(string text){
        var result=new ushort[Width*Height];var bytes=Encoding.UTF8.GetBytes(text);
        bool truncated=bytes.Length>8;int shown=Math.Min(bytes.Length,8),advance=shown<=7?8:7;
        int Coverage(int c,int x,int y){int offset=y*8+x;byte packed=Glyphs[(c-32)*64+offset/2];return offset%2==0?packed>>4:packed&15;}
        for(int i=0;i<shown;i++){
            int c=bytes[i] is >=32 and <=126?bytes[i]:'?';
            for(int y=0;y<Height;y++)for(int x=0;x<advance;x++){
                int coverage;
                if(truncated&&i==7)coverage=y==12&&x is 1 or 3 or 5?15:0;
                else if(advance==8)coverage=Coverage(c,x,y);
                else{
                    int start=x*8,end=start+8,first=start/7,last=(end-1)/7;
                    int weight=Math.Min((first+1)*7,end)-start;
                    coverage=(Coverage(c,first,y)*weight+(last==first?0:Coverage(c,last,y)*(8-weight))+4)/8;
                }
                const int color=0xCE79;
                result[y*Width+i*advance+x]=(ushort)(((((color>>11)&31)*coverage+7)/15<<11)|
                    ((((color>>5)&63)*coverage+7)/15<<5)|((color&31)*coverage+7)/15);
            }
        }
        return result;
    }
    internal static void Draw(Graphics graphics,string text,int x,int y){
        var pixels=Raster(text);
        using var bitmap=new Bitmap(Width,Height);
        for(int row=0;row<Height;row++)for(int column=0;column<Width;column++){
            int rgb=pixels[row*Width+column];
            bitmap.SetPixel(column,row,Color.FromArgb(((rgb>>11)&31)*255/31,((rgb>>5)&63)*255/63,(rgb&31)*255/31));
        }
        graphics.DrawImageUnscaled(bitmap,x,y);
    }
}
