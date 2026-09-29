using System.Collections;
using System.Collections.Generic;
using BelleEpoque.Core;
using UnityEngine;

namespace BelleEpoque
{
    /// <summary>
    /// Representação visual de uma unidade. Funciona de dois jeitos:
    ///  - Com Animator (ex.: modelo do Mixamo): dispara triggers Attack, Cast, Heal, Hit, Die, Defend.
    ///  - Sem Animator (cápsulas provisórias): faz animações procedurais simples.
    /// </summary>
    public class UnitView : MonoBehaviour
    {
        public BattleUnit Unit { get; private set; }
        public UnitDefinition Definition { get; private set; }

        private Animator _animator;
        private readonly HashSet<string> _animatorParams = new HashSet<string>();
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private Vector3 _baseScale;
        private bool _isAway;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public void Init(BattleUnit unit, UnitDefinition definition)
        {
            Unit = unit;
            Definition = definition;
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
            _baseScale = transform.localScale;
            _renderers = GetComponentsInChildren<Renderer>();
            _mpb = new MaterialPropertyBlock();

            _animator = GetComponentInChildren<Animator>();
            if (_animator != null)
            {
                _animator.applyRootMotion = false; // animações do Mixamo não devem mover o personagem
                foreach (var p in _animator.parameters) _animatorParams.Add(p.name);
            }
        }

        /// <summary>Ponto acima da cabeça, para números de dano.</summary>
        public Vector3 TopPosition
        {
            get
            {
                var b = GetBounds();
                return new Vector3(b.center.x, b.max.y + 0.2f, b.center.z);
            }
        }

        public Vector3 CenterPosition => GetBounds().center;

        private Bounds GetBounds()
        {
            bool has = false;
            var b = new Bounds(transform.position + Vector3.up, Vector3.one);
            if (_renderers != null)
                foreach (var r in _renderers)
                {
                    // Partículas (auras, VFX) não contam: o número deve ficar acima do corpo
                    if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    if (!has) { b = r.bounds; has = true; }
                    else b.Encapsulate(r.bounds);
                }
            return b;
        }

        private bool Trigger(string name)
        {
            if (_animator == null || string.IsNullOrEmpty(name) || !_animatorParams.Contains(name)) return false;
            _animator.SetTrigger(name);
            return true;
        }

        // ------------------------------------------------------------------ Ações

        /// <summary>Anima o início da ação e retorna no "momento do impacto".</summary>
        public IEnumerator PlayAction(SkillDefinition skill, UnitView target)
        {
            string trigger = skill != null ? skill.animationTrigger : "Attack";
            bool melee = skill != null && skill.moveToTarget && target != null && target != this;

            if (melee)
            {
                Vector3 dir = (target.transform.position - _homePosition);
                dir.y = 0f;
                Vector3 destination = target.transform.position - dir.normalized * 1.3f;
                destination.y = _homePosition.y;
                yield return MoveTo(destination, 0.22f);
                _isAway = true;
            }

            bool animated = Trigger(trigger);
            if (!animated)
            {
                // Animação procedural: "pulo" de conjuração ou estocada.
                yield return Punch(melee ? 0.15f : 0.25f, 0.18f);
            }
            else
            {
                yield return new WaitForSeconds(0.35f); // aproximadamente o golpe da animação
            }
        }

        public IEnumerator ReturnHome()
        {
            if (!_isAway) yield break;
            _isAway = false;
            yield return MoveTo(_homePosition, 0.25f);
            transform.rotation = _homeRotation;
        }

        public IEnumerator PlayHit(bool strong)
        {
            if (!Trigger("Hit")) { /* sem animator: só tremor */ }
            StartCoroutine(Flash(new Color(1f, 0.35f, 0.35f), 0.15f));
            yield return Shake(strong ? 0.18f : 0.1f, 0.25f);
        }

        public IEnumerator PlayHealed(Color tint)
        {
            StartCoroutine(Flash(tint, 0.3f));
            yield return Punch(0.08f, 0.2f);
        }

        public void PlayDefend()
        {
            if (!Trigger("Defend")) StartCoroutine(Squash(0.85f, 0.25f));
        }

        public IEnumerator PlayDeath()
        {
            if (Trigger("Die"))
            {
                yield return new WaitForSeconds(1.2f);
                yield break;
            }

            // Queda procedural: tomba e afunda.
            Quaternion start = transform.rotation;
            Quaternion end = start * Quaternion.Euler(-80f, 0f, 0f);
            Vector3 p0 = transform.position;
            Vector3 p1 = p0 + Vector3.down * 0.6f;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.6f;
                float e = Mathf.SmoothStep(0f, 1f, t);
                transform.rotation = Quaternion.Slerp(start, end, e);
                transform.position = Vector3.Lerp(p0, p1, e);
                yield return null;
            }
        }

        // ------------------------------------------------------------------ Utilidades de animação

        private IEnumerator MoveTo(Vector3 destination, float duration)
        {
            Vector3 start = transform.position;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                transform.position = Vector3.Lerp(start, destination, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
        }

        private IEnumerator Punch(float amount, float duration)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float s = 1f + Mathf.Sin(t * Mathf.PI) * amount;
                transform.localScale = _baseScale * s;
                yield return null;
            }
            transform.localScale = _baseScale;
        }

        private IEnumerator Squash(float factor, float duration)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float k = Mathf.Sin(t * Mathf.PI);
                transform.localScale = new Vector3(_baseScale.x * (1f + (1f - factor) * k * 0.5f), _baseScale.y * Mathf.Lerp(1f, factor, k), _baseScale.z);
                yield return null;
            }
            transform.localScale = _baseScale;
        }

        private IEnumerator Shake(float amount, float duration)
        {
            Vector3 origin = transform.position;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float damper = 1f - t / duration;
                transform.position = origin + transform.right * Mathf.Sin(t * 60f) * amount * damper;
                yield return null;
            }
            transform.position = origin;
        }

        private IEnumerator Flash(Color color, float duration)
        {
            if (_renderers == null) yield break;
            foreach (var r in _renderers)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, color);
                _mpb.SetColor(ColorId, color);
                r.SetPropertyBlock(_mpb);
            }
            yield return new WaitForSeconds(duration);
            foreach (var r in _renderers)
                if (r != null && !(r is ParticleSystemRenderer)) r.SetPropertyBlock(null);
        }
    }
}
