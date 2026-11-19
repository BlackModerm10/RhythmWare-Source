using UnityEditor;
using UnityEngine;
using RhythmWare.Audio;

namespace RhythmWare.EditorTools
{
    public static class GenerateTestMusicWav
    {
        [MenuItem("RhythmWare/Generar Cancion de Prueba WAV (120 BPM)")]
        public static void GenerateAudioWavFileMenuItem()
        {
            string path = AudioWavGenerator.GenerateAudioWavFile("Assets/Audio/Music/CyberDefenseSong.wav");
            AssetDatabase.Refresh();
        }
    }
}
