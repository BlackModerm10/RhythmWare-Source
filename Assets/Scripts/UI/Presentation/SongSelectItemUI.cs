using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace RhythmWare.UI
{
    public sealed class SongSelectItemUI : MonoBehaviour,IPointerClickHandler
    {
        Image background, artwork, accent;
        TextMeshProUGUI text, fallback;
        Button button;
        Action select;
        Action<Vector2> context;
        float emphasis;
        public void Build()
        {
            background=gameObject.AddComponent<Image>();background.color=CyberUI.Panel;
            button=gameObject.AddComponent<Button>();button.targetGraphic=background;button.navigation=new Navigation{mode=Navigation.Mode.None};
            button.onClick.AddListener(()=>select?.Invoke());
            accent=CyberUI.Box(transform,"Accent",Vector2.zero,new Vector2(.008f,1),CyberUI.Cyan);
            artwork=CyberUI.Box(transform,"Cover",new Vector2(.025f,.12f),new Vector2(.16f,.88f),CyberUI.Raised);artwork.preserveAspect=true;
            fallback=CyberUI.Label(artwork.transform,"//",Vector2.zero,Vector2.one,35,CyberUI.Cyan);fallback.alignment=TextAlignmentOptions.Center;
            text=CyberUI.Label(transform,"",new Vector2(.18f,.03f),new Vector2(.98f,.97f),24);
        }
        public void Bind(LevelItemInfo item, bool selected, Action action,Action<Vector2> contextAction=null)
        {
            select=action;context=contextAction;background.color=selected?new Color(.37f,.15f,.43f):CyberUI.Panel;
            accent.color=selected?CyberUI.Cyan:CyberUI.Muted;
            emphasis=selected?1.018f:1;
            artwork.sprite=item.coverSprite;artwork.color=item.coverSprite!=null?Color.white:CyberUI.Raised;fallback.gameObject.SetActive(item.coverSprite==null);
            bool campaign=string.IsNullOrEmpty(item.chartFilePath);
            string metadata=campaign?$"{item.bpm:0.#} BPM / {(item.audioClip!=null?CyberUI.TimeLabel(item.audioClip.length):"--:--")}":$"{item.chartData.DifficultyId.ToUpperInvariant()} / {item.bpm:0.#} BPM / {item.chartData.notes.Count} NOTES / {(item.audioClip!=null?CyberUI.TimeLabel(item.audioClip.length):"--:--")}";
            text.text=$"<b>{item.songTitle}</b>\n<size=18><color=#D7C7F0>{item.levelTitle}</color></size>\n<size=17>{metadata}</size>";
        }
        void Update() { transform.localScale=Vector3.Lerp(transform.localScale,Vector3.one*emphasis,Time.unscaledDeltaTime*14); }
        public void OnPointerClick(PointerEventData data) { if(data.button==PointerEventData.InputButton.Right)context?.Invoke(data.position); }
    }
}
