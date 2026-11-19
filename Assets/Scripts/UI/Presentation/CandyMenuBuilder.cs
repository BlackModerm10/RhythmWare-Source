using RhythmWare.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmWare.UI
{
    public static class CandyMenuBuilder
    {
        public static Button[] Build(Transform root,MainMenuUI controller)
        {
            CyberUI.Rect(root,"CyberCandyBackdrop",Vector2.zero,Vector2.one).gameObject.AddComponent<CandyMenuGraphic>();
            var logo=CyberUI.Rect(root,"ArcadeLogo",new Vector2(.055f,.38f),new Vector2(.57f,.85f));logo.localRotation=Quaternion.Euler(0,0,5);
            var shadow=CyberUI.Label(logo,"RHYTHM\nWARE",new Vector2(.018f,-.018f),new Vector2(1.018f,.982f),144,CandyMenuGraphic.Pink);shadow.fontStyle=FontStyles.Bold;shadow.lineSpacing=-12;
            var title=CyberUI.Label(logo,"RHYTHM\n<color=#49FFEB>WARE</color>",Vector2.zero,Vector2.one,144,CandyMenuGraphic.Cream);title.fontStyle=FontStyles.Bold;title.lineSpacing=-12;
            CyberUI.Label(root,"SIGUE EL PULSO.\nROMPE LA INFECCIÓN.",new Vector2(.065f,.29f),new Vector2(.55f,.42f),28,CandyMenuGraphic.Cream).fontStyle=FontStyles.Bold;
            var tag=CyberUI.Box(root,"BossAnnouncement",new Vector2(.065f,.095f),new Vector2(.54f,.16f),CandyMenuGraphic.Pink);
            CyberUI.Label(tag.transform,"NUEVO ENCUENTRO  /  BONZI BUDDY",new Vector2(.025f,0),new Vector2(.98f,1),20,CandyMenuGraphic.Ink).fontStyle=FontStyles.Bold;
            CyberUI.Label(root,"5 TECLAS   +   RITMO   +   CIBERSEGURIDAD",new Vector2(.065f,.048f),new Vector2(.57f,.09f),16,CandyMenuGraphic.Mint);
            CyberUI.Label(root,"ELIGE TU SIGUIENTE MOVIMIENTO",new Vector2(.62f,.85f),new Vector2(.965f,.92f),19,CandyMenuGraphic.Cream);
            string[] labels={"TRACK SELECT","MAPPING STUDIO","CYBER ACADEMY","SETTINGS","EXIT"};
            string[] descriptions={"Entra a la red y elige una canción","Crea, abre y transforma tus mapas","Entrena tu defensa SOC","Pon el juego a tu ritmo","Nos vemos en el siguiente beat"};
            Color[] colors={CandyMenuGraphic.Pink,CandyMenuGraphic.Mint,new Color(.76f,.57f,1),new Color(1,.86f,.40f),CandyMenuGraphic.Cream};
            var result=new Button[5];
            for(int i=0;i<5;i++)
            {
                int index=i;float top=.83f-i*.139f;
                var shadowBox=CyberUI.Box(root,"MenuShadow"+i,new Vector2(.637f,top-.121f),new Vector2(.967f,top-.008f),new Color(.035f,.015f,.09f));
                var b=CyberUI.Button(root,labels[i],new Vector2(.625f,top-.11f),new Vector2(.955f,top),null);
                // The custom typography owns these labels.
                foreach(var label in b.GetComponentsInChildren<TextMeshProUGUI>())Object.Destroy(label.gameObject);
                b.gameObject.AddComponent<CandyMenuButton>().Initialize(i,labels[i],descriptions[i],colors[i],()=>controller.FocusMenu(index));result[i]=b;
            }
            CyberUI.Label(root,"W / S o FLECHAS  ·  ENTER PARA ELEGIR",new Vector2(.62f,.06f),new Vector2(.97f,.12f),16,CandyMenuGraphic.Mint);
            return result;
        }
    }
}
