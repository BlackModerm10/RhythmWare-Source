using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using RhythmWare.Core;

namespace RhythmWare.UI
{
    public sealed class ParticipantProfileUI : MonoBehaviour
    {
        Action close;bool welcome,busy,revealed;
        RectTransform roster;TMP_InputField alias,recovery;
        TextMeshProUGUI current,code,status;Button leave,restore;
        readonly List<GameObject> rows=new List<GameObject>();
        public void Build(Action back,bool firstTime)
        {
            close=back;welcome=firstTime;CandyUI.Backdrop(transform);
            CandyUI.Title(transform,welcome?"BIENVENIDO, OPERADOR":"TU PERFIL / TU IDENTIDAD",new Vector2(.04f,.87f),new Vector2(.95f,.97f),40);
            CyberUI.Label(transform,"Elige un alias. No necesitas correo ni contraseña.",new Vector2(.04f,.80f),new Vector2(.95f,.87f),24,CyberUI.Cyan);
            CyberUI.Label(transform,"PARTICIPANTES EN ESTE EQUIPO",new Vector2(.04f,.73f),new Vector2(.38f,.79f),20,CyberUI.Cyan);
            CyberUI.Scroll(transform,new Vector2(.04f,.205f),new Vector2(.38f,.72f),out roster);
            alias=CyberUI.Input(transform,"",new Vector2(.43f,.67f),new Vector2(.94f,.74f),null);alias.characterLimit=20;
            alias.placeholder=CyberUI.Label(alias.transform,"TU ALIAS / 3–20 CARACTERES",Vector2.zero,Vector2.one,20,CyberUI.Muted);alias.textComponent.richText=false;
            CyberUI.Label(transform,"Público: alias y mejores puntuaciones. Privado para el responsable de la beta: respuestas SOC e historial de intentos.",new Vector2(.43f,.56f),new Vector2(.94f,.66f),22,CyberUI.Muted);
            CyberUI.Button(transform,"CREAR PERFIL Y CONTINUAR",new Vector2(.43f,.48f),new Vector2(.74f,.55f),Create);
            CyberUI.Button(transform,"CAMBIAR ALIAS",new Vector2(.76f,.48f),new Vector2(.94f,.55f),Rename);
            var panel=CombatPanelGraphic.Create(transform,"ParticipantCard",new Vector2(.43f,.29f),new Vector2(.94f,.46f),CandyMenuGraphic.Mint);
            current=CyberUI.Label(panel.transform,"",new Vector2(.05f,.68f),new Vector2(.95f,.96f),26,CyberUI.Cyan);current.richText=false;
            code=CyberUI.Label(panel.transform,"Código privado: guárdalo para recuperar tu identidad en otro equipo.",new Vector2(.05f,.24f),new Vector2(.95f,.67f),19,CyberUI.Muted);code.richText=false;code.enableAutoSizing=true;code.fontSizeMin=13;
            CyberUI.Button(panel.transform,"MOSTRAR CÓDIGO",new Vector2(.05f,.025f),new Vector2(.48f,.22f),Reveal);
            CyberUI.Button(panel.transform,"COPIAR CÓDIGO",new Vector2(.51f,.025f),new Vector2(.95f,.22f),Copy);
            recovery=CyberUI.Input(transform,"",new Vector2(.43f,.205f),new Vector2(.77f,.27f),null);recovery.contentType=TMP_InputField.ContentType.Password;recovery.characterLimit=46;
            recovery.placeholder=CyberUI.Label(recovery.transform,"CÓDIGO DE RECUPERACIÓN",Vector2.zero,Vector2.one,18,CyberUI.Muted);
            restore=CyberUI.Button(transform,"RECUPERAR",new Vector2(.79f,.205f),new Vector2(.94f,.27f),Restore);
            status=CyberUI.Label(transform,"",new Vector2(.04f,.105f),new Vector2(.77f,.19f),21,CyberUI.Muted);
            CyberUI.Button(transform,"REINTENTAR CONEXIÓN",new Vector2(.79f,.11f),new Vector2(.94f,.18f),()=>{if(!busy)OnlineService.Instance?.Retry();});
            leave=CyberUI.Button(transform,welcome?"CONTINUAR":"VOLVER / ESC",new Vector2(.04f,.025f),new Vector2(.42f,.09f),Close);
            CyberUI.Label(transform,"El acceso se recuerda en este equipo. Un código no sincronizado todavía no permite recuperar el perfil en otro PC.",new Vector2(.45f,.025f),new Vector2(.94f,.09f),17,CyberUI.Muted);
            ParticipantProfiles.InfoChanged+=Refresh;Refresh();
        }
        void Refresh()
        {
            foreach(var row in rows){row.SetActive(false);Destroy(row);}rows.Clear();int i=0;
            foreach(var profile in ParticipantProfiles.All)
            {
                var selected=profile;var button=CyberUI.Button(roster,profile.PublicName,new Vector2(0,1),Vector2.one,()=>Try(()=>{ParticipantProfiles.Select(selected.localId);revealed=false;Refresh();}));
                var rect=(RectTransform)button.transform;rect.pivot=new Vector2(.5f,1);rect.sizeDelta=new Vector2(0,90);rect.anchoredPosition=new Vector2(0,-i++*104);
                var text=button.GetComponentInChildren<TextMeshProUGUI>();text.richText=false;
                button.GetComponent<Image>().color=ParticipantProfiles.Current?.localId==profile.localId?new Color(.37f,.15f,.43f):CyberUI.Raised;rows.Add(button.gameObject);
            }
            roster.sizeDelta=new Vector2(0,i*104);
            var p=ParticipantProfiles.Current;current.text=p==null?"TU PRIMER PERFIL":p.PublicName;
            leave.interactable=!welcome||p!=null;status.text=OnlineService.Instance?.Status??"La clasificación global todavía no está activada. Puedes jugar y guardar tus resultados.";
            if(revealed&&p!=null)Try(()=>code.text=ProfileVault.Load(p.localId).recoveryCode);
        }
        void Try(Action action){if(busy)return;try{action();}catch(Exception e){status.text=e is ArgumentException||e is InvalidOperationException||e is System.IO.IOException?e.Message:"No se pudo completar la operación. Tus datos se conservan.";}}
        void Create()=>Try(()=>{ParticipantProfiles.Create(alias.text);alias.SetTextWithoutNotify("");revealed=false;Refresh();if(welcome)Close();});
        void Rename()=>Try(()=>{ParticipantProfiles.Rename(alias.text);alias.SetTextWithoutNotify("");Refresh();});
        void Reveal()=>Try(()=>{var p=ParticipantProfiles.Current;if(p==null)return;revealed=true;code.text=ProfileVault.Load(p.localId).recoveryCode;status.text=p.registered?"Guarda este código en privado. Da acceso a tu identidad; no lo compartas en la leaderboard.":"Guarda el código. Servirá en otro equipo después de la primera sincronización online.";});
        void Copy()=>Try(()=>{var p=ParticipantProfiles.Current;if(p==null)return;GUIUtility.systemCopyBuffer=ProfileVault.Load(p.localId).recoveryCode;status.text="Código privado copiado. Guárdalo en un lugar seguro.";});
        void Restore()
        {
            if(busy)return;busy=true;restore.interactable=false;status.text="Recuperando perfil…";
            OnlineService.Instance.Recover(recovery.text.Trim(),(ok,message)=>{busy=false;if(this==null)return;restore.interactable=true;recovery.SetTextWithoutNotify("");revealed=false;Refresh();status.text=message;if(ok&&welcome)Close();});
        }
        void Close(){if(busy||welcome&&ParticipantProfiles.Current==null)return;revealed=false;close?.Invoke();}
        void Update(){if(!busy&&!CandyMenuTransition.IsRunning&&Keyboard.current?.escapeKey.wasPressedThisFrame==true)Close();}
        void OnDestroy(){ParticipantProfiles.InfoChanged-=Refresh;}
    }
}
