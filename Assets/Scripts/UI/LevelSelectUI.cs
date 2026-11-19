using System;
using System.Linq;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.Malware;

namespace RhythmWare.UI
{
    [System.Serializable]
    public class LevelItemInfo
    {
        public string levelTitle;
        public string songTitle;
        public string artist = "RhythmWare Core";
        public string description;
        public float bpm = 120f;
        public int noteCount = 0;
        public ChartDifficulty difficulty = ChartDifficulty.Normal;
        public ChartData chartData;
        public string chartFilePath;
        public string audioClipPath;
        public AudioClip audioClip;
        public string threatClassification = "AMENAZA ESTÁNDAR // RED LAN";
        public string threatIcon = "[SPY]";
        public string threatBadge = "[SPY]";
        public string threatTags = "<color=#00FFFF>[SPYWARE]</color>";
        public Color themeColor = new Color(0f, 0.85f, 1f, 1f);

        // Optional Cover Artwork and Background Image support
        public Sprite coverSprite;
        public string coverPath;
        public Sprite backgroundSprite;
        public string backgroundPath;
    }

    public class LevelSelectUI : MonoBehaviour
    {
        readonly List<LevelItemInfo> levels = new List<LevelItemInfo>();
        readonly List<int> filtered = new List<int>();
        readonly List<SongSelectItemUI> cards = new List<SongSelectItemUI>();
        int selectedIndex;
        bool loading, built;
        ScrollRect scroll;
        RectTransform content;
        TMPro.TextMeshProUGUI info, status;
        Image cover;
        Button play;
        LeaderboardPanelUI leaderboard;
        SongPreviewPlayer preview;
        readonly List<Button> difficultyButtons=new List<Button>();
        GameObject contextOverlay;
        bool browsingCustom;
        Button profileButton;
        string currentQuery="";
        public IReadOnlyList<LevelItemInfo> RefreshCatalog() { RefreshTrackList();return levels; }

