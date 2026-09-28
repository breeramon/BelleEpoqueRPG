using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BelleEpoque
{
    /// <summary>
    /// Liga a Sanidade do grupo ao pós-processamento: quanto menor a sanidade,
    /// mais escura a vinheta, mais aberração cromática e menos cor.
    /// Também dá "pulsos" de terror quando alguém perde sanidade.
    /// </summary>
    public class SanityPostFx : MonoBehaviour
    {
        [SerializeField] private Volume volume;

        [Header("Com sanidade zerada, somar:")]
        [SerializeField] private float extraVignette = 0.25f;
        [SerializeField] private float extraChromatic = 0.6f;
        [SerializeField] private float extraDesaturation = -40f;
        [SerializeField] private Color dreadVignetteColor = new Color(0.35f, 0f, 0.05f);

        [SerializeField] private float smoothing = 2f;

        private Vignette _vignette;
        private ChromaticAberration _chromatic;
        private ColorAdjustments _color;

        private float _baseVignette, _baseChromatic, _baseSaturation;
        private Color _baseVignetteColor;
        private float _targetDread, _dread, _pulse;

        private void Start()
        {
            if (volume == null) volume = FindAnyObjectByType<Volume>();
            if (volume == null) { enabled = false; return; }

            // volume.profile cria uma cópia em tempo de execução: não altera o asset no disco.
            var profile = volume.profile;
            if (!profile.TryGet(out _vignette)) _vignette = profile.Add<Vignette>(true);
            if (!profile.TryGet(out _chromatic)) _chromatic = profile.Add<ChromaticAberration>(true);
            if (!profile.TryGet(out _color)) _color = profile.Add<ColorAdjustments>(true);

            _vignette.intensity.overrideState = true;
            _vignette.color.overrideState = true;
            _chromatic.intensity.overrideState = true;
            _color.saturation.overrideState = true;

            _baseVignette = _vignette.intensity.value;
            _baseVignetteColor = _vignette.color.value;
            _baseChromatic = _chromatic.intensity.value;
            _baseSaturation = _color.saturation.value;
        }

        /// <param name="dread">0 = todos lúcidos, 1 = sanidade zerada.</param>
        public void SetDread(float dread) => _targetDread = Mathf.Clamp01(dread);

        /// <summary>Pulso rápido de terror (ex.: ao perder sanidade ou sofrer crítico).</summary>
        public void Pulse(float amount) => _pulse = Mathf.Clamp01(_pulse + amount);

        private void Update()
        {
            _dread = Mathf.MoveTowards(_dread, _targetDread, Time.deltaTime * smoothing * 0.5f);
            _pulse = Mathf.MoveTowards(_pulse, 0f, Time.deltaTime * 1.5f);
            float k = Mathf.Clamp01(_dread + _pulse);

            _vignette.intensity.value = Mathf.Clamp01(_baseVignette + extraVignette * k);
            _vignette.color.value = Color.Lerp(_baseVignetteColor, dreadVignetteColor, k);
            _chromatic.intensity.value = Mathf.Clamp01(_baseChromatic + extraChromatic * k);
            _color.saturation.value = Mathf.Clamp(_baseSaturation + extraDesaturation * k, -100f, 100f);
        }
    }
}
