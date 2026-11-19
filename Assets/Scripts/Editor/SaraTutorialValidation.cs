#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace RhythmWare.EditorTools
{
    public static class SaraTutorialValidation
    {
        static UnityWebRequest request;static AudioClip imported;static double deadline;
        static string Output=>Path.Combine(Directory.GetParent(Application.dataPath).FullName,"docs/sara-complete-validation");
        public static void ExportAudio()
        {
            Directory.CreateDirectory(Output);
            imported=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Levels/Sara.mp3");imported.LoadAudioData();
            request=UnityWebRequestMultimedia.GetAudioClip(new Uri(Path.Combine(Application.dataPath,"Audio/Music/Levels/Sara.mp3")).AbsoluteUri,AudioType.MPEG);request.SendWebRequest();
            deadline=EditorApplication.timeSinceStartup+120;EditorApplication.update+=Probe;
        }
        static void Probe()
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Sara PCM export timeout");
                if(!request.isDone||imported.loadState!=AudioDataLoadState.Loaded)return;
                if(request.result!=UnityWebRequest.Result.Success)throw new Exception(request.error);
                var disk=DownloadHandlerAudioClip.GetContent(request);var info=new List<string>();
                foreach(var entry in new[]{("imported",imported),("disk",disk)})
                {
                    var clip=entry.Item2;var pcm=new float[clip.samples*clip.channels];if(!clip.GetData(pcm,0))throw new Exception("No PCM: "+entry.Item1);
                    using(var writer=new BinaryWriter(File.Create(Path.Combine(Output,entry.Item1+".f32"))))
                        for(int i=0;i<clip.samples;i+=4){float sum=0;int count=0;for(int f=i;f<Math.Min(i+4,clip.samples);f++)for(int c=0;c<clip.channels;c++){sum+=pcm[f*clip.channels+c];count++;}writer.Write(sum/count);}
                    info.Add(entry.Item1+"="+clip.frequency+","+clip.channels+","+clip.samples+","+clip.length.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                File.WriteAllLines(Path.Combine(Output,"audio-info.txt"),info);EditorApplication.update-=Probe;request.Dispose();EditorApplication.Exit(0);
            }
            catch(Exception ex){File.WriteAllText(Path.Combine(Output,"probe-error.txt"),ex.ToString());EditorApplication.update-=Probe;EditorApplication.Exit(1);}
        }
    }
}
#endif
