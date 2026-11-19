using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using RhythmWare.Core;

namespace RhythmWare.UI
{
    public sealed class ChartLibraryUI : MonoBehaviour
    {
        GameObject overlay;
        RectTransform list;
        TextMeshProUGUI details;
        List<LevelItemInfo> entries;
        LevelItemInfo selected;
        Action<LevelItemInfo> chosen;
        public bool IsOpen => overlay!=null;
        public void Show(LevelSelectUI selector,Action<LevelItemInfo> open,Action openJson)
        {
            Close();chosen=open;entries=selector.RefreshCatalog().ToList();
            overlay=CyberUI.Box(transform,"ChartLibrary",Vector2.zero,Vector2.one,CyberUI.Background).gameObject;
            CandyUI.Entrance(overlay);
            overlay.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;CyberUI.ModalOpen=true;
            CyberUI.Label(overlay.transform,"MAPPING STUDIO / BIBLIOTECA DE MAPAS",new Vector2(.04f,.87f),new Vector2(.89f,.96f),30,CyberUI.Cyan);
            CyberUI.Label(overlay.transform,"Temas oficiales y mapas personalizados. Los oficiales se abren como una copia editable.",new Vector2(.04f,.80f),new Vector2(.95f,.87f),21,CyberUI.Muted);
            var search=CyberUI.Input(overlay.transform,"",new Vector2(.04f,.72f),new Vector2(.58f,.79f),Filter);
            search.placeholder=CyberUI.Label(search.transform,"Buscar título, artista o dificultad…",Vector2.zero,Vector2.one,19,CyberUI.Muted);
            CyberUI.Scroll(overlay.transform,new Vector2(.04f,.15f),new Vector2(.58f,.71f),out list);
            var side=CyberUI.Box(overlay.transform,"ChartDetails",new Vector2(.61f,.25f),new Vector2(.96f,.79f),CyberUI.Panel);
            details=CyberUI.Label(side.transform,"Selecciona un mapa.",new Vector2(.05f,.05f),new Vector2(.95f,.95f),25);
            CyberUI.Button(overlay.transform,"ABRIR EN EL EDITOR",new Vector2(.61f,.15f),new Vector2(.96f,.24f),OpenSelected,new Color(.37f,.15f,.43f));
            CyberUI.Button(overlay.transform,"ABRIR JSON POR RUTA",new Vector2(.04f,.045f),new Vector2(.32f,.12f),()=>{Close();openJson();});
            CyberUI.Button(overlay.transform,"CERRAR / ESC",new Vector2(.72f,.045f),new Vector2(.96f,.12f),Close);
            Filter("");
        }
        void Filter(string query)
        {
            foreach(Transform child in list){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            var filtered=entries.Where(e=>(e.songTitle+" "+e.artist+" "+e.chartData.DifficultyId).IndexOf(query??"",StringComparison.OrdinalIgnoreCase)>=0).ToList();
            for(int i=0;i<filtered.Count;i++)
            {
                var entry=filtered[i];string kind=entry.chartData.IsLegacy?"7 TECLAS / CONVERTIR COPIA":string.IsNullOrEmpty(entry.chartFilePath)?"OFICIAL · COPIA EDITABLE":"PERSONALIZADO";
                var button=CyberUI.Button(list,$"{entry.songTitle}   /   {entry.chartData.DifficultyId}\n{kind}   ·   {entry.chartData.notes.Count} notas",Vector2.zero,Vector2.one,()=>Select(entry));
                var label=button.GetComponentInChildren<TextMeshProUGUI>();label.textWrappingMode=TextWrappingModes.Normal;
                var rt=(RectTransform)button.transform;rt.anchorMin=new Vector2(0,1);rt.anchorMax=Vector2.one;rt.pivot=new Vector2(.5f,1);rt.anchoredPosition=new Vector2(0,-i*88);rt.sizeDelta=new Vector2(-12,80);
            }
            list.sizeDelta=new Vector2(0,filtered.Count*88);list.anchoredPosition=Vector2.zero;
            selected=null;details.text=filtered.Count==0?"No hay mapas que coincidan.":"Selecciona un mapa.";if(filtered.Count>0)Select(filtered[0]);
        }
        void Select(LevelItemInfo entry)
        {
            selected=entry;var c=entry.chartData;
            details.text=$"<color=#49FFEB><b>{c.songTitle}</b></color>\n{c.artist}\n\n{c.DifficultyId} · {c.bpm:0.#} BPM\n{c.notes.Count} notas · {c.notes.Count(n=>n.isHoldNote)} holds\n\n"+
                (string.IsNullOrEmpty(entry.chartFilePath)?"Se creará una copia independiente. Guarda tu versión con Save As.":"Abre el JSON existente. Los cambios solo se escriben al guardar.")+"\n\nAudio: "+(string.IsNullOrEmpty(c.audioClipPath)?"sin asignar":System.IO.Path.GetFileName(c.audioClipPath));
        }
        public void OpenSelected() { if(selected==null)return;var entry=selected;Close();chosen(entry); }
        public void Close() { if(overlay!=null){overlay.SetActive(false);Destroy(overlay);overlay=null;}CyberUI.ModalOpen=false; }
        void OnDisable() { Close(); }
    }
}
