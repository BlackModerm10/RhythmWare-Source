using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using RhythmWare.Core;
using RhythmWare.Malware;
using RhythmWare.UI;

namespace RhythmWare.Rhythm
{
    public enum BeatSnap { Snap_1_1, Snap_1_2, Snap_1_4, Snap_1_8, Snap_1_3, Snap_1_6, Snap_1_12, Snap_1_16 }
    [Serializable] public struct SongFileInfo { public string displayName, fullPath; }
    public class ChartEditorManager : MonoBehaviour
    {
        public static ChartEditorManager Instance { get; private set; }
        public ChartData ActiveChart { get; private set; }
        public EditorCommandHistory History { get; private set; }
        public EditorSelectionManager Selection { get; } = new EditorSelectionManager();
        public EditorNoteIndex NoteIndex { get; } = new EditorNoteIndex();
        public List<SongFileInfo> AvailableSongs { get; } = new List<SongFileInfo>();
        public List<MalwareData> MalwarePalette { get; } = new List<MalwareData>();
        public double PlaybackTime { get; private set; }
        public double Duration => ActiveChart?.audioClip!=null?ActiveChart.audioClip.length:300;
        public bool IsPlaying { get; private set; }
        public bool IsRecording { get; private set; }
        public float PlaybackRate { get; private set; }=1;
        public void SetPlaybackRate(float rate){PlaybackRate=Mathf.Clamp(rate,.25f,1);preview.pitch=PlaybackRate;OnPlaybackTimeUpdated?.Invoke(PlaybackTime);}
        public BeatSnap CurrentSnap { get; private set; } = BeatSnap.Snap_1_4;
        public int SelectedMalwareIndex { get; private set; } = -1;
        public double CurrentNoteDuration { get; private set; }
        public string CurrentPath { get; private set; }
        string protectedLegacyPath;
        public bool Dirty => metadataDirty || (History!=null&&History.Dirty);
        public event Action<double> OnPlaybackTimeUpdated;
        public event Action<ChartData> OnChartLoaded;
        public event Action<bool> OnPlayStateChanged, OnRecordStateChanged;
        public event Action OnAvailableSongsUpdated, OnDocumentChanged, OnAudioChanged;
        public event Action<string> OnStatus;
        AudioSource preview;
        bool metadataDirty;
        int loadGeneration;
        double returnTime;
        ChartData testChart;

