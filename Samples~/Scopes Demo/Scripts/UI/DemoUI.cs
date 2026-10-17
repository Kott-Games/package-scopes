using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Kott.Scopes.Demo
{
    /// <summary>
    ///     Builds the demo's uGUI in code, so scenes and prefabs stay minimal. Not part of the Scopes API: real games
    ///     would author this UI as prefabs.
    /// </summary>
    public static class DemoUI
    {
        /// <summary>Background of panels.</summary>
        public static readonly Color PanelColor = new(0.08f, 0.09f, 0.12f, 0.92f);

        /// <summary>Default text color.</summary>
        public static readonly Color TextColor = new(0.92f, 0.93f, 0.95f);

        /// <summary>Accent for headings and the progress bar.</summary>
        public static readonly Color AccentColor = new(0.36f, 0.62f, 1f);

        private static Font _font;
        private static Sprite _whiteSprite;

        private static Font Font => _font != null ? _font : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>Creates a screen-space overlay canvas (1920×1080 reference) under <paramref name="parent" />.</summary>
        public static RectTransform CreateCanvas(string name, Transform parent, int sortingOrder)
        {
            GameObject canvasObject = new(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return (RectTransform)canvasObject.transform;
        }

        /// <summary>Creates the EventSystem uGUI needs for input, under <paramref name="parent" />.</summary>
        public static GameObject CreateEventSystem(Transform parent)
        {
            GameObject eventSystem = new("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystem.transform.SetParent(parent, false);
            return eventSystem;
        }

        /// <summary>Creates a colored rectangle spanning <paramref name="anchorMin" />–<paramref name="anchorMax" /> of its parent.</summary>
        public static RectTransform Panel(Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax,
            string name = "Panel")
        {
            RectTransform panel = CreateRect(name, parent);
            panel.anchorMin = anchorMin;
            panel.anchorMax = anchorMax;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;

            Image image = panel.gameObject.AddComponent<Image>();
            image.color = color;
            return panel;
        }

        /// <summary>Creates a full-size panel that blocks input to everything below it.</summary>
        public static RectTransform Fullscreen(Transform parent, Color color, string name = "Fullscreen")
        {
            return Panel(parent, color, Vector2.zero, Vector2.one, name);
        }

        /// <summary>
        ///     Creates a centered vertical layout of <paramref name="width" />; children stack top to bottom and the
        ///     column grows with them.
        /// </summary>
        public static RectTransform Column(Transform parent, float width, Color? background = null,
            string name = "Column")
        {
            RectTransform column = CreateRect(name, parent);
            column.anchorMin = column.anchorMax = column.pivot = new Vector2(0.5f, 0.5f);
            column.sizeDelta = new Vector2(width, 0f);

            if (background.HasValue)
            {
                column.gameObject.AddComponent<Image>().color = background.Value;
            }

            VerticalLayoutGroup layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 32, 32);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            column.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return column;
        }

        /// <summary>Creates a horizontal row for buttons inside a <see cref="Column" />.</summary>
        public static RectTransform Row(Transform parent, string name = "Row")
        {
            RectTransform row = CreateRect(name, parent);
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return row;
        }

        /// <summary>Creates a text label. In a layout group it takes its preferred height.</summary>
        public static Text Label(Transform parent, string text, int fontSize = 32,
            TextAnchor alignment = TextAnchor.MiddleCenter, string name = "Label")
        {
            RectTransform rect = CreateRect(name, parent);
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = Font;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = TextColor;
            label.supportRichText = true;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        /// <summary>Creates a button with a label that invokes <paramref name="onClick" />.</summary>
        public static Button Button(Transform parent, string text, Action onClick, int fontSize = 32)
        {
            RectTransform rect = CreateRect(text, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.2f, 0.24f, 0.32f);

            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            button.colors = colors;
            // not selectable: a selected button would also react to Submit (Space), which the demo uses as a game key
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => onClick());

            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = fontSize * 2.2f;

            Text label = Label(rect, text, fontSize);
            Stretch((RectTransform)label.transform);
            return button;
        }

        /// <summary>Creates a horizontal progress bar; set the returned image's <c>fillAmount</c> (0–1).</summary>
        public static Image ProgressBar(Transform parent, float height = 24f)
        {
            RectTransform background = CreateRect("ProgressBar", parent);
            background.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
            background.gameObject.AddComponent<LayoutElement>().minHeight = height;

            RectTransform fillRect = CreateRect("Fill", background);
            Stretch(fillRect);
            Image fill = fillRect.gameObject.AddComponent<Image>();
            fill.color = AccentColor;
            fill.sprite = WhiteSprite();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            return fill;
        }

        /// <summary>Makes <paramref name="rect" /> fill its parent.</summary>
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Destroys <paramref name="target" /> if it still exists.</summary>
        public static void Destroy(Component target)
        {
            if (target != null)
            {
                Object.Destroy(target.gameObject);
            }
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return (RectTransform)gameObject.transform;
        }

        // filled images need a sprite; the white texture is enough for flat colors
        private static Sprite WhiteSprite()
        {
            if (_whiteSprite == null)
            {
                _whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            }

            return _whiteSprite;
        }
    }
}
