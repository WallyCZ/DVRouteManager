using CommandTerminal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DVRouteManager
{
    public class AudioUtils
    {

        public static AudioClip LoadAudioClip(string fileName, string clipName)
        {
            try
            {
                string path = Path.Combine(Path.GetDirectoryName(typeof(AudioUtils).Assembly.Location), fileName);

                WAV wav = new WAV(path);

                AudioClip audioClip = AudioClip.Create(clipName, wav.SampleCount, 1, wav.Frequency, false);

                audioClip.SetData(wav.LeftChannel, 0);

                return audioClip;
            }
            catch(Exception exc)
            {
                Terminal.Log(exc.Message);
            }

            return null;
        }

        public class WAV
        {

            // convert two bytes to one float in the range -1 to 1
            static float bytesToFloat(byte firstByte, byte secondByte)
            {
                // convert two bytes to one short (little endian)
                short s = (short)((secondByte << 8) | firstByte);
                // convert to range from -1 to (just below) 1
                return s / 32768.0F;
            }

            static int bytesToInt(byte[] bytes, int offset = 0)
            {
                int value = 0;
                for (int i = 0; i < 4; i++)
                {
                    value |= ((int)bytes[offset + i]) << (i * 8);
                }
                return value;
            }

            private static byte[] GetBytes(string filename)
            {
                return File.ReadAllBytes(filename);
            }
            // properties
            public float[] LeftChannel { get; internal set; }
            public float[] RightChannel { get; internal set; }
            public int ChannelCount { get; internal set; }
            public int SampleCount { get; internal set; }
            public int Frequency { get; internal set; }

            // Returns left and right double arrays. 'right' will be null if sound is mono.
            public WAV(string filename) :
                this(GetBytes(filename))
            { }

            public WAV(byte[] wav)
            {

                // Determine if mono or stereo
                ChannelCount = wav[22];     // Forget byte 23 as 99.999% of WAVs are 1 or 2 channels

                // Get the frequency
                Frequency = bytesToInt(wav, 24);

                // Get past all the other sub chunks to get to the data subchunk:
                int pos = 12;   // First Subchunk ID from 12 to 16

                // Keep iterating until we find the data chunk (i.e. 64 61 74 61 ...... (i.e. 100 97 116 97 in decimal))
                while (!(wav[pos] == 100 && wav[pos + 1] == 97 && wav[pos + 2] == 116 && wav[pos + 3] == 97))
                {
                    pos += 4;
                    int chunkSize = wav[pos] + wav[pos + 1] * 256 + wav[pos + 2] * 65536 + wav[pos + 3] * 16777216;
                    pos += 4 + chunkSize;
                }
                pos += 8;

                // Pos is now positioned to start of actual sound data.
                SampleCount = (wav.Length - pos) / 2;     // 2 bytes per sample (16 bit sound mono)
                if (ChannelCount == 2) SampleCount /= 2;        // 4 bytes per sample (16 bit stereo)

                // Allocate memory (right will be null if only mono sound)
                LeftChannel = new float[SampleCount];
                if (ChannelCount == 2) RightChannel = new float[SampleCount];
                else RightChannel = null;

                // Write to double array/s:
                int i = 0;
                while (pos < wav.Length)
                {
                    LeftChannel[i] = bytesToFloat(wav[pos], wav[pos + 1]);
                    pos += 2;
                    if (ChannelCount == 2)
                    {
                        RightChannel[i] = bytesToFloat(wav[pos], wav[pos + 1]);
                        pos += 2;
                    }
                    i++;
                }
            }

            public override string ToString()
            {
                return string.Format("[WAV: LeftChannel={0}, RightChannel={1}, ChannelCount={2}, SampleCount={3}, Frequency={4}]", LeftChannel, RightChannel, ChannelCount, SampleCount, Frequency);
            }
        }
    }
}
