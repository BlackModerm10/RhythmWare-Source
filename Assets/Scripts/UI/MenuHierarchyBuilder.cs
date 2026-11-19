using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RhythmWare.Core;
using RhythmWare.Rhythm;
using RhythmWare.Malware;

namespace RhythmWare.UI
{
    /// <summary>
    /// Programmatically builds the complete UI Suite on the Canvas:
    /// - Main Menu
    /// - Level Selector with Difficulty
    /// - Chart Editor (osu! style)
    /// - Settings (Audio, Video, Background Dim, Offset)
    /// - Extra Content (Malware Encyclopedia)
    /// </summary>
    public static class MenuHierarchyBuilder
    {
        private static Font defaultFont;
        private static Sprite solidWhiteSprite;

        private static Font GetFont()
        {
            if (defaultFont == null)
            {
                defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (defaultFont == null)
                {
                    defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
            }
            return defaultFont;
        }

        private static Sprite GetSolidSprite()
        {
            if (solidWhiteSprite == null)
            {
                Texture2D tex = new Texture2D(2, 2);
                tex.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
                tex.Apply();
                solidWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            }
            return solidWhiteSprite;
        }

        public static void BuildFullMenuSuite(Canvas canvas)
        {
            if (canvas == null) return;

            // Ensure EventSystem exists
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            Transform canvasTrans = canvas.transform;

            // Main Menu Controller on Canvas (stays active even when child panels are hidden)
            MainMenuUI mainMenuUI = canvas.GetComponent<MainMenuUI>();
            if (mainMenuUI == null)
            {
                mainMenuUI = canvas.gameObject.AddComponent<MainMenuUI>();
            }

            // 1. Build Main Menu Panel
            GameObject mainMenuObj = CreatePanel(canvasTrans, "MainMenuPanel", new Color(0.04f, 0.06f, 0.12f, 0.96f));

            var candyButtons=CandyMenuBuilder.Build(mainMenuObj.transform,mainMenuUI);
            Button playBtn=candyButtons[0],editorBtn=candyButtons[1],extraBtn=candyButtons[2],settingsBtn=candyButtons[3],exitBtn=candyButtons[4];

            // 2. Build Level Select Panel (osu! / Rift of the NecroDancer style)
            GameObject levelSelectObj = CreatePanel(canvasTrans, "LevelSelectPanel", new Color(0.03f, 0.04f, 0.08f, 0.98f));
            LevelSelectUI levelSelectUI = levelSelectObj.AddComponent<LevelSelectUI>();
            levelSelectUI.Build();

            // 3. Build Chart Editor Panel
            GameObject editorObj = CreatePanel(canvasTrans, "EditorPanel", new Color(0.01f, 0.02f, 0.04f, 0.35f));
            ChartEditorUI editorUI = editorObj.AddComponent<ChartEditorUI>();
            editorUI.Build();

            // 4. Build Settings Panel
            GameObject settingsObj = CreatePanel(canvasTrans, "SettingsPanel", new Color(0.04f, 0.05f, 0.10f, 0.98f));
            SettingsUI settingsUI = settingsObj.AddComponent<SettingsUI>();
            settingsUI.Build();

            // 5. Build Extra Content Panel
            GameObject extraObj = CreatePanel(canvasTrans, "ExtraContentPanel", new Color(0.04f, 0.05f, 0.10f, 0.98f));
            ExtraContentUI extraUI = extraObj.AddComponent<ExtraContentUI>();
            extraUI.Build();

            mainMenuUI.Configure(mainMenuObj,levelSelectObj,editorObj,settingsObj,extraObj,new[]{playBtn,editorBtn,extraBtn,settingsBtn,exitBtn});

            // Show Main Menu, hide all subpanels initially
            mainMenuObj.SetActive(true);
            levelSelectObj.SetActive(false);
            editorObj.SetActive(false);
            settingsObj.SetActive(false);
            extraObj.SetActive(false);
        }

        #region Helpers

        private static GameObject CreatePanel(Transform parent, string name, Color bgColor)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            RectTransform rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            Image img = panel.AddComponent<Image>();
            img.sprite = GetSolidSprite();
            img.color = bgColor;

            return panel;
        }

        private static GameObject CreateSubPanel(Transform parent, string name, Color bgColor, Vector2 pos, Vector2 sizeDelta)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            RectTransform rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;

            Image img = panel.AddComponent<Image>();
            img.sprite = GetSolidSprite();
            img.color = bgColor;

            return panel;
        }

