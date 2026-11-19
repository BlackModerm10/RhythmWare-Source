using UnityEngine;

namespace RhythmWare.UI
{
    // A fictional in-game desktop. These windows, files and cursor are scenery;
    // the game never opens OS dialogs or interacts with real user files.
    public sealed class BonziDesktopEnvironment : MonoBehaviour
    {
        EnvironmentDrawing desk,bar;
        double clock,attackAt=-100,counterAt=-100;
        int phase=1;
        float integrity=1;
        bool contained,ready;
        string actorAction="idle";
        public bool Visible { get; private set; }
        public int AttackSerial { get; private set; }
        public int CounterSerial { get; private set; }
        public string LastThreat { get; private set; }="Spyware";
        public int Phase=>phase;
        public bool Contained=>contained;
        public Vector2 CursorPosition { get; private set; }
        public bool TaskbarVisible=>bar!=null&&bar.Visible;
        static readonly Color Grey=new Color(.59f,.59f,.59f),White=new Color(.96f,.96f,.96f),Black=new Color(.012f,.012f,.022f);
        public void Initialize(){desk=new EnvironmentDrawing(transform,"BonziRetroDesktop",-94);bar=new EnvironmentDrawing(transform,"BonziDesktopTaskbar",-60);}
        public void SetVisible(bool value){Visible=value;desk.Visible=bar.Visible=value;}
        public void ResetEncounter(){AttackSerial=CounterSerial=0;phase=1;integrity=1;contained=ready=false;LastThreat="Spyware";actorAction="idle";attackAt=counterAt=-100;}
        public void ActorAction(string action){actorAction=action;}
        string ActorStatus=>contained?"Proceso aislado":actorAction=="scan"?"Consultando documentos...":actorAction=="present"||actorAction=="phase"?"Preparando recomendaciones...":actorAction=="dance"?"Escuchando tu música":actorAction=="stunned"?"La defensa ha bloqueado el acceso":actorAction=="talk"?"Bonzi quiere hablar contigo":"Tu nuevo mejor amigo";
        public void Attack(string threat)
        {
            if(contained)return;LastThreat=threat;AttackSerial++;
            if(clock-attackAt>.4)attackAt=clock;
        }
        public void Counter(){CounterSerial++;counterAt=clock;}
        public void Overclock(bool active){ready=active;if(active)counterAt=clock;}
        public void Status(int nextPhase,float core,bool isolated){phase=Mathf.Clamp(nextPhase,1,3);integrity=Mathf.Clamp01(core);contained=isolated;}
        public void Render(double time,bool reduced)
        {
            clock=time;if(!Visible||Camera.main==null)return;desk.Begin();bar.Begin();
            DrawDesktopIcons();
            var app=new Rect(620,83,680,310);Window(app,"BonziBuddy.exe",true,new Color(.66f,.74f,.88f));
            desk.Label(ActorStatus,new Vector2(636,373),15,Black);
            // Corner windows live behind the opaque highway and its exits.
            Window(new Rect(30,268,440,190),"Conexiones de Bonzi",false,new Color(.055f,.075f,.075f));
            desk.Label(phase==1?"Privacidad: acceso solicitado":phase==2?"Anuncios: ventanas en cola":"Actualización: origen no confiable",new Vector2(48,319),18,new Color(.37f,1,.62f),true);
            desk.Label("Destino: bonzi / sesión local",new Vector2(48,351),16,White);
            desk.Label("Actividad: "+(contained?"AISLADA":LastThreat.ToUpperInvariant()),new Vector2(48,381),16,White);
            Window(new Rect(1470,290,420,178),contained?"Centro de seguridad":"Asistente de escritorio",true,new Color(.84f,.82f,.72f));
            desk.Label(contained?"Amenaza contenida":phase==1?"Permitir acceso a tus datos?":phase==2?"Ofertas personalizadas de Bonzi":"Verificar bonzi_update.exe",new Vector2(1488,340),18,Black,true);
            desk.Label(contained?"Mantén activo el firewall.":"No confíes solo en su apariencia.",new Vector2(1488,375),16,Black);
            float age=(float)(clock-attackAt),counterAge=(float)(clock-counterAt);
            bool attacking=age>=0&&age<2&&!contained;
            if(phase>=2&&!contained)
            {
                int count=phase==2?2:3;for(int i=0;i<count;i++)
                {
                    float open=reduced?1:Mathf.SmoothStep(0,1,Mathf.Clamp01(age*3-i*.12f));if(!attacking)open=1;
                    if(ready||counterAge<.7f)continue;
                    Rect r=new Rect(1450+i*30,530+i*125,390,100*open);if(r.height<32)continue;
                    Window(r,phase==2?"OFERTA DE BONZI!":"Windows - Alerta de seguridad",true,new Color(.84f,.82f,.72f));
                    if(open>.9f)desk.Label(phase==2?"GRATIS! Instala mi ayudante":"Editor desconocido / proceso bloqueado",new Vector2(r.x+15,r.y+49),16,Black,true);
                }
            }
            // A moving system cursor makes each malware cast originate from
            // Bonzi's window and visibly target a desktop action.
            Vector2 target=LastThreat=="Adware"?new Vector2(1670,565):LastThreat=="Trojan"?new Vector2(1695,817):new Vector2(535,285);
            float travel=reduced?1:Mathf.SmoothStep(0,1,Mathf.Clamp01(age/.65f));
            CursorPosition=Vector2.Lerp(new Vector2(1090,265),target,travel);
            if(attacking)
            {
                if(!reduced)for(int i=0;i<16;i++)
                {
                    float p=i/16f;if(p>travel)break;Vector2 pos=Vector2.Lerp(new Vector2(1090,265),target,p);desk.Quad(new Rect(pos.x,pos.y,3,3),new Color(.52f,.78f,1,.35f));
                }
                Cursor(CursorPosition);
                if(age>.65f&&!reduced)desk.Ring(target,12+(age-.65f)*16,2,new Color(.48f,1,.76f,Mathf.Clamp01(1-(age-.65f))));
            }
            // Classic taskbar and two process buttons remain below the HUD.
            bar.Gradient(new Rect(0,1046,1920,34),new Color(.015f,.08f,.72f),new Color(.025f,.22f,.79f));
            bar.Line(new Vector2(0,1047),new Vector2(1920,1047),3,new Color(.17f,.47f,1));
            bar.RoundedRect(new Rect(1,1048,115,31),10,new Color(.025f,.36f,.055f));bar.Line(new Vector2(14,1050),new Vector2(102,1050),2,new Color(.36f,.71f,.30f));bar.Label("Inicio",new Vector2(26,1055),19,White,true);
            TaskButton(new Rect(124,1050,250,25),"BonziBuddy.exe",true);TaskButton(new Rect(383,1050,280,25),contained?"SOC / proceso aislado":"Monitor SOC",false);
            bar.Quad(new Rect(1640,1048,280,32),new Color(.025f,.34f,.73f));bar.Label($"RAM {(contained?12:34+phase*14)}%    {Mathf.FloorToInt((float)clock/60):00}:{Mathf.FloorToInt((float)clock%60):00}",new Vector2(1670,1055),16,White);
            if(ready||contained||counterAge<.5f)
            {
                Color tint=contained||ready?new Color(.36f,1,.67f):new Color(.60f,.80f,1);
                desk.Line(new Vector2(app.x,app.yMax),new Vector2(app.xMax,app.yMax),4,tint);
                desk.Label(contained?"Núcleo aislado":ready?"Defensa OVERCLOCK activa":"Paquete interceptado",new Vector2(1070,373),15,new Color(.015f,.12f,.17f),true);
            }
            desk.End();bar.End();
        }
        void TaskButton(Rect r,string title,bool selected)
        {
            bar.RoundedRect(r,4,selected?new Color(.005f,.045f,.38f):new Color(.045f,.20f,.67f));
            bar.Line(new Vector2(r.x+5,r.y+2),new Vector2(r.xMax-5,r.y+2),1,new Color(.14f,.40f,.95f));
            bar.Label(title,new Vector2(r.x+14,r.y+5),16,White,selected);
        }
        void DrawDesktopIcons()
        {
            Icon(new Vector2(535,275),"Mis documentos",true);Icon(new Vector2(1830,210),"Mi PC",false);
            Icon(new Vector2(1830,850),"Papelera",false);
        }
        void Icon(Vector2 p,string caption,bool folder)
        {
            if(folder){desk.Quad(new Rect(p.x-20,p.y-8,20,8),new Color(.95f,.65f,.12f));desk.Gradient(new Rect(p.x-24,p.y,48,31),new Color(.99f,.87f,.37f),new Color(.85f,.53f,.08f));desk.Line(new Vector2(p.x-23,p.y+1),new Vector2(p.x+23,p.y+1),2,White);}
            else{desk.RoundedRect(new Rect(p.x-24,p.y-10,48,35),4,new Color(.60f,.72f,.85f));desk.Quad(new Rect(p.x-19,p.y-5,38,23),new Color(.025f,.28f,.82f));desk.Quad(new Rect(p.x-19,p.y+9,38,9),new Color(.23f,.61f,.11f));desk.Quad(new Rect(p.x-9,p.y+25,18,6),Grey);}
            desk.RoundedRect(new Rect(p.x-51,p.y+37,137,23),3,new Color(.015f,.035f,.12f,.5f));desk.Label(caption,new Vector2(p.x-46,p.y+40),15,White);
        }
        void Window(Rect r,string title,bool xp,Color body)
        {
            desk.RoundedRect(new Rect(r.x+4,r.y+6,r.width,r.height),8,new Color(0,0,0,.28f));
            desk.RoundedRect(r,8,new Color(.005f,.035f,.36f));desk.RoundedRect(new Rect(r.x+2,r.y+2,r.width-4,r.height-4),7,xp?new Color(.025f,.16f,.78f):new Color(.04f,.10f,.37f));
            Rect inner=new Rect(r.x+5,r.y+35,r.width-10,r.height-40);if(inner.height>0)desk.Quad(inner,body);
            desk.Gradient(new Rect(r.x+4,r.y+4,r.width-8,29),xp?new Color(.025f,.12f,.78f):new Color(.02f,.065f,.28f),xp?new Color(.035f,.30f,.90f):new Color(.07f,.16f,.46f));
            desk.Line(new Vector2(r.x+11,r.y+3),new Vector2(r.xMax-11,r.y+3),2,new Color(.23f,.52f,1));
            desk.Label(title,new Vector2(r.x+12,r.y+10),18,White,true);
            for(int i=0;i<3;i++){float x=r.xMax-29-i*25;desk.RoundedRect(new Rect(x,r.y+6,22,23),4,White);desk.Gradient(new Rect(x+1,r.y+7,20,21),i==0?new Color(.83f,.16f,.045f):new Color(.04f,.23f,.81f),i==0?new Color(.65f,.045f,.015f):new Color(.015f,.10f,.56f));desk.Label(i==0?"x":i==1?"□":"_",new Vector2(x+6,r.y+10),14,White,true);}
        }
        void Bevel(Rect r,Color fill,EnvironmentDrawing target=null)
        {var draw=target??desk;draw.Quad(r,Black);draw.Quad(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),White);draw.Quad(new Rect(r.x+3,r.y+3,r.width-4,r.height-4),fill);draw.Line(new Vector2(r.x+2,r.yMax-2),new Vector2(r.xMax-2,r.yMax-2),2,new Color(.20f,.20f,.23f));}
        void Cursor(Vector2 p)
        {
            desk.Polygon(new[]{p,p+new Vector2(0,28),p+new Vector2(7,22),p+new Vector2(13,34),p+new Vector2(18,31),p+new Vector2(12,20),p+new Vector2(23,20)},Black);
            desk.Polygon(new[]{p+new Vector2(2,5),p+new Vector2(2,23),p+new Vector2(8,18),p+new Vector2(13,29),p+new Vector2(15,28),p+new Vector2(10,17),p+new Vector2(18,18)},White);
        }
        void OnDestroy(){desk?.Dispose();bar?.Dispose();}
    }
}
