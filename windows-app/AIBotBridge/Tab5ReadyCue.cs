using System.Buffers.Binary;
using NAudio.Dsp;

namespace AIBotBridge;

// The TAB5 plays one 880 Hz readiness cue beside its microphone. Recognize only
// a strongly concentrated copy of that known tone in the initial audio window;
// speech/noise mixed with it keeps the normal wait. PCM is always passed on
// unchanged, and no frequency exclusion applies after the first capture second.
internal static class Tab5ReadyCue
{
    internal static bool IsTone(ReadOnlySpan<byte> pcm)
    {
        int samples=pcm.Length/2;
        if(pcm.Length%2!=0||samples is <160 or >3200)return false;
        int length=1,power=0;while(length<samples){length<<=1;power++;}
        var spectrum=new Complex[length];
        for(int i=0;i<samples;i++)spectrum[i].X=BinaryPrimitives.ReadInt16LittleEndian(pcm[(i*2)..])/32768f;
        FastFourierTransform.FFT(true,power,spectrum);
        double total=0,tone=0,peak=0,peakHz=0;
        for(int i=1;i<=length/2;i++) {
            double energy=(double)spectrum[i].X*spectrum[i].X+(double)spectrum[i].Y*spectrum[i].Y;
            total+=energy;
            double hz=i*16000d/length;if(hz is >=790 and <=970)tone+=energy;
            if(energy>peak){peak=energy;peakHz=hz;}
        }
        return total>0&&Math.Abs(peakHz-880)<=12&&tone/total>=0.97;
    }
}