        public void Build()
        {
            preview = AudioManager.Instance.GetComponent<SongPreviewPlayer>();
            if(preview==null) { preview=AudioManager.Instance.gameObject.AddComponent<SongPreviewPlayer>(); preview.Initialize(); }
            CandyUI.Backdrop(transform);
            CyberUI.Label(transform,"TRACK SELECT / CAMPAÑA",new Vector2(.03f,.90f),new Vector2(.78f,.98f),28,CyberUI.Cyan);
            CyberUI.Button(transform,"VOLVER [ESC]",new Vector2(.83f,.91f),new Vector2(.98f,.98f),BackToMainMenu);
            CyberUI.Label(transform,"TRES ETAPAS",new Vector2(.03f,.83f),new Vector2(.18f,.90f),22);
            var search=CyberUI.Input(transform,"",new Vector2(.19f,.84f),new Vector2(.53f,.9f),q=>Filter(q));
            search.placeholder=CyberUI.Label(search.transform,"BUSCAR TÍTULO / ARTISTA",Vector2.zero,Vector2.one,17,CyberUI.Muted);
            scroll=CyberUI.Scroll(transform,new Vector2(.03f,.12f),new Vector2(.53f,.83f),out content);
            // Virtual rows: pool size bounded by viewport, independent of chart count.
            for(int i=0;i<12;i++) { var row=new GameObject("SongCard",typeof(RectTransform)).AddComponent<SongSelectItemUI>();row.transform.SetParent(content,false);row.Build();cards.Add(row); }
            scroll.onValueChanged.AddListener(_=>RefreshCards());
            var panel=CyberUI.Box(transform,"MapInfo",new Vector2(.55f,.45f),new Vector2(.97f,.89f),CyberUI.Panel);
            cover=CyberUI.Box(panel.transform,"Cover",new Vector2(.03f,.43f),new Vector2(.32f,.96f),CyberUI.Raised);
            cover.preserveAspect=true;
            CyberUI.Label(cover.transform,"RW\nNETWORK\nDEFENSE",Vector2.zero,Vector2.one,24,CyberUI.Cyan).alignment=TMPro.TextAlignmentOptions.Center;
            for(int i=0;i<4;i++)
            {
                float y=.08f+i*.08f;
                var chip=CyberUI.Button(panel.transform,"",new Vector2(.025f,y),new Vector2(.32f,y+.085f),null);
                var chipLabel=chip.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                chipLabel.fontSize=16;chipLabel.fontSizeMax=16;chipLabel.textWrappingMode=TMPro.TextWrappingModes.NoWrap;
                chipLabel.rectTransform.offsetMin=new Vector2(2,2);chipLabel.rectTransform.offsetMax=new Vector2(-2,-2);
                difficultyButtons.Add(chip);
            }
            info=CyberUI.Label(panel.transform,"",new Vector2(.34f,.05f),new Vector2(.98f,.98f),23);
            var lb=CyberUI.Box(transform,"Leaderboard",new Vector2(.55f,.13f),new Vector2(.97f,.43f),CyberUI.Panel);
            leaderboard=lb.gameObject.AddComponent<LeaderboardPanelUI>(); leaderboard.Build();
            CyberUI.Label(transform,"OPERADOR",new Vector2(.55f,.075f),new Vector2(.65f,.13f),16,CyberUI.Muted);
            profileButton=CyberUI.Button(transform,PlayerProfile.DisplayName,new Vector2(.63f,.075f),new Vector2(.75f,.13f),()=>MainMenuUI.Instance.OpenParticipantProfiles());
            ParticipantProfiles.InfoChanged+=RefreshProfileLabel;
            status=CyberUI.Label(transform,"UP/DOWN: navegar   ENTER: iniciar   SPACE: preview",new Vector2(.03f,.03f),new Vector2(.75f,.075f),18,CyberUI.Muted);
            Button collection=null;
            collection=CyberUI.Button(transform,"CAMPAÑA / VER MIS MAPAS",new Vector2(.03f,.076f),new Vector2(.33f,.12f),()=>
            {
                browsingCustom=!browsingCustom;collection.GetComponentInChildren<TMPro.TextMeshProUGUI>().text=browsingCustom?"MIS MAPAS / VER CAMPAÑA":"CAMPAÑA / VER MIS MAPAS";Filter(currentQuery);
            });
            play=CyberUI.Button(transform,"INICIAR PROTOCOLO [ENTER]",new Vector2(.76f,.03f),new Vector2(.97f,.11f),StartSelectedLevel,CyberUI.Raised);
            built=true; RefreshTrackList(); Filter("");
        }
        void RefreshProfileLabel(){if(profileButton!=null)profileButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text=PlayerProfile.DisplayName;}
        void OnDestroy(){ParticipantProfiles.InfoChanged-=RefreshProfileLabel;}
        void OnEnable() { if(built) { RefreshTrackList();Filter(""); } }
        void OnDisable() { CloseContext();loading=false; if(preview!=null)preview.Silence(); }
        void Update()
        {
            if(CandyMenuTransition.IsRunning)return;
            if(!built||CyberUI.Typing||CyberUI.ModalOpen||Keyboard.current==null)return;
            if(contextOverlay!=null){if(Keyboard.current.escapeKey.wasPressedThisFrame)CloseContext();return;}
            if(Keyboard.current.escapeKey.wasPressedThisFrame)BackToMainMenu();
            if(Keyboard.current.downArrowKey.wasPressedThisFrame)Navigate(1);
            if(Keyboard.current.upArrowKey.wasPressedThisFrame)Navigate(-1);
            if(Keyboard.current.enterKey.wasPressedThisFrame)StartSelectedLevel();
            if(Keyboard.current.spaceKey.wasPressedThisFrame)preview.Toggle();
        }
        void Navigate(int direction)
        {
            if(filtered.Count==0||loading)return;
            int pos=filtered.IndexOf(selectedIndex);pos=(pos+direction+filtered.Count)%filtered.Count;
            Select(filtered[pos]);
            content.anchoredPosition=new Vector2(0,Mathf.Clamp(pos*142-200,0,Mathf.Max(0,content.sizeDelta.y-scroll.viewport.rect.height)));
            RefreshCards();
        }
        void Filter(string query)
        {
            currentQuery=query??"";
            filtered.Clear();
            for(int i=0;i<levels.Count;i++) if(browsingCustom==!string.IsNullOrEmpty(levels[i].chartFilePath)&&(levels[i].songTitle+" "+levels[i].artist+" "+levels[i].levelTitle).IndexOf(currentQuery,StringComparison.OrdinalIgnoreCase)>=0)filtered.Add(i);
            content.sizeDelta=new Vector2(0,filtered.Count*142);content.anchoredPosition=Vector2.zero;
            if(filtered.Count>0)Select(filtered.Contains(selectedIndex)?selectedIndex:filtered[0]);
            else { info.text="SIN RESULTADOS";play.interactable=false; }
            RefreshCards();
        }
        void RefreshCards()
        {
            int start=Mathf.Max(0,Mathf.FloorToInt(content.anchoredPosition.y/142)-1);
            for(int i=0;i<cards.Count;i++)
            {
                int slot=start+i;var card=cards[i];card.gameObject.SetActive(slot<filtered.Count);
                if(slot>=filtered.Count)continue;
                int index=filtered[slot]; card.Bind(levels[index],index==selectedIndex,()=>Select(index),position=>ShowMapContext(index,position));
                var rt=(RectTransform)card.transform;rt.anchorMin=new Vector2(0,1);rt.anchorMax=Vector2.one;rt.pivot=new Vector2(.5f,1);
                rt.sizeDelta=new Vector2(-24,132);rt.anchoredPosition=new Vector2(index==selectedIndex?8:0,-slot*142);
            }
        }
        void Select(int index)
        {
            if(loading)return;selectedIndex=index;ShowInfo();RefreshCards();
            var item=levels[index];status.text="CARGANDO PREVIEW…";
            preview.Select(item.audioClipPath,item.audioClip,clip=>
            {
                item.audioClip=clip;if(item.chartData!=null)item.chartData.audioClip=clip;
                if(!isActiveAndEnabled||selectedIndex!=index)return;
                status.text=clip==null?"No se pudo cargar audio. Revisa la ruta del mapa.":"FLECHAS: navegar · ENTER: iniciar · CLICK DERECHO: editar";
                ShowInfo();RefreshCards();
            });
        }
        void ShowInfo()
        {
            var item=levels[selectedIndex];var chart=item.chartData;
            string threats=string.Join(" / ",chart.notes.Where(n=>n.malwareData!=null).Select(n=>n.malwareData.GetType().Name.Replace("Data","")).Distinct());
            cover.sprite=item.coverSprite;cover.color=item.coverSprite!=null?Color.white:CyberUI.Raised;
            cover.transform.GetChild(0).gameObject.SetActive(item.coverSprite==null);
            bool campaign=string.IsNullOrEmpty(item.chartFilePath);
            info.text=campaign?$"<b>{item.songTitle}</b>\n<size=20>{item.artist}</size>\n\n{chart.bpm:0.#} BPM · {(item.audioClip!=null?CyberUI.TimeLabel(item.audioClip.length):"--:--")}\n\n<size=18>{item.description}\n\nAMENAZAS: {(threats.Length==0?"Paquetes de datos":threats)}</size>":$"<b>{item.songTitle}</b>\n<size=20>{item.artist}</size>\n\n{chart.DifficultyId.ToUpperInvariant()}  /  {chart.bpm:0.#} BPM\n"+
                $"{(item.audioClip!=null?CyberUI.TimeLabel(item.audioClip.length):"--:--")}  /  {chart.notes.Count} NOTES\n"+
                $"MAPPER: {(string.IsNullOrEmpty(chart.mapper)?"Sin acreditar":chart.mapper)}\nPROGRESIÓN: APRENDE / DOMINA / DEFIENDE\n\n<size=18>{(threats.Length==0?"DATA PACKETS":threats)}\n{item.description}</size>";
            leaderboard.Refresh(chart);
            var variants=levels.Where(l=>l.audioClipPath==item.audioClipPath&&browsingCustom==!string.IsNullOrEmpty(l.chartFilePath)).GroupBy(l=>l.chartData.DifficultyId).Select(g=>g.First()).Take(4).ToList();
            for(int i=0;i<difficultyButtons.Count;i++)
            {
                var button=difficultyButtons[i];button.gameObject.SetActive(!campaign&&i<variants.Count);if(campaign||i>=variants.Count)continue;
                var variant=variants[i];button.GetComponentInChildren<TMPro.TextMeshProUGUI>().text=variant.chartData.DifficultyId.ToUpperInvariant();
                button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>Select(levels.IndexOf(variant)));
                button.GetComponent<Image>().color=variant.chartData.DifficultyId==chart.DifficultyId?new Color(.37f,.15f,.43f):CyberUI.Raised;
            }
            play.interactable=!loading && !chart.IsLegacy && chart.notes.Count>0 && item.audioClip!=null;
            if(chart.IsLegacy)status.text="Mapa de 7 teclas: clic derecho → editor → crear copia de 5 teclas.";
        }
        public void StartSelectedLevel()
        {
            if(loading||selectedIndex<0||selectedIndex>=levels.Count)return;
            var level=levels[selectedIndex];var chart=level.chartData;
            if(chart!=null&&chart.IsLegacy){status.text="Convierte una copia desde el editor antes de jugar.";return;}
            if(chart==null||chart.notes.Count==0||level.audioClip==null) { status.text="Mapa vacío o audio aún no disponible.";return; }
            loading=true;preview.Silence();chart.audioClip=level.audioClip;
            if(SocLearningFlow.Instance!=null&&chart.tutorialId!="sara-basics-5"){SocLearningFlow.Instance.Briefing(chart,()=>BeginLevel(level),()=>loading=false);return;}
            BeginLevel(level);
        }
        void BeginLevel(LevelItemInfo level)
        {
            var chart=level.chartData;
            GameplayUI.IsTestPlayFromEditor=false;TutorialGuide.Instance?.Request(chart);
            ChartManager.Instance.SelectChart(chart);
            chart.backgroundSprite=level.backgroundSprite ?? chart.backgroundSprite;BackgroundManager.Instance.SetBackground(chart.backgroundSprite);
            NoteSpawner.Instance.LoadChart(chart);
            AudioManager.Instance.StopSong();AudioManager.Instance.PlaySong(chart.audioClip,chart.PlaybackLeadIn);
            GameManager.Instance.StartGame();
        }
        public void BackToMainMenu() { preview?.Silence();MainMenuUI.Instance.BackToMainMenu(); }
        public void ShowMapContext(int index,Vector2 screenPosition)
        {
            if(loading||index<0||index>=levels.Count)return;CloseContext();
            var entry=levels[index];
            contextOverlay=CyberUI.Box(transform,"MapContext",Vector2.zero,Vector2.one,new Color(0,0,0,.3f)).gameObject;
            var dismiss=contextOverlay.AddComponent<Button>();dismiss.targetGraphic=contextOverlay.GetComponent<Image>();dismiss.targetGraphic.raycastTarget=true;dismiss.onClick.AddListener(CloseContext);
            var root=(RectTransform)contextOverlay.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root,screenPosition,null,out var point);
            float x=Mathf.Clamp(point.x,root.rect.xMin+12,root.rect.xMax-440),y=Mathf.Clamp(point.y,root.rect.yMin+190,root.rect.yMax-12);
            var panel=CyberUI.Box(root,"ContextCard",new Vector2(.5f,.5f),new Vector2(.5f,.5f),CyberUI.Panel);
            panel.rectTransform.pivot=new Vector2(0,1);panel.rectTransform.sizeDelta=new Vector2(420,176);panel.rectTransform.anchoredPosition=new Vector2(x,y);
            CyberUI.Label(panel.transform,entry.songTitle,new Vector2(.04f,.60f),new Vector2(.96f,.98f),23,CyberUI.Cyan);
            CyberUI.Button(panel.transform,"VER / EDITAR MAPA",new Vector2(.04f,.10f),new Vector2(.96f,.57f),()=>{CloseContext();preview.Silence();MainMenuUI.Instance.OpenChartInEditor(entry.chartData,entry.chartFilePath);});
        }
        public void CloseContext() { if(contextOverlay!=null){contextOverlay.SetActive(false);Destroy(contextOverlay);contextOverlay=null;} }
        void RefreshTrackList()
        {
            // Retain official chart instances and preview cache across panel visits.
            if(levels.Count==0)InitializeDefaultLevels();
            levels.RemoveAll(l=>l.levelTitle.StartsWith("[CUSTOM]"));LoadCustomChartsFromDisk();
            for(int i=0;i<levels.Count;i++)
            {
                var item=levels[i];DetectAndLoadArtwork(item);
                if(item.coverSprite==null)item.coverSprite=SongCoverFactory.Create(item.themeColor,i);
            }
        }
        public static Sprite LoadSpriteFromFile(string filePath)
        {
            if(string.IsNullOrEmpty(filePath)||!File.Exists(filePath))return null;
            try {var tex=new Texture2D(2,2);if(tex.LoadImage(File.ReadAllBytes(filePath)))return Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(.5f,.5f));Destroy(tex);}
            catch(Exception e){Debug.LogWarning(e.Message);}return null;
        }
        void DetectAndLoadArtwork(LevelItemInfo item)
        {
            if(item.coverSprite!=null||string.IsNullOrEmpty(item.audioClipPath))return;
            string folder=Path.GetDirectoryName(item.audioClipPath), name=Path.GetFileNameWithoutExtension(item.audioClipPath);
            foreach(string ext in new[]{".png",".jpg",".jpeg"})
            {
                item.coverSprite=LoadSpriteFromFile(Path.Combine(folder,name+ext))??LoadSpriteFromFile(Path.Combine(folder,"cover"+ext));
                item.backgroundSprite=LoadSpriteFromFile(Path.Combine(folder,name+"_bg"+ext));
                if(item.coverSprite!=null)break;
            }
        }
        private void InitializeDefaultLevels() { levels.AddRange(CampaignCatalog.CreateLevels()); }

        private void LoadCustomChartsFromDisk()
        {
            string customFolder = GameContentPaths.BundledChartsRoot;
            var paths = new List<string>(); if (Directory.Exists(customFolder)) paths.AddRange(Directory.GetFiles(customFolder, "*.json"));
            string userFolder = Path.Combine(Application.persistentDataPath, "Charts"); if (Directory.Exists(userFolder)) paths.AddRange(Directory.GetFiles(userFolder, "*.json"));

            string[] files = paths.ToArray();
            foreach (string file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    ChartData chart = ChartData.FromJson(json);
                    if (chart != null)
                    {
                        chart.audioClipPath=GameContentPaths.ResolveAudio(chart.audioClipPath,Path.GetDirectoryName(file));
                        var customItem = new LevelItemInfo
                        {
                            levelTitle = $"[CUSTOM] {chart.songTitle}",
                            songTitle = chart.songTitle,
                            artist = chart.artist,
                            description = "Pista creada por la comunidad mediante el Chart Editor de RhythmWare.",
                            bpm = chart.bpm,
                            noteCount = chart.notes != null ? chart.notes.Count : 0,
                            difficulty = chart.difficulty,
                            chartData = chart,
                            chartFilePath = Path.GetFullPath(file),
                            audioClipPath = chart.audioClipPath,
                            backgroundSprite = chart.backgroundSprite,
                            threatClassification = "MAPA COMUNITARIO // CHART PERSONALIZADO",
                            threatIcon = "[MAP]",
                            threatBadge = "[CUSTOM]",
                            threatTags = "<color=#00FF99>[COMMUNITY CHART]</color>",
                            themeColor = new Color(0.2f, 1f, 0.4f, 1f)
                        };
                        DetectAndLoadArtwork(customItem);
                        levels.Add(customItem);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LevelSelectUI] Error cargando chart custom {file}: {ex.Message}");
                }
            }
        }


    }
}

