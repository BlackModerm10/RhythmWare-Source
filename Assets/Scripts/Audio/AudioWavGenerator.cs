using System;
using System.IO;
using UnityEngine;

namespace RhythmWare.Audio
{
    public static class AudioWavGenerator
    {
        public static string GenerateAudioWavFile(string relativePath = "Assets/Audio/Music/CyberDefenseSong.wav")
        {
            string folderPath = Path.GetDirectoryName(relativePath);
            if (!string.IsNullOrEmpty(folderPath) && !Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            int sampleRate = 44100;
            double durationSec = 45.0; // 45 seconds of 120 BPM track
            int totalSamples = (int)(sampleRate * durationSec);
            short[] audioData = new short[totalSamples];

            double bpm = 120.0;
            double secPerBeat = 60.0 / bpm;

            // Synthesize a 120 BPM Cyberpunk Metronome / Beat loop
            for (int i = 0; i < totalSamples; i++)
            {
                double timeSec = (double)i / sampleRate;
                double beatPos = (timeSec % secPerBeat) / secPerBeat;
                int currentBeat = (int)(timeSec / secPerBeat);

                double sampleValue = 0.0;

                // 1. Kick drum on every beat (4/4 time)
                if (beatPos < 0.15)
                {
                    double kickFreq = 150.0 * Math.Exp(-beatPos * 25.0);
                    double kickEnv = Math.Exp(-beatPos * 15.0);
                    sampleValue += Math.Sin(2.0 * Math.PI * kickFreq * timeSec) * kickEnv * 0.7;
                }

                // 2. Snare drum on beats 2 and 4 (currentBeat % 2 == 1)
                if (currentBeat % 2 == 1 && beatPos < 0.2)
                {
                    double noise = (new System.Random(i).NextDouble() * 2.0 - 1.0);
                    double snareEnv = Math.Exp(-beatPos * 20.0);
                    sampleValue += noise * snareEnv * 0.4;
                }

                // 3. Cyber Bass synth line (Arpeggiated)
                double bassFreq = 110.0; // A2
                if (currentBeat % 8 >= 4) bassFreq = 130.81; // C3
                if (currentBeat % 8 >= 6) bassFreq = 98.0;   // G2

                double bassEnv = Math.Exp(-beatPos * 5.0);
                double sawtooth = 2.0 * ((timeSec * bassFreq) - Math.Floor(0.5 + timeSec * bassFreq));
                sampleValue += sawtooth * bassEnv * 0.25;

                // Clamp audio signal
                sampleValue = Math.Max(-1.0, Math.Min(1.0, sampleValue));
                audioData[i] = (short)(sampleValue * short.MaxValue);
            }

            // Write 16-bit PCM WAV File Header & Data
            using (FileStream fs = new FileStream(relativePath, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(fs))
            {
                int subchunk2Size = totalSamples * 2;
                int chunkSize = 36 + subchunk2Size;

                // RIFF header
                writer.Write(new char[] { 'R', 'I', 'F', 'F' });
                writer.Write(chunkSize);
                writer.Write(new char[] { 'W', 'A', 'V', 'E' });

                // fmt subchunk
                writer.Write(new char[] { 'f', 'm', 't', ' ' });
                writer.Write(16); // Subchunk1Size (16 for PCM)
                writer.Write((short)1); // AudioFormat (1 for PCM)
                writer.Write((short)1); // NumChannels (Mono)
                writer.Write(sampleRate); // SampleRate
                writer.Write(sampleRate * 2); // ByteRate
                writer.Write((short)2); // BlockAlign
                writer.Write((short)16); // BitsPerSample

                // data subchunk
                writer.Write(new char[] { 'd', 'a', 't', 'a' });
                writer.Write(subchunk2Size);

                for (int i = 0; i < totalSamples; i++)
                {
                    writer.Write(audioData[i]);
                }
            }

            Debug.Log($"[RhythmWare] ¡Se ha generado la canción WAV rítmica en {relativePath}!");
            return relativePath;
        }
    }
}
