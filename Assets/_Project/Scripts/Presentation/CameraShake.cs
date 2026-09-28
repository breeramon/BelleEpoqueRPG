using UnityEngine;

namespace BelleEpoque
{
    /// <summary>Tremor de câmera baseado em Perlin noise. Coloque na Main Camera.</summary>
    public class CameraShake : MonoBehaviour
    {
        [SerializeField] private float maxOffset = 0.25f;
        [SerializeField] private float maxRoll = 2f;
        [SerializeField] private float decay = 2.5f;

        private float _trauma;
        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private float _seed;

        private void Awake()
        {
            _basePosition = transform.localPosition;
            _baseRotation = transform.localRotation;
            _seed = Random.value * 100f;
        }

        /// <summary>Recaptura a pose base (use depois de mudar o pai da câmera).</summary>
        public void Rebase()
        {
            _basePosition = transform.localPosition;
            _baseRotation = transform.localRotation;
        }

        /// <param name="amount">0..1. Valores são somados e limitados a 1.</param>
        public void Shake(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

        private void LateUpdate()
        {
            if (_trauma <= 0f) return;
            float shake = _trauma * _trauma;
            float t = Time.time * 25f;
            transform.localPosition = _basePosition + new Vector3(
                (Mathf.PerlinNoise(_seed, t) * 2f - 1f) * maxOffset * shake,
                (Mathf.PerlinNoise(_seed + 1f, t) * 2f - 1f) * maxOffset * shake,
                0f);
            transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(_seed + 2f, t) * 2f - 1f) * maxRoll * shake);

            _trauma = Mathf.Max(0f, _trauma - decay * Time.deltaTime);
            if (_trauma <= 0f)
            {
                transform.localPosition = _basePosition;
                transform.localRotation = _baseRotation;
            }
        }
    }
}