        void Awake()
        {
            if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}Instance=this;
            preview=gameObject.AddComponent<AudioSource>();preview.playOnAwake=false;
            var catalog=ChartData.GetDefaultMalwareCatalog();foreach(var m in catalog.Values.Distinct())if(m is SpywareData||m is AdwareData||m is TrojanData)MalwarePalette.Add(m);
            CreateNewChart("Mi Cancion","Artista",120);
            // The untouched startup placeholder contains no user work.
            // Every later metadata or note edit still marks the document dirty.
            metadataDirty=false;
        }
        void Start() { RefreshAvailableSongs(); }
        void Update()
        {
            if(GameManager.Instance==null||GameManager.Instance.CurrentState!=GameState.Editor){if(IsPlaying)Pause();return;}
            if(CandyMenuTransition.IsRunning)return;
            preview.volume=GameSettings.MasterVolume*GameSettings.MusicVolume;
            if(IsPlaying)
            {
                if(preview.isPlaying) { PlaybackTime=preview.time;OnPlaybackTimeUpdated?.Invoke(PlaybackTime); }
                else { PlaybackTime=Duration;IsPlaying=false;OnPlayStateChanged?.Invoke(false);OnPlaybackTimeUpdated?.Invoke(PlaybackTime); }
            }
            if(Keyboard.current==null||CyberUI.Typing||CyberUI.ModalOpen)return;
            var kb=Keyboard.current;bool ctrl=kb.ctrlKey.isPressed||kb.leftMetaKey.isPressed||kb.rightMetaKey.isPressed;
            if(ctrl)
            {
                if(kb.zKey.wasPressedThisFrame)History.Undo();if(kb.yKey.wasPressedThisFrame)History.Redo();
                if(kb.aKey.wasPressedThisFrame)Selection.SelectMany(ActiveChart.notes);
                if(kb.cKey.wasPressedThisFrame)Selection.Copy();if(kb.vKey.wasPressedThisFrame)Selection.Paste(History,Quantize(PlaybackTime));return;
            }
            if(kb.spaceKey.wasPressedThisFrame&&!IsRecording)TogglePlayPause();
            if(kb.rKey.wasPressedThisFrame)ToggleLiveRecording();if(kb.homeKey.wasPressedThisFrame)Seek(0);
            if(kb.leftArrowKey.wasPressedThisFrame) { if(kb.shiftKey.isPressed)StepTime(-GetSnapTimeSeconds()*16);else StepSnap(-1); }
            if(kb.rightArrowKey.wasPressedThisFrame) { if(kb.shiftKey.isPressed)StepTime(GetSnapTimeSeconds()*16);else StepSnap(1); }
            if(kb.deleteKey.wasPressedThisFrame||kb.backspaceKey.wasPressedThisFrame)Selection.Delete(History);
            if(IsRecording&&IsPlaying)
            {
                for(int lane=0;lane<LaneLayout.Count;lane++)
                    if(kb[InputManager.Instance.GetKeyForLane(lane)].wasPressedThisFrame)AddOrReplaceNote(Quantize(PlaybackTime),lane);
            }
        }
        public void CreateNewChart(string title,string artist,float bpm)
        {
            Pause();loadGeneration++;SetAudioClip(null);
            var chart=ScriptableObject.CreateInstance<ChartData>();chart.chartId=Guid.NewGuid().ToString("N");
            chart.songTitle=title;chart.artist=artist;chart.bpm=Mathf.Max(1,bpm);CurrentPath=null;protectedLegacyPath=null;Install(chart);metadataDirty=true;
        }
        void Install(ChartData chart)
        {
            ActiveChart=chart;History=new EditorCommandHistory(chart,DocumentChanged);Selection.Clear();PlaybackTime=0;
            metadataDirty=false;NoteIndex.Rebuild(chart);OnChartLoaded?.Invoke(chart);OnPlaybackTimeUpdated?.Invoke(0);
        }
        void DocumentChanged() { NoteIndex.Rebuild(ActiveChart);Selection.Reconcile(ActiveChart);OnDocumentChanged?.Invoke(); }
        public void MetadataChanged() { metadataDirty=true;OnDocumentChanged?.Invoke(); }
        public void SetAudioClip(AudioClip clip)
        { preview.Stop();IsPlaying=false;if(ActiveChart!=null)ActiveChart.audioClip=clip;preview.clip=clip;OnAudioChanged?.Invoke(); }
        public void Play()
        {
            if(preview.clip==null){OnStatus?.Invoke("Importa audio antes de reproducir.");return;}
            if(PlaybackTime>=Duration)PlaybackTime=0;
            preview.pitch=PlaybackRate;preview.time=(float)Math.Min(PlaybackTime,Duration-.01);preview.Play();IsPlaying=true;OnPlayStateChanged?.Invoke(true);
        }
        public void Pause()
        {
            if(preview!=null){if(IsPlaying&&preview.clip!=null)PlaybackTime=preview.time;preview.Pause();}
            IsPlaying=false;OnPlayStateChanged?.Invoke(false);if(IsRecording){IsRecording=false;OnRecordStateChanged?.Invoke(false);}
        }
        public void Stop() { Pause();Seek(0); }
        public void TogglePlayPause() { if(IsPlaying)Pause();else Play(); }
        public void Seek(double time)
        {
            PlaybackTime=Math.Max(0,Math.Min(Duration,time));if(preview.clip!=null)preview.time=(float)Math.Min(PlaybackTime,Duration-.001);
            OnPlaybackTimeUpdated?.Invoke(PlaybackTime);
        }
        public void StepTime(double delta)=>Seek(PlaybackTime+delta);
        public void StepSnap(int direction)
        {
            double step=GetSnapTimeSeconds(),offset=ActiveChart.offsetMs/1000.0,beat=(PlaybackTime-offset)/step;
            Seek(offset+(direction>0?Math.Floor(beat+.005)+1:Math.Ceiling(beat-.005)-1)*step);
        }
        public void SetBeatSnap(BeatSnap snap) { CurrentSnap=snap;OnDocumentChanged?.Invoke(); }
        public double GetSnapTimeSeconds()
        {
            int divisor=CurrentSnap==BeatSnap.Snap_1_1?1:CurrentSnap==BeatSnap.Snap_1_2?2:CurrentSnap==BeatSnap.Snap_1_3?3:
                CurrentSnap==BeatSnap.Snap_1_4?4:CurrentSnap==BeatSnap.Snap_1_6?6:CurrentSnap==BeatSnap.Snap_1_8?8:CurrentSnap==BeatSnap.Snap_1_12?12:16;
            return 60.0/Math.Max(1,ActiveChart.bpm)/divisor;
        }
        public double Quantize(double time)
        {
            double step=GetSnapTimeSeconds(),offset=ActiveChart.offsetMs/1000.0;
            return Math.Max(0,offset+Math.Round((time-offset)/step)*step);
        }
        public void ToggleLiveRecording() { IsRecording=!IsRecording;if(IsRecording&&!IsPlaying)Play();if(!IsPlaying)IsRecording=false;OnRecordStateChanged?.Invoke(IsRecording); }
        public void SetNoteDuration(double seconds)=>CurrentNoteDuration=Math.Max(0,seconds);
        public void SelectMalwareIndex(int index)=>SelectedMalwareIndex=index;
        public void AddOrReplaceNote(double time,int lane,float speed=1,double duration=-1)
        {
            if(!LaneLayout.Valid(lane)||ActiveChart==null)return;
            var note=new NoteData{timestamp=Math.Max(0,time),laneIndex=lane,speedMultiplier=Mathf.Max(.05f,speed),duration=duration<0?CurrentNoteDuration:Math.Max(0,duration),
                malwareData=SelectedMalwareIndex>=0&&SelectedMalwareIndex<MalwarePalette.Count?MalwarePalette[SelectedMalwareIndex]:null};
            var changes=ActiveChart.notes.Where(n=>n.laneIndex==lane&&Math.Abs(n.timestamp-time)<.001).Select(n=>new EditorNoteMutation(n,EditorNoteState.Capture(n),null)).ToList();
            changes.Add(new EditorNoteMutation(note,null,EditorNoteState.Capture(note)));History.Execute(changes);Selection.Select(note);
        }
        public void ApplyProperties(Action<NoteData> change)
        {
            var mutations=new List<EditorNoteMutation>();foreach(var n in Selection.Notes.ToArray())
            {var before=EditorNoteState.Capture(n);change(n);mutations.Add(new EditorNoteMutation(n,before,EditorNoteState.Capture(n)));}
            History.Execute(mutations);
        }
        public bool RemoveClosestNoteAtCurrentTime(int lane=-1)
        {
            var note=ActiveChart.notes.Where(n=>(lane<0||n.laneIndex==lane)&&Math.Abs(n.timestamp-PlaybackTime)<Math.Max(.12,GetSnapTimeSeconds()*.6)).OrderBy(n=>Math.Abs(n.timestamp-PlaybackTime)).FirstOrDefault();
            if(note==null)return false;History.Execute(new List<EditorNoteMutation>{new EditorNoteMutation(note,EditorNoteState.Capture(note),null)});return true;
        }
        public void RemoveNoteAt(double time,int lane)
        {History.Execute(ActiveChart.notes.Where(n=>n.laneIndex==lane&&Math.Abs(n.timestamp-time)<.1).Select(n=>new EditorNoteMutation(n,EditorNoteState.Capture(n),null)).ToList());}
        public void RefreshAvailableSongs()
        {
            AvailableSongs.Clear();foreach(var folder in new[]{Path.Combine(GameContentPaths.AudioRoot,"Music","Levels"),Path.Combine(GameContentPaths.AudioRoot,"Music"),Path.Combine(Application.streamingAssetsPath,"Songs"),Path.Combine(Application.persistentDataPath,"Songs")})
            {
                if(!Directory.Exists(folder))continue;foreach(var file in Directory.GetFiles(folder))
                {string ext=Path.GetExtension(file).ToLowerInvariant();if(ext==".mp3"||ext==".ogg"||ext==".wav")AvailableSongs.Add(new SongFileInfo{displayName=Path.GetFileNameWithoutExtension(file),fullPath=file});}
            }
            AvailableSongs.Sort((a,b)=>string.Compare(a.displayName,b.displayName,StringComparison.OrdinalIgnoreCase));OnAvailableSongsUpdated?.Invoke();
        }
        public void LoadSongByIndex(int index,Action<bool> complete=null)
        {if(index<0||index>=AvailableSongs.Count){complete?.Invoke(false);return;}ImportAudio(AvailableSongs[index].fullPath,complete);}
        public void ImportAudio(string path,Action<bool> complete=null,bool markDirty=true)
        { Pause();int token=++loadGeneration;StartCoroutine(LoadAudio(path,token,complete,markDirty)); }
        IEnumerator LoadAudio(string path,int token,Action<bool> complete,bool dirty)
        {
            path=GameContentPaths.ResolveAudio(path);
            AudioClip clip=null;yield return AudioManager.Instance.LoadAudioFile(path,c=>clip=c);
            if(token!=loadGeneration)yield break;
            if(clip==null){OnStatus?.Invoke("No se pudo leer el audio.");complete?.Invoke(false);yield break;}
            SetAudioClip(clip);ActiveChart.audioClipPath=path;Seek(0);if(dirty)MetadataChanged();OnChartLoaded?.Invoke(ActiveChart);complete?.Invoke(true);
        }
        public string SaveChartToJson(string customFileName="")
        {
            string name=string.IsNullOrEmpty(customFileName)?ActiveChart.songTitle:customFileName;foreach(char c in Path.GetInvalidFileNameChars())name=name.Replace(c,'_');
            string path=string.IsNullOrEmpty(CurrentPath)?Path.Combine(Application.persistentDataPath,"Charts",name+".json"):CurrentPath;
            return SaveToPath(path,path==CurrentPath)?path:null;
        }
        public bool SaveToPath(string filePath,bool overwrite=false,bool export=false)
        {
            try
            {
                string path=Path.GetFullPath(filePath);if(string.Equals(path,protectedLegacyPath,StringComparison.OrdinalIgnoreCase)){OnStatus?.Invoke("El original antiguo está protegido. Guarda la copia con otro nombre.");return false;}if(File.Exists(path)&&!overwrite){OnStatus?.Invoke("El archivo existe. Confirma reemplazar o usa otro nombre.");return false;}
                Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";File.WriteAllText(temp,ActiveChart.ToJson());
                if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
                if(!export){CurrentPath=path;metadataDirty=false;History.MarkSaved();}OnStatus?.Invoke("Guardado: "+path);OnDocumentChanged?.Invoke();return true;
            }
            catch(Exception ex){OnStatus?.Invoke("Error al guardar: "+ex.Message);return false;}
        }
        public bool LoadChartFromJson(string path,bool convertLegacy=false)
        {
            try
            {
                var catalog=ChartData.GetDefaultMalwareCatalog();
                var chart=ChartData.FromJson(File.ReadAllText(path),catalog);
                bool legacy=chart!=null&&chart.IsLegacy;string report="";
                if(legacy){if(!convertLegacy){OnStatus?.Invoke("Mapa de 7 teclas: crea una copia desde la biblioteca.");return false;}chart=LegacyChartConversion.ConvertCopy(chart,out report);}
                if(chart==null||chart.notes.Any(n=>double.IsNaN(n.timestamp)||double.IsInfinity(n.timestamp)||n.timestamp<0||!LaneLayout.Valid(n.laneIndex)||n.duration<0||n.duration>3600||double.IsNaN(n.duration)||double.IsInfinity(n.duration)||float.IsNaN(n.speedMultiplier)||float.IsInfinity(n.speedMultiplier)))throw new InvalidDataException("Notas inválidas.");
                Pause();loadGeneration++;SetAudioClip(null);protectedLegacyPath=legacy?Path.GetFullPath(path):null;CurrentPath=legacy?null:Path.GetFullPath(path);Install(chart);metadataDirty=legacy;
                string audio=GameContentPaths.ResolveAudio(chart.audioClipPath,Path.GetDirectoryName(Path.GetFullPath(path)));
                if(!string.IsNullOrEmpty(audio))ImportAudio(audio,null,false);OnStatus?.Invoke(legacy?report:"Mapa abierto: "+chart.songTitle);return true;
            }
            catch(Exception ex){OnStatus?.Invoke("No se pudo abrir: "+ex.Message);return false;}
        }
        public bool OpenCatalogChart(ChartData source,string sourcePath=null,bool convertLegacy=false)
        {
            if(source==null)return false;
            bool legacy=source.IsLegacy;string report="";
            if(legacy&&!convertLegacy){OnStatus?.Invoke("Confirma crear una copia de cinco teclas.");return false;}
            Pause();loadGeneration++;
            var copy=legacy?LegacyChartConversion.ConvertCopy(source,out report):Instantiate(source);
            copy.notes=copy.notes.Select(n=>new NoteData{timestamp=n.timestamp,laneIndex=n.laneIndex,malwareData=n.malwareData,speedMultiplier=n.speedMultiplier,duration=n.duration}).ToList();
            bool official=string.IsNullOrEmpty(sourcePath);
            if(official)copy.chartId=Guid.NewGuid().ToString("N");
            protectedLegacyPath=legacy&&!official?Path.GetFullPath(sourcePath):null;CurrentPath=official||legacy?null:Path.GetFullPath(sourcePath);Install(copy);SetAudioClip(source.audioClip);
            metadataDirty=official||legacy;
            if(copy.audioClip==null&&!string.IsNullOrEmpty(copy.audioClipPath))ImportAudio(copy.audioClipPath,null,false);
            OnStatus?.Invoke(legacy?report:official?"Copia editable del mapa oficial. Usa Save As para guardar tu versión.":"Mapa personalizado abierto: "+copy.songTitle);
            OnDocumentChanged?.Invoke();return true;
        }
        public bool TestPlayCurrentChart()=>TestPlay(false);
        public bool TestPlay(bool fromHere)
        {
            if(ActiveChart.audioClip==null||ActiveChart.notes.Count==0){OnStatus?.Invoke("Agrega audio y notas antes de probar.");return false;}
            Pause();returnTime=PlaybackTime;double start=fromHere?PlaybackTime:0;
            if(testChart!=null)Destroy(testChart);testChart=Instantiate(ActiveChart);testChart.notes=ActiveChart.notes.Where(n=>n.timestamp>=start).ToList();
            if(testChart.notes.Count==0){OnStatus?.Invoke("No hay notas después del cursor.");return false;}
            GameplayUI.IsTestPlayFromEditor=true;ChartManager.Instance.SelectChart(testChart);
            NoteSpawner.Instance.LoadChart(testChart);AudioManager.Instance.PlaySong(testChart.audioClip,testChart.PlaybackLeadIn,start);GameManager.Instance.StartGame();return true;
        }
        public void RestoreAfterTest()
        { ChartManager.Instance.SelectChart(ActiveChart);Seek(returnTime);if(testChart!=null){Destroy(testChart);testChart=null;} }
    }
}
