using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using RhythmWare.Malware;

namespace RhythmWare.UI
{
    public sealed class ExtraContentUI : MonoBehaviour
    {
        readonly List<MalwareData> catalog=new List<MalwareData>();
        readonly List<Button> malwareButtons=new List<Button>();
        readonly GameObject[] panels=new GameObject[4];
        readonly Button[] tabs=new Button[4];
        TextMeshProUGUI threatTitle,category,description,prevention,incident;
        int selectedMalwareIndex,currentTabIndex=1;
        bool built;
        public CyberAcademyUI Academy { get; private set; }
        public int CurrentTab => currentTabIndex;
        public int MalwareCount => catalog.Count;
        public void Build()
        {
            if(built)return;built=true;
            CandyUI.Backdrop(transform);
            CyberUI.Box(transform,"TopAccent",new Vector2(0,.99f),Vector2.one,CyberUI.Cyan);
            CyberUI.Label(transform,"RHYTHMWARE / CENTRO DE FORMACIÓN SOC",new Vector2(.035f,.91f),new Vector2(.70f,.97f),18,CyberUI.Cyan);
            CandyUI.Title(transform,"Cyber Academy",new Vector2(.035f,.835f),new Vector2(.70f,.93f),48);
            CyberUI.Label(transform,"Identifica la amenaza. Aprende el protocolo. Defiende tu sesión.",new Vector2(.035f,.795f),new Vector2(.75f,.84f),21,CyberUI.Muted);
            CyberUI.Button(transform,"Volver al menú  /  Esc",new Vector2(.79f,.865f),new Vector2(.965f,.935f),()=>MainMenuUI.Instance.BackToMainMenu());
            string[] names={"Biblioteca de amenazas","Práctica SOC","Expedientes de jefes","Acerca de RhythmWare"};
            for(int i=0;i<4;i++)
            {
                int index=i;float left=.035f+i*.235f;
                tabs[i]=CyberUI.Button(transform,names[i],new Vector2(left,.71f),new Vector2(left+.23f,.775f),()=>SwitchTab(index));
                panels[i]=CyberUI.Rect(transform,"AcademySection"+i,new Vector2(.035f,.055f),new Vector2(.965f,.68f)).gameObject;
            }
            Academy=panels[1].AddComponent<CyberAcademyUI>();Academy.Build();
            BuildLibrary(panels[0].transform);
            var bosses=CyberUI.Box(panels[2].transform,"BossDossiers",Vector2.zero,Vector2.one,CyberUI.Panel);
            BuildBossDossier(bosses.transform);
            var credits=CyberUI.Box(panels[3].transform,"Credits",Vector2.zero,Vector2.one,CyberUI.Panel);
            CyberUI.Label(credits.transform,"RYTHMWARE / BETA",new Vector2(.05f,.80f),new Vector2(.95f,.94f),22,CyberUI.Cyan);
            CandyUI.Title(credits.transform,"Defiende la red. Aprende al ritmo.",new Vector2(.05f,.57f),new Vector2(.95f,.79f),42);
            CyberUI.Label(credits.transform,"Una experiencia de ritmo y ciberseguridad: reconoce amenazas, protege tu firewall y lleva lo aprendido a situaciones cotidianas.\n\nEsta es una versión beta. Tu experiencia y tus comentarios nos ayudarán a mejorar la dificultad, la claridad y el aprendizaje.",new Vector2(.05f,.13f),new Vector2(.90f,.55f),27);
            SwitchTab(1);
        }
        RhythmWare.Boss.BonziAnimations dossierArt;
        void BuildBossDossier(Transform parent)
        {
            CyberUI.Label(parent,"EXPEDIENTE / EL AMIGO QUE PIDIÓ DEMASIADO",new Vector2(.04f,.88f),new Vector2(.96f,.97f),20,CyberUI.Magenta);
            CandyUI.Title(parent,"BONZI BUDDY",new Vector2(.04f,.70f),new Vector2(.61f,.89f),44);
            var history=CyberUI.Label(parent,"Un asistente virtual con apariencia amistosa en los escritorios de la época de Windows 98 y XP. Su historia recuerda que una mascota simpática no garantiza privacidad. En 2004, Bonzi Software acordó resolver cargos de la FTC por recopilar datos de menores sin consentimiento parental.",new Vector2(.04f,.43f),new Vector2(.61f,.70f),24);
            var tactics=CyberUI.Label(parent,"EN RYTHMWARE\n<b>Spyware</b> representa la invasión de privacidad. <b>Adware</b> convierte la publicidad en distracción. <b>Trojan</b> usa regalos aparentemente útiles para ocultar una amenaza.\n\nEstos ataques son una adaptación ficticia para aprender a desconfiar de permisos, anuncios y descargas engañosas; no una descripción técnica del programa original.",new Vector2(.04f,.07f),new Vector2(.61f,.42f),23);
            foreach(var label in new[]{history,tactics}){label.enableAutoSizing=true;label.fontSizeMin=17;label.fontSizeMax=label.fontSize;}
            var portrait=CombatPanelGraphic.Create(parent,"BonziPortrait",new Vector2(.66f,.17f),new Vector2(.96f,.85f),CandyMenuGraphic.Pink);
            dossierArt=new RhythmWare.Boss.BonziAnimations();
            if(dossierArt.Available)
            {
                var frame=dossierArt.Frame("idle",0);var rect=frame.rect;var bounds=dossierArt.VisibleBounds("idle");
                // Crop transparent atlas padding so the portrait fills the card.
                var crop=new Rect(rect.x+bounds.x,rect.y+rect.height-bounds.yMax,bounds.width,bounds.height);
                dossierPortrait=Sprite.Create(frame.texture,crop,new Vector2(.5f,.5f),100);
                var image=CyberUI.Box(portrait.transform,"BossImage",new Vector2(.08f,.13f),new Vector2(.92f,.94f),Color.white);
                image.sprite=dossierPortrait;image.material=dossierArt.Material;image.preserveAspect=true;
            }
            CyberUI.Label(portrait.transform,"¿CONFÍAS EN MÍ?",new Vector2(.07f,.02f),new Vector2(.93f,.13f),22,CyberUI.Magenta).alignment=TextAlignmentOptions.Center;
            CyberUI.Button(parent,"FUENTE HISTÓRICA / FTC, 2004",new Vector2(.66f,.055f),new Vector2(.96f,.14f),()=>Application.OpenURL("https://www.ftc.gov/news-events/news/press-releases/2004/02/umg-recordings-inc-pay-400000-bonzi-software-inc-pay-75000-settle-coppa-civil-penalty-charges"));
        }
        Sprite dossierPortrait;
        void BuildLibrary(Transform parent)
        {
            catalog.Add(ScriptableObject.CreateInstance<SpywareData>());catalog.Add(ScriptableObject.CreateInstance<RansomwareData>());catalog.Add(ScriptableObject.CreateInstance<TrojanData>());
            catalog.Add(ScriptableObject.CreateInstance<AdwareData>());catalog.Add(ScriptableObject.CreateInstance<DDoSData>());
            var side=CyberUI.Box(parent,"ThreatList",Vector2.zero,new Vector2(.255f,1),CyberUI.Panel);
            CyberUI.Label(side.transform,"5 TEMAS / CUATRO MALWARE + DDoS",new Vector2(.05f,.88f),new Vector2(.96f,.98f),18,CyberUI.Cyan);
            for(int i=0;i<catalog.Count;i++)
            {
                int index=i;float top=.87f-i*.145f;
                malwareButtons.Add(CyberUI.Button(side.transform,catalog[i].malwareName,new Vector2(.035f,top-.125f),new Vector2(.965f,top),()=>{selectedMalwareIndex=index;UpdateMalwareDisplay();}));
            }
            CyberUI.Scroll(parent,new Vector2(.28f,0),Vector2.one,out var content);content.sizeDelta=new Vector2(0,840);
            threatTitle=CyberUI.Label(content,"",new Vector2(.035f,.85f),new Vector2(.97f,.97f),44,CyberUI.Cyan);
            category=CyberUI.Label(content,"",new Vector2(.035f,.78f),new Vector2(.97f,.85f),22,CyberUI.Muted);
            CyberUI.Label(content,"IDENTIFICA EL ATAQUE",new Vector2(.035f,.68f),new Vector2(.97f,.75f),18,CyberUI.Cyan);
            description=CyberUI.Label(content,"",new Vector2(.035f,.48f),new Vector2(.97f,.68f),25);
            CyberUI.Label(content,"PROTOCOLO DE PREVENCIÓN",new Vector2(.035f,.40f),new Vector2(.97f,.47f),18,CyberUI.Cyan);
            prevention=CyberUI.Label(content,"",new Vector2(.035f,.25f),new Vector2(.97f,.40f),24);
            CyberUI.Label(content,"CASO HISTÓRICO",new Vector2(.035f,.17f),new Vector2(.97f,.24f),18,CyberUI.Magenta);
            incident=CyberUI.Label(content,"",new Vector2(.035f,.015f),new Vector2(.97f,.17f),23,CyberUI.Muted);
            foreach(var label in new[]{description,prevention,incident}){label.enableAutoSizing=true;label.fontSizeMin=18;label.overflowMode=TextOverflowModes.Overflow;}
            UpdateMalwareDisplay();
        }
        public void SwitchTab(int index)
        {
            if(!built || index<0 || index>=panels.Length)return;currentTabIndex=index;
            for(int i=0;i<panels.Length;i++){panels[i].SetActive(i==index);if(i==index)CandyUI.Entrance(panels[i]);tabs[i].GetComponent<Image>().color=i==index?new Color(.37f,.15f,.43f):CyberUI.Raised;}
        }
        public void OnPrevClicked() { selectedMalwareIndex=(selectedMalwareIndex-1+catalog.Count)%catalog.Count;UpdateMalwareDisplay(); }
        public void OnNextClicked() { selectedMalwareIndex=(selectedMalwareIndex+1)%catalog.Count;UpdateMalwareDisplay(); }
        void UpdateMalwareDisplay()
        {
            var data=catalog[selectedMalwareIndex];threatTitle.text=data.malwareName;category.text=data.categoryName;description.text=data.educationalDescription;prevention.text=data.preventionTips;incident.text=data.historicalIncident;
            for(int i=0;i<malwareButtons.Count;i++)malwareButtons[i].GetComponent<Image>().color=i==selectedMalwareIndex?new Color(.37f,.15f,.43f):CyberUI.Raised;
        }
        void Update()
        {
            if(CandyMenuTransition.IsRunning)return;
            var key=Keyboard.current;if(key==null)return;
            if(key.escapeKey.wasPressedThisFrame)MainMenuUI.Instance.BackToMainMenu();
            if(key.qKey.wasPressedThisFrame)SwitchTab((currentTabIndex+3)%4);
            else if(key.eKey.wasPressedThisFrame)SwitchTab((currentTabIndex+1)%4);
            // Number keys belong exclusively to the question when practicing.
            if(currentTabIndex==0){if(key.leftArrowKey.wasPressedThisFrame)OnPrevClicked();if(key.rightArrowKey.wasPressedThisFrame)OnNextClicked();}
        }
        void OnDestroy() { dossierArt?.Dispose();if(dossierPortrait!=null)Destroy(dossierPortrait);foreach(var data in catalog)if(data!=null)Destroy(data); }
    }
}