        private static Text CreateText(Transform parent, string name, string text, int size, Vector2 pos, Vector2 sizeDelta, Color color, FontStyle style)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;

            Text uiText = go.AddComponent<Text>();
            uiText.font = GetFont();
            uiText.text = text;
            uiText.fontSize = size;
            uiText.fontStyle = style;
            uiText.color = color;
            uiText.alignment = TextAnchor.MiddleCenter;
            uiText.raycastTarget = false;

            return uiText;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 sizeDelta, Color baseColor)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);

            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;

            Image img = btnObj.AddComponent<Image>();
            img.sprite = GetSolidSprite();
            img.color = baseColor;

            Button btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = baseColor * 1.25f;
            colors.pressedColor = baseColor * 0.75f;
            btn.colors = colors;

            // Audio triggers for Hover and Click
            btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayMenuClick();
            });

            var trigger = btnObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var hoverEntry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            hoverEntry.callback.AddListener((data) =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayMenuHover();
            });
            trigger.triggers.Add(hoverEntry);

            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform tRt = textObj.AddComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.sizeDelta = Vector2.zero;

            Text uiText = textObj.AddComponent<Text>();
            uiText.font = GetFont();
            uiText.text = label;
            uiText.fontSize = 18;
            uiText.fontStyle = FontStyle.Bold;
            uiText.color = Color.black;
            uiText.alignment = TextAnchor.MiddleCenter;
            uiText.raycastTarget = false;
            uiText.resizeTextForBestFit = true;
            uiText.resizeTextMinSize = 10;
            uiText.resizeTextMaxSize = 18;

            return btn;
        }

        private static Slider CreateSlider(Transform parent, string name, Vector2 pos, Vector2 sizeDelta)
        {
            GameObject sliderObj = new GameObject(name);
            sliderObj.transform.SetParent(parent, false);

            RectTransform rt = sliderObj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;

            Slider slider = sliderObj.AddComponent<Slider>();

            // Background
            GameObject bg = new GameObject("Background");
            bg.transform.SetParent(sliderObj.transform, false);
            RectTransform bgRt = bg.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;
            Image bgImg = bg.AddComponent<Image>();
            bgImg.sprite = GetSolidSprite();
            bgImg.color = new Color(0.2f, 0.22f, 0.28f, 1f);

            // Fill Area
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform faRt = fillArea.AddComponent<RectTransform>();
            faRt.anchorMin = Vector2.zero;
            faRt.anchorMax = Vector2.one;
            faRt.sizeDelta = Vector2.zero;

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fRt = fill.AddComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero;
            fRt.anchorMax = Vector2.one;
            fRt.sizeDelta = Vector2.zero;
            Image fImg = fill.AddComponent<Image>();
            fImg.sprite = GetSolidSprite();
            fImg.color = Color.cyan;

            slider.fillRect = fRt;
            slider.targetGraphic = bgImg;

            return slider;
        }

        private static InputField CreateInputField(Transform parent, string name, string placeholder, Vector2 pos, Vector2 sizeDelta)
        {
            GameObject inputObj = new GameObject(name);
            inputObj.transform.SetParent(parent, false);

            RectTransform rt = inputObj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;

            Image img = inputObj.AddComponent<Image>();
            img.sprite = GetSolidSprite();
            img.color = new Color(0.12f, 0.14f, 0.20f, 1f);

            InputField inputField = inputObj.AddComponent<InputField>();

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(inputObj.transform, false);
            RectTransform tRt = textObj.AddComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.sizeDelta = new Vector2(-10f, 0f);
            Text text = textObj.AddComponent<Text>();
            text.font = GetFont();
            text.fontSize = 16;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;

            inputField.textComponent = text;
            inputField.text = placeholder;

            return inputField;
        }

        private static Dropdown CreateDropdown(Transform parent, string name, Vector2 pos, Vector2 sizeDelta)
        {
            DefaultControls.Resources res = new DefaultControls.Resources();
            res.standard = GetSolidSprite();
            res.background = GetSolidSprite();

            GameObject dropObj = DefaultControls.CreateDropdown(res);
            dropObj.name = name;
            dropObj.transform.SetParent(parent, false);

            RectTransform rt = dropObj.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;

            Image img = dropObj.GetComponent<Image>();
            if (img != null)
            {
                img.color = new Color(0.14f, 0.18f, 0.28f, 1f);
            }

            Dropdown dropdown = dropObj.GetComponent<Dropdown>();

            if (dropdown.captionText != null)
            {
                dropdown.captionText.font = GetFont();
                dropdown.captionText.fontSize = 14;
                dropdown.captionText.color = Color.white;
                dropdown.captionText.alignment = TextAnchor.MiddleLeft;
                dropdown.captionText.resizeTextForBestFit = true;
                dropdown.captionText.resizeTextMinSize = 10;
                dropdown.captionText.resizeTextMaxSize = 14;
            }

            if (dropdown.itemText != null)
            {
                dropdown.itemText.font = GetFont();
                dropdown.itemText.fontSize = 13;
                dropdown.itemText.color = Color.white;
                dropdown.itemText.alignment = TextAnchor.MiddleLeft;
                dropdown.itemText.resizeTextForBestFit = true;
                dropdown.itemText.resizeTextMinSize = 10;
                dropdown.itemText.resizeTextMaxSize = 13;
            }

            if (dropdown.template != null)
            {
                dropdown.template.sizeDelta = new Vector2(0f, 180f);
                Image tImg = dropdown.template.GetComponent<Image>();
                if (tImg != null) tImg.color = new Color(0.04f, 0.06f, 0.10f, 0.98f);

                // Style Viewport, Item, and Toggle so backgrounds are dark and text is clearly readable
                Transform vp = dropdown.template.Find("Viewport");
                if (vp != null)
                {
                    Image vpImg = vp.GetComponent<Image>();
                    if (vpImg != null) vpImg.color = new Color(0.04f, 0.06f, 0.10f, 0.98f);

                    Transform content = vp.Find("Content");
                    if (content != null)
                    {
                        Transform item = content.Find("Item");
                        if (item != null)
                        {
                            Image itemImg = item.GetComponent<Image>();
                            if (itemImg != null)
                            {
                                itemImg.color = new Color(0.07f, 0.09f, 0.15f, 1f);
                            }

                            Toggle itemToggle = item.GetComponent<Toggle>();
                            if (itemToggle != null)
                            {
                                var colors = itemToggle.colors;
                                colors.normalColor = new Color(0.07f, 0.09f, 0.15f, 1f);
                                colors.highlightedColor = new Color(0.16f, 0.28f, 0.45f, 1f);
                                colors.pressedColor = new Color(0.25f, 0.40f, 0.65f, 1f);
                                colors.selectedColor = new Color(0.12f, 0.20f, 0.35f, 1f);
                                itemToggle.colors = colors;
                            }

                            Transform checkmark = item.Find("Item Checkmark");
                            if (checkmark != null)
                            {
                                Image checkImg = checkmark.GetComponent<Image>();
                                if (checkImg != null) checkImg.color = Color.cyan;
                            }

                            Transform itemLabel = item.Find("Item Label");
                            if (itemLabel != null)
                            {
                                Text itemTextComp = itemLabel.GetComponent<Text>();
                                if (itemTextComp != null)
                                {
                                    itemTextComp.font = GetFont();
                                    itemTextComp.fontSize = 14;
                                    itemTextComp.color = Color.white;
                                    itemTextComp.alignment = TextAnchor.MiddleLeft;
                                    itemTextComp.resizeTextForBestFit = true;
                                    itemTextComp.resizeTextMinSize = 10;
                                    itemTextComp.resizeTextMaxSize = 14;
                                }
                            }
                        }
                    }
                }

                // Scrollbar styling
                Transform sb = dropdown.template.Find("Scrollbar");
                if (sb != null)
                {
                    Image sbImg = sb.GetComponent<Image>();
                    if (sbImg != null) sbImg.color = new Color(0.03f, 0.04f, 0.07f, 1f);

                    Transform handle = sb.Find("Sliding Area/Handle");
                    if (handle != null)
                    {
                        Image hImg = handle.GetComponent<Image>();
                        if (hImg != null) hImg.color = new Color(0f, 0.75f, 1f, 0.85f);
                    }
                }

                Canvas tCanvas = dropdown.template.gameObject.GetComponent<Canvas>();
                if (tCanvas == null) tCanvas = dropdown.template.gameObject.AddComponent<Canvas>();
                tCanvas.overrideSorting = true;
                tCanvas.sortingOrder = 30000;
                if (dropdown.template.gameObject.GetComponent<GraphicRaycaster>() == null)
                {
                    dropdown.template.gameObject.AddComponent<GraphicRaycaster>();
                }
            }

            return dropdown;
        }

        private static void AssignText(object target, string fieldName, Text textComp)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(target, textComp);
            }
        }

        #endregion
    }
}
