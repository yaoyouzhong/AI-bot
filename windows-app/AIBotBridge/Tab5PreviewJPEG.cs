using System.Runtime.InteropServices;

namespace AIBotBridge;

internal static class Tab5PreviewJPEG
{
    private static bool JpegDimensions(ReadOnlySpan<byte> data,int width,int height) {
        if(data.Length<4||data[0]!=255||data[1]!=216)return false;
        int at=2;
        while(at+3<data.Length) {
            if(data[at++]!=255)return false;
            while(at<data.Length&&data[at]==255)at++;
            if(at>=data.Length)return false;
            int marker=data[at++];if(marker is 0xD9 or 0xDA)return false;
            if(marker==1||marker is >=0xD0 and <=0xD7)continue;
            if(at+2>data.Length)return false;
            int size=(data[at]<<8)|data[at+1];if(size<2||size>data.Length-at)return false;
            if(marker is 0xC0 or 0xC1 or 0xC2)return size>=8&&((data[at+3]<<8)|data[at+4])==height&&((data[at+5]<<8)|data[at+6])==width;
            at+=size;
        }
        return false;
    }
    internal static byte[]? Decode(byte[] bytes,int width,int height) {
        if(!JpegDimensions(bytes,width,height)||width is <1 or >1280||height is <1 or >1280||width*height>1280*720||bytes.Length>524288)return null;
        try {
            using var stream=new MemoryStream(bytes,false);using var image=Image.FromStream(stream,true,true);
            if(image.RawFormat.Guid!=System.Drawing.Imaging.ImageFormat.Jpeg.Guid||image.Width!=width||image.Height!=height)return null;
            using var bitmap=new Bitmap(image);byte[] pixels=new byte[width*height*2];
            var locked=bitmap.LockBits(new(0,0,width,height),System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try {
                byte[] row=new byte[width*4];
                for(int y=0;y<height;y++) {
                    System.Runtime.InteropServices.Marshal.Copy(locked.Scan0+y*locked.Stride,row,0,row.Length);
                    for(int x=0;x<width;x++) {int v=((row[x*4+2]>>3)<<11)|((row[x*4+1]>>2)<<5)|(row[x*4]>>3),at=(y*width+x)*2;pixels[at]=(byte)v;pixels[at+1]=(byte)(v>>8);}
                }
            }finally{bitmap.UnlockBits(locked);}
            return pixels;
        }catch(ArgumentException){return null;}catch(ExternalException){return null;}catch(OutOfMemoryException){return null;}
    }
}
