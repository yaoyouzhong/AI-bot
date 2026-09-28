using System.Buffers.Binary;

namespace AIBotBridge;

// Peak only: never retain audio. Map -60..0 dBFS to a compact display range.
internal static class Tab5AudioMeter
{
    internal static int Level(ReadOnlySpan<byte> bytes,int bits,bool floatingPoint=false)
    {
        int stride=bits/8;
        if(stride is <2 or >4 || bits%8!=0 || floatingPoint&&bits!=32)return 0;
        double peak=0;
        for(int i=0;i+stride<=bytes.Length;i+=stride) {
            double sample;
            if(floatingPoint)sample=BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes[i..]));
            else if(bits==16)sample=BinaryPrimitives.ReadInt16LittleEndian(bytes[i..])/32768d;
            else if(bits==24)sample=((bytes[i]|bytes[i+1]<<8|bytes[i+2]<<16)<<8>>8)/8388608d;
            else sample=BinaryPrimitives.ReadInt32LittleEndian(bytes[i..])/2147483648d;
            if(double.IsFinite(sample))peak=Math.Max(peak,Math.Abs(sample));
        }
        return peak<=0.001?0:(int)Math.Clamp(Math.Round((20*Math.Log10(peak)+60)*100/60),0,100);
    }
}
