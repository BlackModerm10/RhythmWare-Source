using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using RhythmWare.Core;

namespace RhythmWare.UI
{
    public sealed class EditorFileDialog : MonoBehaviour
    {
        GameObject overlay;
        public bool IsOpen => overlay!=null;
        void Create(string title,string message)
        {
            Close();overlay=CyberUI.Box(transform,"Modal",Vector2.zero,Vector2.one,new Color(.055f,.02f,.12f,.93f)).gameObject;
            overlay.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            CyberUI.ModalOpen=true;
            var panel=CyberUI.Box(overlay.transform,"Dialog",new Vector2(.18f,.16f),new Vector2(.82f,.86f),CyberUI.Panel);
            CandyUI.Entrance(panel.gameObject);
            CyberUI.Label(panel.transform,title,new Vector2(.025f,.84f),new Vector2(.97f,.99f),30,CyberUI.Cyan);
            CyberUI.Label(panel.transform,message,new Vector2(.025f,.69f),new Vector2(.97f,.85f),20);
        }
        public void Confirm(string message,Action save,Action discard)
        {
            Create("CAMBIOS SIN GUARDAR",message);var panel=overlay.transform.GetChild(0);
            CyberUI.Button(panel,"GUARDAR",new Vector2(.05f,.08f),new Vector2(.34f,.19f),()=>{Close();save();});
            CyberUI.Button(panel,"DESCARTAR",new Vector2(.36f,.08f),new Vector2(.65f,.19f),()=>{Close();discard();});
            CyberUI.Button(panel,"CANCELAR",new Vector2(.67f,.08f),new Vector2(.96f,.19f),Close);
        }
        public void PathDialog(string title,string initialPath,IEnumerable<string> suggestions,Action<string> chosen)
        {
            Create(title,"Selecciona un archivo o introduce una ruta completa.");var panel=overlay.transform.GetChild(0);
            var input=CyberUI.Input(panel,initialPath,new Vector2(.03f,.57f),new Vector2(.97f,.69f),null);
            var scroll=CyberUI.Scroll(panel,new Vector2(.03f,.22f),new Vector2(.97f,.55f),out var content);
            int row=0;
            foreach(string entry in suggestions)
            {
                string captured=entry;var button=CyberUI.Button(content,Path.GetFileName(entry),Vector2.zero,Vector2.one,()=>input.SetTextWithoutNotify(captured));
                var rt=(RectTransform)button.transform;rt.anchorMin=new Vector2(0,1);rt.anchorMax=Vector2.one;rt.pivot=new Vector2(.5f,1);
                rt.sizeDelta=new Vector2(-8,42);rt.anchoredPosition=new Vector2(0,-row++*46);
            }
            content.sizeDelta=new Vector2(0,row*46);
            CyberUI.Button(panel,"ACEPTAR",new Vector2(.47f,.05f),new Vector2(.72f,.17f),()=>{string value=input.text;Close();chosen(value);});
            CyberUI.Button(panel,"CANCELAR",new Vector2(.73f,.05f),new Vector2(.98f,.17f),Close);
        }
        public void Overwrite(string path,Action yes)
        {
            Create("REEMPLAZAR ARCHIVO",path+"\nEl archivo ya existe.");var panel=overlay.transform.GetChild(0);
            CyberUI.Button(panel,"REEMPLAZAR",new Vector2(.47f,.05f),new Vector2(.72f,.17f),()=>{Close();yes();},CyberUI.Red);
            CyberUI.Button(panel,"CANCELAR",new Vector2(.73f,.05f),new Vector2(.98f,.17f),Close);
        }
        public void ConfirmConversion(string report,Action convert)
        {
            Create("MAPA ANTIGUO / CONVERTIR UNA COPIA",report);var panel=overlay.transform.GetChild(0);
            CyberUI.Button(panel,"CREAR COPIA DE 5 TECLAS",new Vector2(.04f,.10f),new Vector2(.66f,.24f),()=>{Close();convert();});
            CyberUI.Button(panel,"CANCELAR",new Vector2(.69f,.10f),new Vector2(.96f,.24f),Close);
        }
        public void Close() { if(overlay!=null){overlay.SetActive(false);Destroy(overlay);overlay=null;}CyberUI.ModalOpen=false; }
        void OnDisable() { Close(); }
    }
}
