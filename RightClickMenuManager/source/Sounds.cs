using System;
using System.IO;
using System.Media;

namespace RightClickMenuManager
{
    // Small UI sounds generated in memory, so the app needs no sound files.
    public static class Sounds
    {
        public static bool Enabled = true;

        const int Rate = 44100;
        static byte[] toggleOn, toggleOff, success, error, tap;

        static Sounds()
        {
            try
            {
                toggleOn = Wav(Tone(1650, 0.045, 0.22, 60));
                toggleOff = Wav(Tone(1150, 0.045, 0.22, 60));
                tap = Wav(Tone(2100, 0.025, 0.12, 90));
                success = Wav(Concat(Tone(880, 0.11, 0.22, 18, 0.25), Tone(1318.5, 0.32, 0.22, 9, 0.25)));
                error = Wav(Concat(Tone(311, 0.14, 0.26, 14, 0.15), Tone(233, 0.26, 0.26, 9, 0.15)));
            }
            catch { }
        }

        public static void ToggleOn() => Play(toggleOn);
        public static void ToggleOff() => Play(toggleOff);
        public static void Tap() => Play(tap);
        public static void Success() => Play(success);
        public static void Error() => Play(error);

        static void Play(byte[] wav)
        {
            if (!Enabled || wav == null) return;
            try
            {
                var player = new SoundPlayer(new MemoryStream(wav));
                player.Play();
            }
            catch { }
        }

        static float[] Tone(double freq, double seconds, double volume, double decay, double harmonic = 0.0)
        {
            int n = (int)(Rate * seconds);
            var s = new float[n];
            int attack = Math.Max(1, (int)(Rate * 0.004));
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Rate;
                double env = Math.Exp(-decay * t) * Math.Min(1.0, (double)i / attack);
                double release = Math.Min(1.0, (n - i) / (Rate * 0.01));
                double v = Math.Sin(2 * Math.PI * freq * t) + harmonic * Math.Sin(4 * Math.PI * freq * t);
                s[i] = (float)(v * env * release * volume / (1 + harmonic));
            }
            return s;
        }

        static float[] Concat(float[] a, float[] b)
        {
            var r = new float[a.Length + b.Length];
            a.CopyTo(r, 0);
            b.CopyTo(r, a.Length);
            return r;
        }

        static byte[] Wav(float[] samples)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            int dataLen = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataLen);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(Rate);
            w.Write(Rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(dataLen);
            foreach (var f in samples)
                w.Write((short)Math.Max(short.MinValue, Math.Min(short.MaxValue, f * 32767)));
            w.Flush();
            return ms.ToArray();
        }
    }
}
