using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using RhythmWare.Core;
using RhythmWare.Rhythm;

namespace RhythmWare.UI
{
    public class ChartEditorUI : MonoBehaviour
    {
        ChartEditorManager editor;
        EditorTimeline timeline;
        EditorFileDialog files;
        ChartLibraryUI library;
        TMP_InputField title,artist,mapper,bpm,offset,difficulty,time,lane,speed,duration;
        TextMeshProUGUI transport,status,selection,malware,snap,titlebar;
        int snapIndex=3;
        int rateIndex;readonly float[] rates={1,.75f,.5f,.25f};
        readonly BeatSnap[] snaps={BeatSnap.Snap_1_1,BeatSnap.Snap_1_2,BeatSnap.Snap_1_3,BeatSnap.Snap_1_4,BeatSnap.Snap_1_6,BeatSnap.Snap_1_8,BeatSnap.Snap_1_12,BeatSnap.Snap_1_16};
        readonly int[] divisors={1,2,3,4,6,8,12,16};
        bool built;
        static bool Number(string s,out double value) => double.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&!double.IsNaN(value)&&!double.IsInfinity(value);
        public void Build()
        {
            editor=ChartEditorManager.Instance;files=gameObject.AddComponent<EditorFileDialog>();library=gameObject.AddComponent<ChartLibraryUI>();
            CandyUI.Backdrop(transform);
            titlebar=CyberUI.Label(transform,"RHYTHMWARE / MAPPING STUDIO",new Vector2(.02f,.94f),new Vector2(.68f,1),24,CyberUI.Cyan);
            CyberUI.Button(transform,"VOLVER [ESC]",new Vector2(.84f,.94f),new Vector2(.98f,1),Exit);
            string[] names={"NEW","MAP LIBRARY","SAVE","SAVE AS","IMPORT AUDIO","EXPORT MAP","UNDO","REDO","TEST FULL","TEST HERE"};
            Action[] actions={()=>Guard(()=>editor.CreateNewChart("Untitled","",120)),()=>Guard(Open),Save,()=>SaveAs(false),()=>Guard(ImportAudio),()=>SaveAs(true),()=>editor.History.Undo(),()=>editor.History.Redo(),()=>editor.TestPlay(false),()=>editor.TestPlay(true)};
            for(int i=0;i<names.Length;i++)CyberUI.Button(transform,names[i],new Vector2(.015f+i*.097f,.875f),new Vector2(.015f+(i+1)*.097f,.94f),actions[i]);
            var meta=CyberUI.Box(transform,"MapInfo",new Vector2(.015f,.13f),new Vector2(.19f,.865f),CyberUI.Panel);
            CyberUI.Label(meta.transform,"MAP INFO",new Vector2(.05f,.91f),new Vector2(.96f,.99f),23,CyberUI.Cyan);
            title=MetaField(meta.transform,"TITLE",.80f,v=>{editor.ActiveChart.songTitle=v;editor.MetadataChanged();});
            artist=MetaField(meta.transform,"ARTIST",.67f,v=>{editor.ActiveChart.artist=v;editor.MetadataChanged();});
            mapper=MetaField(meta.transform,"MAPPER",.54f,v=>{editor.ActiveChart.mapper=v;editor.MetadataChanged();});
            bpm=MetaField(meta.transform,"BPM",.41f,v=>{if(Number(v,out var n)&&n>0&&n<=1000){editor.ActiveChart.bpm=(float)n;editor.MetadataChanged();}else ShowStatus("BPM inválido (1–1000).");});
            offset=MetaField(meta.transform,"OFFSET / ms",.28f,v=>{if(Number(v,out var n)){editor.ActiveChart.offsetMs=(float)n;editor.MetadataChanged();}});
            difficulty=MetaField(meta.transform,"DIFFICULTY ID",.15f,v=>{if(string.IsNullOrWhiteSpace(v))return;editor.ActiveChart.difficultyId=v.Trim();editor.MetadataChanged();});
            CyberUI.Button(meta.transform,"AUDIO LIBRARY",new Vector2(.04f,.015f),new Vector2(.96f,.10f),()=>Guard(AudioLibrary));
            var timelineRect=CyberUI.Rect(transform,"EditorTimeline",new Vector2(.235f,.19f),new Vector2(.785f,.86f));
            timelineRect.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            timeline=timelineRect.gameObject.AddComponent<EditorTimeline>();timeline.Initialize(editor);
            for(int row=0;row<LaneLayout.Count;row++)
            {
                float y=.19f+(.67f*.76f)*(LaneLayout.Count-1-row)/LaneLayout.Count;
                var laneLabel=CyberUI.Label(transform,CyberUI.LaneNames[CyberUI.LaneOrder[row]],new Vector2(.194f,y),new Vector2(.235f,y+.67f*.76f/LaneLayout.Count),CyberUI.LaneOrder[row]==LaneLayout.Center?15:22,CyberUI.Cyan);
                laneLabel.textWrappingMode=TextWrappingModes.NoWrap;laneLabel.alignment=TextAlignmentOptions.Center;
            }
            var properties=CyberUI.Box(transform,"Properties",new Vector2(.80f,.19f),new Vector2(.985f,.865f),CyberUI.Panel);
            selection=CyberUI.Label(properties.transform,"NOTE PROPERTIES",new Vector2(.04f,.89f),new Vector2(.96f,.99f),22,CyberUI.Cyan);
            time=MetaField(properties.transform,"TIMESTAMP / seconds",.76f,v=>{if(Number(v,out var n)&&n>=0)editor.ApplyProperties(note=>note.timestamp=n);});
            lane=MetaField(properties.transform,"LANE / 0..4",.62f,v=>{if(int.TryParse(v,out int n)&&LaneLayout.Valid(n))editor.ApplyProperties(note=>note.laneIndex=n);});
            speed=MetaField(properties.transform,"SPEED",.48f,v=>{if(Number(v,out var n)&&n>0&&n<=20)editor.ApplyProperties(note=>note.speedMultiplier=(float)n);});
            duration=MetaField(properties.transform,"HOLD / seconds (0 = tap)",.34f,v=>{if(Number(v,out var n)&&n>=0&&n<=3600){editor.SetNoteDuration(n);editor.ApplyProperties(note=>note.duration=n);}});
            malware=CyberUI.Label(properties.transform,"NORMAL PACKET",new Vector2(.04f,.22f),new Vector2(.96f,.32f),20);
            CyberUI.Button(properties.transform,"< TYPE",new Vector2(.04f,.13f),new Vector2(.49f,.22f),()=>ChangeMalware(-1));
            CyberUI.Button(properties.transform,"TYPE >",new Vector2(.51f,.13f),new Vector2(.96f,.22f),()=>ChangeMalware(1));
            CyberUI.Button(properties.transform,"DELETE",new Vector2(.04f,.015f),new Vector2(.96f,.11f),()=>editor.Selection.Delete(editor.History));
            snap=CyberUI.Label(transform,"SNAP 1/4 / GRID 4 BEATS",new Vector2(.235f,.13f),new Vector2(.51f,.19f),19,CyberUI.Muted);
            CyberUI.Button(transform,"SNAP",new Vector2(.50f,.13f),new Vector2(.59f,.19f),()=>{snapIndex=(snapIndex+1)%snaps.Length;editor.SetBeatSnap(snaps[snapIndex]);snap.text=$"SNAP 1/{divisors[snapIndex]} / GRID 4 BEATS";});
            CyberUI.Button(transform,"ZOOM +",new Vector2(.60f,.13f),new Vector2(.69f,.19f),()=>timeline.Zoom(.8f));
            CyberUI.Button(transform,"ZOOM -",new Vector2(.69f,.13f),new Vector2(.785f,.19f),()=>timeline.Zoom(1.25f));
            string[] controls={"HOME","<<","<","PLAY / PAUSE",">",">>","STOP","REC"};
            Action[] transportActions={()=>editor.Seek(0),()=>editor.StepTime(-editor.GetSnapTimeSeconds()*16),()=>editor.StepSnap(-1),()=>editor.TogglePlayPause(),()=>editor.StepSnap(1),()=>editor.StepTime(editor.GetSnapTimeSeconds()*16),()=>editor.Stop(),()=>editor.ToggleLiveRecording()};
            for(int i=0;i<controls.Length;i++)CyberUI.Button(transform,controls[i],new Vector2(.015f+i*.08f,.065f),new Vector2(.015f+(i+1)*.08f,.125f),transportActions[i]);
            UnityEngine.UI.Button rate=null;rate=CyberUI.Button(transform,"AUDIO 1,00×",new Vector2(.66f,.065f),new Vector2(.79f,.125f),()=>{rateIndex=(rateIndex+1)%rates.Length;editor.SetPlaybackRate(rates[rateIndex]);rate.GetComponentInChildren<TextMeshProUGUI>().text="AUDIO "+rates[rateIndex].ToString("0.00",CultureInfo.InvariantCulture)+"×";});
            UnityEngine.UI.Button attacks=null;attacks=CyberUI.Button(transform,"ATAQUES: DORADO",new Vector2(.80f,.13f),new Vector2(.985f,.19f),()=>{timeline.ShowAudioAttacks=!timeline.ShowAudioAttacks;attacks.GetComponentInChildren<TextMeshProUGUI>().text=timeline.ShowAudioAttacks?"ATAQUES: DORADO":"ATAQUES: OCULTOS";});
            transport=CyberUI.Label(transform,"",new Vector2(.795f,.065f),new Vector2(.985f,.125f),20);transport.enableAutoSizing=true;transport.fontSizeMin=12;
            status=CyberUI.Label(transform,"CLICK: añadir / SHIFT: seleccionar / CTRL+WHEEL: zoom / arrastra cola: hold",new Vector2(.015f,.005f),new Vector2(.985f,.062f),18,CyberUI.Muted);
            editor.OnChartLoaded+=RefreshMetadata;editor.OnStatus+=ShowStatus;editor.OnDocumentChanged+=RefreshDocument;
            editor.Selection.Changed+=RefreshSelection;editor.OnPlaybackTimeUpdated+=RefreshTime;
            editor.OnRecordStateChanged+=Recording;built=true;RefreshMetadata(editor.ActiveChart);RefreshDocument();
        }
        TMP_InputField MetaField(Transform panel,string label,float y,Action<string> change)
        {
            CyberUI.Label(panel,label,new Vector2(.04f,y+.035f),new Vector2(.96f,y+.105f),16,CyberUI.Muted);
            return CyberUI.Input(panel,"",new Vector2(.04f,y-.035f),new Vector2(.96f,y+.035f),change);
        }
        void OnEnable() { if(built){RefreshMetadata(editor.ActiveChart);RefreshDocument();} }
        void OnDestroy()
        {
            if(!built)return;editor.OnChartLoaded-=RefreshMetadata;editor.OnStatus-=ShowStatus;editor.OnDocumentChanged-=RefreshDocument;
            editor.Selection.Changed-=RefreshSelection;editor.OnPlaybackTimeUpdated-=RefreshTime;editor.OnRecordStateChanged-=Recording;
        }
        void RefreshMetadata(ChartData chart)
        {
            if(chart==null)return;title.SetTextWithoutNotify(chart.songTitle);artist.SetTextWithoutNotify(chart.artist);mapper.SetTextWithoutNotify(chart.mapper);
            bpm.SetTextWithoutNotify(chart.bpm.ToString(CultureInfo.InvariantCulture));offset.SetTextWithoutNotify(chart.offsetMs.ToString(CultureInfo.InvariantCulture));difficulty.SetTextWithoutNotify(chart.DifficultyId);
        }
        void RefreshDocument()
        {
            titlebar.text=$"RHYTHMWARE / MAPPING STUDIO / {editor.ActiveChart.notes.Count} NOTES{(editor.Dirty?" *":"")}";
            snapIndex=Math.Max(0,Array.IndexOf(snaps,editor.CurrentSnap));
            if(snap!=null)snap.text=$"SNAP 1/{divisors[snapIndex]} / GRID 4 BEATS";
            RefreshTime(editor.PlaybackTime);
        }
        void RefreshTime(double t) { transport.text=$"{CyberUI.TimeLabel(t)} / {(editor.ActiveChart.audioClip!=null?CyberUI.TimeLabel(editor.Duration):"--:--.---")}"; }
        void Recording(bool active) { ShowStatus(active?"REC / D F SPACE J K / R para finalizar":"Grabación finalizada."); }
        void RefreshSelection()
        {
            selection.text=$"NOTE PROPERTIES / {editor.Selection.Count}";
            var note=editor.Selection.Notes.FirstOrDefault();if(note==null)return;
            time.SetTextWithoutNotify(note.timestamp.ToString("F3",CultureInfo.InvariantCulture));lane.SetTextWithoutNotify(note.laneIndex.ToString());
            speed.SetTextWithoutNotify(note.speedMultiplier.ToString(CultureInfo.InvariantCulture));duration.SetTextWithoutNotify(note.duration.ToString("F3",CultureInfo.InvariantCulture));
            malware.text=note.malwareData!=null?note.malwareData.malwareName:"NORMAL PACKET";
        }
        void ChangeMalware(int direction)
        {
            int index=editor.SelectedMalwareIndex+direction;if(index<-1)index=editor.MalwarePalette.Count-1;if(index>=editor.MalwarePalette.Count)index=-1;
            editor.SelectMalwareIndex(index);var type=index>=0?editor.MalwarePalette[index]:null;
            editor.ApplyProperties(note=>note.malwareData=type);malware.text=type!=null?type.malwareName:"NORMAL PACKET";
        }
        void Guard(Action action)
        {
            if(!editor.Dirty){action();return;}
            files.Confirm("You have unsaved changes.\nGuarda, descarta o continúa editando.",()=>SaveThen(action),action);
        }
        void SaveThen(Action next)
        {
            if(!string.IsNullOrEmpty(editor.CurrentPath)){if(editor.SaveToPath(editor.CurrentPath,true))next();}
            else SaveAs(false,next);
        }
        void Save() { if(!string.IsNullOrEmpty(editor.CurrentPath))editor.SaveToPath(editor.CurrentPath,true);else SaveAs(false); }
        void SaveAs(bool export,Action next=null)
        {
            string folder=Path.Combine(Application.persistentDataPath,"Charts");
            string safe=editor.ActiveChart.songTitle;foreach(char c in Path.GetInvalidFileNameChars())safe=safe.Replace(c,'_');
            files.PathDialog(export?"EXPORT MAP":"SAVE AS",Path.Combine(folder,safe+".json"),ChartFiles(),path=>
            {
                if(!path.EndsWith(".json",StringComparison.OrdinalIgnoreCase))path+=".json";
                Action write=()=>{if(editor.SaveToPath(path,File.Exists(path),export))next?.Invoke();};
                if(File.Exists(path))files.Overwrite(path,write);else write();
            });
        }
        IEnumerable<string> ChartFiles()
        {
            foreach(var folder in new[]{Path.Combine(Application.persistentDataPath,"Charts"),GameContentPaths.BundledChartsRoot})
                if(Directory.Exists(folder))foreach(var file in Directory.GetFiles(folder,"*.json"))yield return file;
        }
        void Open()=>library.Show(UnityEngine.Object.FindAnyObjectByType<LevelSelectUI>(FindObjectsInactive.Include),entry=>OpenWithConversion(entry.chartData,entry.chartFilePath),OpenJson);
        void OpenJson()=>files.PathDialog("OPEN CHART JSON","",ChartFiles(),path=>{
            try{var source=ChartData.FromJson(File.ReadAllText(path));if(source.IsLegacy)ConfirmLegacy(source,()=>editor.LoadChartFromJson(path,true));else editor.LoadChartFromJson(path);}
            catch(Exception e){ShowStatus("No se pudo abrir: "+e.Message);}
        });
        void ConfirmLegacy(ChartData source,Action convert){var preview=LegacyChartConversion.ConvertCopy(source,out string report);UnityEngine.Object.Destroy(preview);files.ConfirmConversion(report,convert);}
        void OpenWithConversion(ChartData source,string path){if(source.IsLegacy)ConfirmLegacy(source,()=>editor.OpenCatalogChart(source,path,true));else editor.OpenCatalogChart(source,path);}
        public void OpenCatalogChart(RhythmWare.Rhythm.ChartData chart,string sourcePath=null)=>Guard(()=>OpenWithConversion(chart,sourcePath));
        public void OpenLibrary()=>Guard(Open);
        void ImportAudio()=>files.PathDialog("IMPORT AUDIO","",editor.AvailableSongs.Select(s=>s.fullPath),path=>editor.ImportAudio(path));
        void AudioLibrary() { editor.RefreshAvailableSongs();ImportAudio(); }
        void Exit()=>Guard(()=>{editor.Pause();MainMenuUI.Instance.BackToMainMenu();});
        public void ShowStatus(string message) { if(status!=null)status.text=message; }
        void Update()
        {
            if(CandyMenuTransition.IsRunning)return;
            if(!built||Keyboard.current==null)return;var kb=Keyboard.current;
            if(library.IsOpen){if(kb.escapeKey.wasPressedThisFrame)library.Close();return;}
            if(files.IsOpen){if(kb.escapeKey.wasPressedThisFrame)files.Close();return;}
            if(CyberUI.Typing)return;
            if(kb.escapeKey.wasPressedThisFrame)Exit();
            if(kb.ctrlKey.isPressed&&kb.sKey.wasPressedThisFrame)Save();
        }
    }
}

