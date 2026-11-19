#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RhythmWare.EditorTools
{
    public static class CampaignAudioAnalysis
    {
        public static void Run()
        {
            string root=Directory.GetParent(Application.dataPath).FullName;
            string output=Path.GetFullPath(Path.Combine(root,"..",".qa","campaign-audio"));Directory.CreateDirectory(output);
            string[] names={"Sara","Jack-the-Ripper sasakure.UK","femtanyl - DINNER!"};
            try
            {
                foreach(string name in names)
                {
                    var clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Levels/"+name+".mp3");
                    if(clip==null)throw new Exception("Missing audio: "+name);clip.LoadAudioData();
                    var samples=new float[clip.samples*clip.channels];if(!clip.GetData(samples,0))throw new Exception("PCM unavailable: "+name);
                    using(var writer=new BinaryWriter(File.Create(Path.Combine(output,name+".f32"))))
                        for(int frame=0;frame<clip.samples;frame+=4)
                        {
                            float sum=0;int count=0;
                            for(int j=0;j<4&&frame+j<clip.samples;j++)for(int channel=0;channel<clip.channels;channel++){sum+=samples[(frame+j)*clip.channels+channel];count++;}
                            writer.Write(sum/count);
                        }
                    File.WriteAllText(Path.Combine(output,name+".json"),"{\"sampleRate\":"+(clip.frequency/4.0).ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"duration\":"+clip.length.ToString(System.Globalization.CultureInfo.InvariantCulture)+"}");
                }
                File.WriteAllText(Path.Combine(output,"result.txt"),"PASS");EditorApplication.Exit(0);
            }
            catch(Exception ex){File.WriteAllText(Path.Combine(output,"result.txt"),ex.ToString());EditorApplication.Exit(1);}
        }
    }
}
#endif
