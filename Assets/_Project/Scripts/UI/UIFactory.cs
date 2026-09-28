using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace BelleEpoque
{
    /// <summary>Atalhos para montar a uGUI por código no estilo do tema.</summary>
    public static class UIFactory
    {
        private static Sprite _fadeRight, _fadeUp, _fadeDown, _fadeLeft;

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
        }

        public static void Anchor(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void Stretch(RectTransform rt, float padding = 0f) => Stretch(rt, padding, padding, padding, padding);

        public static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static UnityEngine.UI.Image CreatePanel(Transform parent, string name, Color color, bool raycast = false, Sprite sprite = null)
        {
            var rt = CreateRect(name, parent);
            var img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public enum Fade { Right, Left, Up, Down }

        /// <summary>Sprite de degradê de opaco para transparente, gerado uma vez.</summary>
        public static Sprite FadeSprite(Fade dir)
        {
            switch (dir)
            {
                case Fade.Right: return _fadeRight != null ? _fadeRight : (_fadeRight = MakeFade(true, false));
                case Fade.Left: return _fadeLeft != null ? _fadeLeft : (_fadeLeft = MakeFade(true, true));
                case Fade.Up: return _fadeUp != null ? _fadeUp : (_fadeUp = MakeFade(false, false));
                default: return _fadeDown != null ? _fadeDown : (_fadeDown = MakeFade(false, true));
            }
        }

        private static Sprite MakeFade(bool horizontal, bool reverse)
        {
            const int n = 64;
            var tex = new Texture2D(horizontal ? n : 1, horizontal ? 1 : n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "UIFade"
            };
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                float a = reverse ? t : 1f - t;
                a = Mathf.SmoothStep(0f, 1f, a);
                var c = new Color(1f, 1f, 1f, a);
                if (horizontal) tex.SetPixel(i, 0, c); else tex.SetPixel(0, i, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite _disc, _ring;

        /// <summary>Círculo cheio (disc) ou anel (ring), gerado uma vez.</summary>
        public static Sprite CircleSprite(bool ring)
        {
            if (ring && _ring != null) return _ring;
            if (!ring && _disc != null) return _disc;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = ring ? "UIRing" : "UIDisc" };
            float r = n / 2f - 1f, inner = r - 7f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    float outer = Mathf.Clamp01(r - d + 0.5f);
                    float a = ring ? Mathf.Min(outer, Mathf.Clamp01(d - inner + 0.5f)) : outer;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            if (ring) _ring = sprite; else _disc = sprite;
            return sprite;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left, float spacing = 0f)
        {
            var rt = CreateRect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.characterSpacing = spacing;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        /// <summary>Filete dourado fino (1–2 px), como os divisores do site.</summary>
        public static UnityEngine.UI.Image CreateRule(Transform parent, string name, Color color, float thickness = 1.5f)
        {
            var img = CreatePanel(parent, name, color);
            img.rectTransform.sizeDelta = new Vector2(0f, thickness);
            return img;
        }

        /// <summary>Losango decorativo (quadrado girado 45°).</summary>
        public static UnityEngine.UI.Image CreateDiamond(Transform parent, string name, Color color, float size)
        {
            var img = CreatePanel(parent, name, color);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            return img;
        }

        /// <summary>Barra fina de recurso feita com âncoras (não precisa de sprite).</summary>
        public static RectTransform CreateBar(Transform parent, string name, Color background, Color fill)
        {
            var bg = CreatePanel(parent, name, background);
            var fillImg = CreatePanel(bg.transform, "Fill", fill);
            var fillRt = fillImg.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            return fillRt;
        }

        public static void SetFill(RectTransform fill, float percent) => fill.anchorMax = new Vector2(Mathf.Clamp01(percent), 1f);

        /// <summary>Adiciona clique e hover (inclusive em botão desabilitado).</summary>
        public static UnityEngine.UI.Button MakeButton(GameObject go, UnityEngine.UI.Graphic target, ButtonColors colors, UnityAction onClick)
        {
            var button = go.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = target;
            var cb = button.colors;
            cb.normalColor = colors.Normal;
            cb.highlightedColor = colors.Highlighted;
            cb.selectedColor = colors.Highlighted;
            cb.pressedColor = colors.Pressed;
            cb.disabledColor = colors.Disabled;
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.12f;
            button.colors = cb;
            if (onClick != null) button.onClick.AddListener(onClick);
            return button;
        }

        public struct ButtonColors
        {
            public Color Normal, Highlighted, Pressed, Disabled;
        }
    }

    /// <summary>Repassa hover/seleção para callbacks (usado para mostrar descrição e o losango).</summary>
    public class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public Action OnEnter;
        public Action OnExit;

        public void OnPointerEnter(PointerEventData eventData) => OnEnter?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => OnExit?.Invoke();
        public void OnSelect(BaseEventData eventData) => OnEnter?.Invoke();
        public void OnDeselect(BaseEventData eventData) => OnExit?.Invoke();
    }
}
