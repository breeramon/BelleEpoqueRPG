using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace BelleEpoque
{
    /// <summary>
    /// Música (com crossfade), ambiência em loop e efeitos sonoros com várias vozes.
    /// Se você criar um AudioMixer, arraste os grupos Music/SFX/Ambience nos campos abaixo
    /// para controlar volumes separadamente (ex.: numa tela de opções).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Mixer (opcional)")]
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup ambienceGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;

        [Header("Volumes")]
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.55f;
        [Range(0f, 1f)] [SerializeField] private float ambienceVolume = 0.4f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.9f;
        [SerializeField] private int sfxVoices = 10;

        [Header("Sons genéricos (usados quando a habilidade não tem som próprio)")]
        public AudioClip uiClick;
        public AudioClip uiBack;
        public AudioClip turnStart;
        public AudioClip genericHit;
        public AudioClip criticalHit;
        public AudioClip weaknessHit;
        public AudioClip miss;
        public AudioClip heal;
        public AudioClip sanityLoss;
        public AudioClip statusApplied;
        public AudioClip death;

        private AudioSource _musicA, _musicB, _ambience;
        private AudioSource[] _sfx;
        private int _nextVoice;
        private bool _musicOnA = true;
        private Coroutine _fade;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _musicA = CreateSource("Music A", musicGroup, true);
            _musicB = CreateSource("Music B", musicGroup, true);
            _ambience = CreateSource("Ambience", ambienceGroup, true);
            _sfx = new AudioSource[Mathf.Max(1, sfxVoices)];
            for (int i = 0; i < _sfx.Length; i++) _sfx[i] = CreateSource("SFX " + i, sfxGroup, false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private AudioSource CreateSource(string sourceName, AudioMixerGroup group, bool loop)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f; // 2D: ideal para jogo de turnos com câmera fixa
            src.outputAudioMixerGroup = group;
            return src;
        }

        public void PlayMusic(AudioClip clip, float fadeSeconds = 1.5f)
        {
            if (clip == null) return;
            var from = _musicOnA ? _musicA : _musicB;
            var to = _musicOnA ? _musicB : _musicA;
            if (from.clip == clip && from.isPlaying) return;
            _musicOnA = !_musicOnA;

            to.clip = clip;
            to.volume = 0f;
            to.Play();
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(Crossfade(from, to, fadeSeconds));
        }

        public void StopMusic(float fadeSeconds = 1f)
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(Crossfade(_musicOnA ? _musicA : _musicB, null, fadeSeconds));
        }

        public void PlayAmbience(AudioClip clip)
        {
            if (clip == null) return;
            _ambience.clip = clip;
            _ambience.volume = ambienceVolume;
            _ambience.Play();
        }

        /// <summary>Toca um efeito com leve variação de tom para não soar repetitivo.</summary>
        public void PlaySfx(AudioClip clip, float volume = 1f, float pitchVariation = 0.06f)
        {
            if (clip == null || _sfx == null) return;
            var src = _sfx[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _sfx.Length;
            src.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            src.PlayOneShot(clip, volume * sfxVolume);
        }

        private IEnumerator Crossfade(AudioSource from, AudioSource to, float duration)
        {
            float t = 0f;
            float fromStart = from != null ? from.volume : 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = duration <= 0f ? 1f : t / duration;
                if (from != null) from.volume = Mathf.Lerp(fromStart, 0f, k);
                if (to != null) to.volume = Mathf.Lerp(0f, musicVolume, k);
                yield return null;
            }
            if (from != null) { from.Stop(); from.volume = 0f; }
            if (to != null) to.volume = musicVolume;
        }
    }
}
