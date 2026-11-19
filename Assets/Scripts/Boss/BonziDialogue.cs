namespace RhythmWare.Boss
{
    // Uses song time exclusively, so text, cooldowns and reactions freeze with gameplay.
    public sealed class BonziDialogue
    {
        public string Text { get; private set; }="";
        public double StartedAt { get; private set; }
        public double Until { get; private set; }
        int phase,priority,attackVariant,missVariant;
        double nextAmbient;
        bool overclockPending,overclockSaid,revivedPending,warningSaid,criticalSaid,containedSaid;
        public bool VisibleAt(double time)=>Text.Length>0&&time>=StartedAt&&time<Until;

        public void Reset()
        {
            Text="";StartedAt=Until=nextAmbient=0;phase=priority=attackVariant=missVariant=0;
            overclockPending=overclockSaid=revivedPending=warningSaid=criticalSaid=containedSaid=false;
        }
        public void Overclock() { if(!overclockSaid)overclockPending=true; }
        public void Revived() { revivedPending=true; }
        public void Refresh(double time,int currentPhase,float integrity)
        {
            if(integrity<=.001f)
            {
                if(!containedSaid){Say("¡Mi instalación...! Esto NO estaba en los términos y condiciones.",time,4);containedSaid=true;}
                return;
            }
            if(currentPhase!=phase)
            {
                phase=currentPhase;
                Say(phase==1?"¡Hola, amiguito! Solo necesito permiso para entrar en tu sistema...":phase==2?"¡Regalos para ti! Acepta mis anuncios. El botón de cerrar es opcional.":"Se acabó la amistad. ¡Abre la puerta a mis troyanos!",time,3);
            }
            if(overclockPending&&Say("¿OVERCLOCK? ¡Oye! Yo también quería usar ese procesador.",time,2)){overclockPending=false;overclockSaid=true;}
            if(revivedPending&&Say("¿Otra oportunidad? Vaya... sí que estudiaste esa respuesta.",time,2))revivedPending=false;
            if(integrity<=.33f&&!criticalSaid)
            {
                if(Say("¡No me desinstales! Prometo dejar solo UNA barrita de herramientas.",time,2)){criticalSaid=true;warningSaid=true;}
            }
            else if(integrity<=.66f&&!warningSaid&&Say("¡Eh! Ese núcleo es propiedad privada. ¡Deja de acertar!",time,2))warningSaid=true;
        }
        public bool Attack(string malware,double time)
        {
            if(containedSaid)return false;
            string line;
            switch(malware)
            {
                case "Spyware":line=attackVariant%2==0?"No es espionaje. Solo quiero conocerte un poquito mejor.":"Mira bien... mis amigos saben pasar desapercibidos.";break;
                case "Adware":line=attackVariant%2==0?"¡Has ganado mil ventanas gratis! De nada, amiguito.":"Esta oferta termina pronto. Tu firewall también.";break;
                case "Trojan":line=attackVariant%2==0?"Es un regalo totalmente seguro. ¡Ábrelo sin mirar dentro!":"¿Ves esa nota? La sorpresa viene escondida dentro.";break;
                default:return false;
            }
            if(!Say(line,time,1))return false;attackVariant++;return true;
        }
        public bool Miss(double time)
        {
            if(containedSaid)return false;
            if(!Say(missVariant%2==0?"¡Ups! Otro huequito para mi instalación.":"No te preocupes. Yo me encargo de tu firewall... je, je.",time,1))return false;
            missVariant++;return true;
        }
        bool Say(string text,double time,int importance)
        {
            if(VisibleAt(time)&&(importance<priority||(importance==priority&&importance<3)))return false;
            if(importance==1&&time<nextAmbient)return false;
            Text=text;StartedAt=time;Until=time+5.5;priority=importance;nextAmbient=time+10;
            return true;
        }
    }
}
