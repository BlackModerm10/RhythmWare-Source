using RhythmWare.Core;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace RhythmWare.UI
{
    // Shared design tokens and typed UI construction. All coordinates live in a
    // 1920x1080 reference canvas; panels use proportional anchors.
    public static class CyberUI
    {
        public static bool ModalOpen { get; set; }
        public static readonly Color Background = new Color(.095f,.045f,.21f);
        public static readonly Color Panel = new Color(.14f,.075f,.25f,.98f);
        public static readonly Color Raised = new Color(.22f,.12f,.36f);
        public static readonly Color Cyan = CandyMenuGraphic.Mint;
        public static readonly Color Muted = new Color(.80f,.73f,.89f);
        public static readonly Color Magenta = CandyMenuGraphic.Pink;
        public static readonly Color Red = new Color(1f, .26f, .36f);
        public static readonly string[] LaneNames = LaneLayout.Names;
        public static readonly int[] LaneOrder = LaneLayout.VisualOrder;
        static TMP_FontAsset font;
        public static TMP_FontAsset Font
        {
            get
            {
                if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (font == null) font = TMP_FontAsset.CreateFontAsset(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
                return font;
            }
        }
        public static bool Typing
        {
            get
            {
                var go = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                if(go==null)return false;
                var tmp=go.GetComponent<TMP_InputField>();var legacy=go.GetComponent<InputField>();
                return (tmp!=null&&tmp.isFocused)||(legacy!=null&&legacy.isFocused);
            }
        }
        public static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max, float inset = 0)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false); rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.one * inset; rt.offsetMax = Vector2.one * -inset;
            return rt;
        }
        public static Image Box(Transform parent, string name, Vector2 min, Vector2 max, Color color, float inset = 0)
        {
            var img = Rect(parent, name, min, max, inset).gameObject.AddComponent<Image>();
            img.color = color; img.raycastTarget = false; return img;
        }
        public static TextMeshProUGUI Label(Transform parent, string text, Vector2 min, Vector2 max, float size = 22, Color? color = null)
        {
            var label = Rect(parent, "Label", min, max, 6).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = Font; label.text = text; label.fontSize = size; label.color = color ?? CandyMenuGraphic.Cream;
            label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }
        public static Button Button(Transform parent, string title, Vector2 min, Vector2 max, Action action, Color? color = null)
        {
            var img = Box(parent, title, min, max, color ?? Raised, 4); img.raycastTarget = true;
            var button = img.gameObject.AddComponent<Button>(); button.targetGraphic = img;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var c = button.colors; c.highlightedColor = new Color(1,.83f,.97f); c.pressedColor = new Color(.80f,.60f,.90f); button.colors = c;
            var label=Label(img.transform, title, Vector2.zero, Vector2.one, 20);
            label.alignment=TextAlignmentOptions.Center;label.textWrappingMode=TextWrappingModes.NoWrap;
            label.enableAutoSizing=true;label.fontSizeMin=12;label.fontSizeMax=20;
            button.gameObject.AddComponent<CandyButtonFeedback>();
            button.onClick.AddListener(() => action?.Invoke()); return button;
        }
        public static TMP_InputField Input(Transform parent, string initial, Vector2 min, Vector2 max, Action<string> changed)
        {
            var bg = Box(parent, "Input", min, max, Raised, 4); bg.raycastTarget = true;
            var label = Label(bg.transform, "", Vector2.zero, Vector2.one, 21);
            var input = bg.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = bg.rectTransform; input.textComponent = label; input.fontAsset = Font;
            input.SetTextWithoutNotify(initial); input.onEndEdit.AddListener(value => changed?.Invoke(value)); return input;
        }
        public static ScrollRect Scroll(Transform parent, Vector2 min, Vector2 max, out RectTransform content)
        {
            var root = Rect(parent, "Scroll", min, max, 6);
            var viewport = Box(root, "Viewport", Vector2.zero, Vector2.one, Panel); viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            content = Rect(viewport.transform, "Content", new Vector2(0,1), Vector2.one);
            content.pivot = new Vector2(.5f,1);
            var scroll = root.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport.rectTransform;
            scroll.content = content; scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 45;
            return scroll;
        }
        public static string TimeLabel(double time) => string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0:00}:{1:00.000}",(int)Math.Max(0,time)/60,Math.Max(0,time)%60);
    }
}
