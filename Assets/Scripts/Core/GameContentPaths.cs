using System;
using System.IO;
using UnityEngine;

namespace RhythmWare.Core
{
    [Serializable] public sealed class BundledContentManifest { public int version=1;public BundledContentFile[] files; }
    [Serializable] public sealed class BundledContentFile { public string path,sha256;public long bytes; }
    // Authored files stay in Assets; Windows players use the build's bundled copy.
    public static class GameContentPaths
    {
        public static string ContentRoot => Application.isEditor?Application.dataPath:Application.streamingAssetsPath;
        public static string AudioRoot => Path.Combine(ContentRoot,"Audio");
        public static string BundledChartsRoot => Path.Combine(ContentRoot,"Data","Charts","Custom");

        public static string ResolveAudio(string path,string chartDirectory=null)
        {
            if(string.IsNullOrWhiteSpace(path))return "";
            string normalized=path.Replace('\\','/');
            string relative=AudioRelative(normalized);
            if(relative!=null)return Path.GetFullPath(Path.Combine(AudioRoot,relative));
            return Path.GetFullPath(Path.IsPathRooted(normalized)?normalized:Path.Combine(chartDirectory??Application.persistentDataPath,normalized));
        }
        public static string PortableAudio(string path)
        {
            if(string.IsNullOrWhiteSpace(path))return "";
            string relative=AudioRelative(path.Replace('\\','/'));
            return relative==null?path:"Audio/"+relative;
        }
        static string AudioRelative(string path)
        {
            string relative=null;
            if(path.StartsWith("Audio/",StringComparison.OrdinalIgnoreCase))relative=path.Substring(6);
            else if(path.StartsWith("Assets/Audio/",StringComparison.OrdinalIgnoreCase))relative=path.Substring(13);
            else
            {
                foreach(string marker in new[]{"/StreamingAssets/Audio/","/Assets/Audio/"})
                {
                    int at=path.LastIndexOf(marker,StringComparison.OrdinalIgnoreCase);
                    if(at>=0){relative=path.Substring(at+marker.Length);break;}
                }
            }
            if(relative!=null)foreach(string part in relative.Split('/'))if(part=="..")return null;
            return relative;
        }
    }
}
