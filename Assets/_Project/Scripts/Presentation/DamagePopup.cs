using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BelleEpoque
{
    /// <summary>
    /// Número flutuante (dano, cura, sanidade, status) que nasce ACIMA da cabeça da unidade, sobe e some.
    ///
    /// É desenhado num Canvas de tela (Screen Space - Overlay) e acompanha a posição 3D da unidade,
    /// por isso nunca fica escondido dentro/atrás dos modelos e tem o mesmo tamanho em qualquer distância.
    /// Vários números na mesma unidade ao mesmo tempo (dano + VULNERÁVEL + SAN...) são empilhados.
    /// </summary>
    public class DamagePopup : MonoBehaviour
    {
        /// <summary>Fonte dos números (a HUD define a Bebas Neue do tema).</summary>
        public static TMP_FontAsset Font;

        // Distâncias em pixels (referência 1920x1080)
        private const float HeroGap = 28f;      // acima da cabeça dos agentes
        private const float EnemyGap = 78f;     // acima do nome/barra de PV das ameaças
        private const float StackStep = 52f;    // espaço entre números empilhados
        private const float StackWindow = 0.45f;
        private const float RiseDistance = 70f;

        private static Canvas _canvas;
        private static RectTransform _root;
        private static readonly Dictionary<UnitView, (float time, int count)> _stack = new Dictionary<UnitView, (float, int)>();

        private RectTransform _rt;
        private TextMeshProUGUI _text;
        private UnitView _follow;
        private Vector3 _world;
        private Vector3 _followOffset;
        private Vector2 _offset;
        private float _drift;
        private float _age;
        private float _lifetime = 1.2f;
        private Color _color;
        private Camera _camera;

        /// <summary>Número acima da cabeça de uma unidade (acompanha a unidade se ela se mexer).</summary>
        public static DamagePopup Spawn(UnitView view, string text, Color color, float size = 6f)
        {
            if (view == null) return null;
            bool hero = view.Unit != null && view.Unit.Team == BelleEpoque.Core.Team.Heroes;

            int index = 0;
            if (_stack.TryGetValue(view, out var s) && Time.time - s.time < StackWindow) index = s.count + 1;
            _stack[view] = (Time.time, index);

            var popup = Create(text, color, size);
            popup._follow = view;
            popup._world = view.TopPosition;
            popup._followOffset = popup._world - view.transform.position;
            popup._offset = new Vector2(0f, (hero ? HeroGap : EnemyGap) + index * StackStep);
            return popup;
        }

        /// <summary>Número num ponto qualquer do mundo.</summary>
        public static DamagePopup Spawn(Vector3 position, string text, Color color, float size = 6f)
        {
            var popup = Create(text, color, size);
            popup._world = position;
            return popup;
        }

        private static DamagePopup Create(string text, Color color, float size)
        {
            EnsureCanvas();
            var go = new GameObject("Popup " + text, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(_root, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(420f, 90f);

            var popup = go.AddComponent<DamagePopup>();
            popup._rt = rt;
            popup._text = go.AddComponent<TextMeshProUGUI>();
            if (Font != null) popup._text.font = Font;
            popup._text.text = text;
            popup._text.fontSize = size * 10f; // 6 -> 60 px
            popup._text.alignment = TextAlignmentOptions.Center;
            popup._text.textWrappingMode = TextWrappingModes.NoWrap;
            popup._text.characterSpacing = 4f;
            popup._text.raycastTarget = false;
            popup._text.outlineWidth = 0.25f;
            popup._text.outlineColor = new Color32(10, 5, 10, 255);
            popup._text.color = color;
            popup._color = color;
            popup._drift = Random.Range(-18f, 18f);
            popup._camera = Camera.main;
            popup.Place(); // já nasce no lugar certo (sem piscar no centro da tela)
            return popup;
        }

        private static void EnsureCanvas()
        {
            if (_canvas != null) return;
            _stack.Clear();
            var go = new GameObject("DamagePopups", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.layer = 5;
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10; // acima da HUD da batalha, abaixo do contador de FPS (100)
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;
        }

        private void LateUpdate()
        {
            _age += Time.deltaTime;
            if (_follow != null) _world = _follow.transform.position + _followOffset; // segue sem tremer com a animação
            Place();

            // Pequeno "pop" no início
            float scale = _age < 0.12f ? Mathf.Lerp(1.6f, 1f, _age / 0.12f) : 1f;
            _rt.localScale = Vector3.one * scale;

            float alpha = 1f - Mathf.Clamp01((_age - _lifetime * 0.6f) / (_lifetime * 0.4f));
            _text.color = new Color(_color.r, _color.g, _color.b, alpha);

            if (_age >= _lifetime) Destroy(gameObject);
        }

        private void Place()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null || _root == null) return;

            Vector3 screen = _camera.WorldToScreenPoint(_world);
            bool visible = screen.z > 0f;
            _text.enabled = visible;
            if (!visible) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local))
            {
                float t = Mathf.Clamp01(_age / _lifetime);
                float rise = RiseDistance * (1f - (1f - t) * (1f - t)); // sobe rápido e desacelera
                _rt.anchoredPosition = local + _offset + new Vector2(_drift * t, rise);
            }
        }
    }
}
