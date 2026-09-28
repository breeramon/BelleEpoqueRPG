using TMPro;
using UnityEngine;

namespace BelleEpoque
{
    /// <summary>Número flutuante (dano, cura, sanidade) que sobe e some.</summary>
    public class DamagePopup : MonoBehaviour
    {
        /// <summary>Fonte dos números (a HUD define a Bebas Neue do tema).</summary>
        public static TMP_FontAsset Font;

        private TextMeshPro _text;
        private float _age;
        private float _lifetime = 1.1f;
        private Vector3 _velocity;
        private Color _color;
        private Camera _camera;

        public static DamagePopup Spawn(Vector3 position, string text, Color color, float size = 6f)
        {
            var go = new GameObject("Popup " + text);
            go.transform.position = position;
            var popup = go.AddComponent<DamagePopup>();
            popup._text = go.AddComponent<TextMeshPro>();
            if (Font != null) popup._text.font = Font;
            popup._text.text = text;
            popup._text.fontSize = size;
            popup._text.alignment = TextAlignmentOptions.Center;
            popup._text.characterSpacing = 4f;
            popup._text.rectTransform.sizeDelta = new Vector2(8f, 2f);
            popup._text.outlineWidth = 0.25f;
            popup._text.outlineColor = new Color32(10, 5, 10, 255);
            popup._color = color;
            popup._text.color = color;
            popup._velocity = new Vector3(Random.Range(-0.3f, 0.3f), 1.4f, 0f);
            return popup;
        }

        private void Awake() => _camera = Camera.main;

        private void LateUpdate()
        {
            _age += Time.deltaTime;
            transform.position += _velocity * Time.deltaTime;
            _velocity.y = Mathf.Max(0.2f, _velocity.y - 2f * Time.deltaTime);

            if (_camera != null) transform.rotation = _camera.transform.rotation;

            // Pequeno "pop" no início
            float scale = _age < 0.1f ? Mathf.Lerp(1.6f, 1f, _age / 0.1f) : 1f;
            transform.localScale = Vector3.one * scale;

            float alpha = 1f - Mathf.Clamp01((_age - _lifetime * 0.6f) / (_lifetime * 0.4f));
            _text.color = new Color(_color.r, _color.g, _color.b, alpha);

            if (_age >= _lifetime) Destroy(gameObject);
        }
    }
}
