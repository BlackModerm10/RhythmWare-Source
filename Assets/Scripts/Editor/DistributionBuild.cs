#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using RhythmWare.Core;
using RhythmWare.Rhythm;

namespace RhythmWare.EditorTools
{
    [InitializeOnLoad]
    public sealed class DistributionBuild : BuildPlayerProcessor
    {
        const string IconPath="Assets/Branding/RhythmWare-RW.png";
        static DistributionBuild() { EditorApplication.delayCall+=ConfigureIcon; }
        [MenuItem("RhythmWare/Build/Aplicar icono RW")]
        public static void ConfigureIcon()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(IconPath))return;
            var importer=AssetImporter.GetAtPath(IconPath) as TextureImporter;if(importer==null)return;
            if(importer.textureType!=TextureImporterType.Default||importer.mipmapEnabled||importer.textureCompression!=TextureImporterCompression.Uncompressed||importer.maxTextureSize!=1024||importer.npotScale!=TextureImporterNPOTScale.ToNearest)
            {
                importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=false;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=1024;importer.npotScale=TextureImporterNPOTScale.ToNearest;importer.SaveAndReimport();
            }
            var icon=AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);if(icon==null)throw new BuildFailedException("No se pudo importar el icono RW.");
            SetIcons(NamedBuildTarget.Unknown,IconKind.Any,icon);
            SetIcons(NamedBuildTarget.Standalone,IconKind.Application,icon);
            AssetDatabase.SaveAssets();
        }
        static void SetIcons(NamedBuildTarget target,IconKind kind,Texture2D icon)
        {
            int count=PlayerSettings.GetIconSizes(target,kind).Length;
            if(count==0&&target==NamedBuildTarget.Unknown)count=1;if(count==0)return;
            var current=PlayerSettings.GetIcons(target,kind);
            if(current.Length!=count||current.Any(t=>t!=icon))PlayerSettings.SetIcons(target,Enumerable.Repeat(icon,count).ToArray(),kind);
        }
        public override void PrepareForBuild(BuildPlayerContext context)
        {
            var online=Resources.Load<TextAsset>("OnlineSettings");
            var config=online!=null?JsonUtility.FromJson<OnlineConfiguration>(online.text):null;
            bool invalidKey=config!=null&&!string.IsNullOrEmpty(config.publishableKey)&&!new OnlineConfiguration{enabled=true,projectUrl="https://key-validation.invalid",publishableKey=config.publishableKey}.IsValid;
            if(invalidKey||config!=null&&config.enabled&&!config.IsValid)throw new BuildFailedException("OnlineSettings requiere HTTPS y una clave PUBLICABLE/anon. Nunca incluyas una clave secreta o service_role en el juego, aunque la conexión esté desactivada.");
            var included=new List<BundledContentFile>();
            string audio=Path.Combine(Application.dataPath,"Audio");
            foreach(string relative in CampaignCatalog.CreateLevels().Select(l=>GameContentPaths.PortableAudio(l.audioClipPath).Substring("Audio/".Length)).Concat(new[]{"SFX/UI/select.wav","SFX/UI/menu_hover.wav"}))
                if(!File.Exists(Path.Combine(audio,relative)))throw new BuildFailedException("Falta audio necesario: "+relative);
            if(!Directory.Exists(Path.Combine(audio,"Music","Menu"))||!Directory.GetFiles(Path.Combine(audio,"Music","Menu")).Any(IsAudio))throw new BuildFailedException("Falta la música del menú.");
            foreach(string file in Directory.GetFiles(audio,"*",SearchOption.AllDirectories).Where(f=>!f.EndsWith(".meta",StringComparison.OrdinalIgnoreCase)))
                Add(context,included,file,"Audio/"+Path.GetRelativePath(audio,file).Replace('\\','/'));
            string staging=Path.GetFullPath("Library/RhythmWareDistribution");Directory.CreateDirectory(staging);
            string charts=Path.Combine(Application.dataPath,"Data","Charts","Custom");
            if(Directory.Exists(charts))foreach(string file in Directory.GetFiles(charts,"*.json"))
            {
                var dto=JsonUtility.FromJson<ChartJsonDto>(File.ReadAllText(file));dto.audioClipPath=GameContentPaths.PortableAudio(dto.audioClipPath);
                string copy=Path.Combine(staging,Path.GetFileName(file));File.WriteAllText(copy,JsonUtility.ToJson(dto,true));
                Add(context,included,copy,"Data/Charts/Custom/"+Path.GetFileName(file));
            }
            string manifest=Path.Combine(staging,"content-manifest.json");
            File.WriteAllText(manifest,JsonUtility.ToJson(new BundledContentManifest{files=included.OrderBy(e=>e.path).ToArray()},true));
            context.AddAdditionalPathToStreamingAssets(manifest,"content-manifest.json");
            Debug.Log("[RhythmWare Build] Empaquetados "+included.Count+" archivos de audio/mapas en StreamingAssets.");
        }
        static bool IsAudio(string file)=>new[]{".mp3",".ogg",".wav"}.Contains(Path.GetExtension(file).ToLowerInvariant());
        static void Add(BuildPlayerContext context,List<BundledContentFile> files,string source,string destination)
        {
            context.AddAdditionalPathToStreamingAssets(source,destination);
            using(var sha=SHA256.Create())using(var stream=File.OpenRead(source))
                files.Add(new BundledContentFile{path=destination,bytes=stream.Length,sha256=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant()});
        }
        [MenuItem("RhythmWare/Build/Windows listo para compartir")]
        public static void ExportWindows()
        {
            ConfigureIcon();
            string root=Directory.GetParent(Application.dataPath).FullName;
            string output=Argument("-rwOutput")??Path.GetFullPath(Path.Combine(root,"..","Builds","RhythmWare-Windows"));
            Directory.CreateDirectory(output);
            var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
            if(scenes.Length==0)throw new BuildFailedException("No hay escenas habilitadas en el perfil de exportación.");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName=Path.Combine(output,"RhythmWare.exe"),target=BuildTarget.StandaloneWindows64,subtarget=(int)StandaloneBuildSubtarget.Player,options=BuildOptions.None});
            string json=JsonUtility.ToJson(new BuildOutcome{result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,bytes=report.summary.totalSize,output=report.summary.outputPath},true);
            File.WriteAllText(Path.Combine(output,"build-result.json"),json);
            if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("Falló la exportación Windows: "+json);
            File.WriteAllText(Path.Combine(output,"LEEME.txt"),"RYTHMWARE — BETA\r\n\r\nExtrae toda esta carpeta y abre RhythmWare.exe. Conserva RhythmWare_Data, UnityPlayer.dll y las demás carpetas junto al ejecutable. No envíes solo el .exe.\r\n\r\nControles: D F / J K y SPACE (remapeables, también mando). Esc: pausa. F3/F4: velocidad de notas de 0,5 a 8x. Rueda: volumen general; Shift + rueda: efectos.\r\nAl primer inicio te ofreceremos calibrar el audio. Puedes repetirlo desde Ajustes.\r\nLos ajustes, récords y el progreso SOC se guardan en el perfil local de cada jugador.\r\n");
            Debug.Log("[RhythmWare Build] Windows listo para compartir: "+output);
        }
        public static void ExportWindowsBatch()
        {
            try { ExportWindows();EditorApplication.Exit(0); }
            catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
        }
        static string Argument(string key) { var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:null; }
        [Serializable] sealed class BuildOutcome { public string result,output;public int errors,warnings;public ulong bytes; }
    }
}
#endif
