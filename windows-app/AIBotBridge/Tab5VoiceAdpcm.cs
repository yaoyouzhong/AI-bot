using System.Buffers.Binary;

namespace AIBotBridge;

internal static class Tab5VoiceAdpcm
{
    internal static byte[] Decode8k(byte[] block) {
        var low=Decode(block);
        if(low.Length>3200)throw new ArgumentException("蓝牙音频分片过长");
        var pcm=new byte[low.Length*2];
        for(int i=0;i<low.Length/2;i++) {
            short sample=BinaryPrimitives.ReadInt16LittleEndian(low.AsSpan(i*2));
            short next=BinaryPrimitives.ReadInt16LittleEndian(low.AsSpan(Math.Min(i+1,low.Length/2-1)*2));
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i*4),sample);
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i*4+2),(short)(((int)sample+next)/2));
        }
        return pcm;
    }
    private static readonly int[] Steps=[7,8,9,10,11,12,13,14,16,17,19,21,23,25,28,31,34,37,41,45,50,55,60,66,73,80,88,97,107,118,130,143,157,173,190,209,230,253,279,307,337,371,408,449,494,544,598,658,724,796,876,963,1060,1166,1282,1411,1552,1707,1878,2066,2272,2499,2749,3024,3327,3660,4026,4428,4871,5358,5894,6484,7132,7845,8630,9493,10442,11487,12635,13899,15289,16818,18500,20350,22385,24623,27086,29794,32767];
    private static readonly int[] Adjust=[-1,-1,-1,-1,2,4,6,8];
    internal static byte[] Decode(byte[] block)
    {
        if(block.Length<6)throw new ArgumentException("蓝牙音频分片不完整");
        int count=BinaryPrimitives.ReadUInt16LittleEndian(block),predictor=BinaryPrimitives.ReadInt16LittleEndian(block.AsSpan(2)),index=block[4];
        if(count is <1 or >3200||block.Length!=6+count/2||index>88||block[5]!=0)throw new ArgumentException("蓝牙音频分片无效");
        var pcm=new byte[count*2];BinaryPrimitives.WriteInt16LittleEndian(pcm,(short)predictor);
        for(int i=1;i<count;i++) {
            int code=(block[6+(i-1)/2]>>((i-1)%2*4))&15,step=Steps[index];
            int delta=(step>>3)+((code&4)!=0?step:0)+((code&2)!=0?step>>1:0)+((code&1)!=0?step>>2:0);
            predictor=Math.Clamp(predictor+((code&8)!=0?-delta:delta),-32768,32767);
            index=Math.Clamp(index+Adjust[code&7],0,88);
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i*2),(short)predictor);
        }
        return pcm;
    }
}
