using System;
using System.Collections;
using Athena.Boss.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace Athena.Audio
{
    /// <summary>
    /// ควบคุมและจัดการระบบเพลงประกอบ (Background Music / BGM) ในเกมทั้งหมด:
    /// 1. มีระบบ Crossfade (เปลี่ยนเพลงแบบค่อยๆ สลับเสียงเบาและดังอย่างนุ่มนวล) โดยใช้ Dual AudioSources
    /// 2. มีช่องตั้งระดับเสียงเฉพาะ (Volume Slider) แยกอิสระสำหรับแต่ละเพลง แก้ปัญหาไฟล์เพลงดัง-เบาไม่เท่ากัน
    /// 3. รองรับการสลับเพลงตาม Phase ต่างๆ ของ Boss (Exploration, Phase 1, Phase 2, Ghost Transition, Phase 3, Victory)
    /// 4. ซิงค์กับ BossManager อัตโนมัติ หรือจะเรียกผ่านโค้ดและ UnityEvent ก็ได้
    /// 5. ปลอดภัยกรณีที่ยังไม่ได้ใส่ไฟล์เพลง (ไม่ฟ้อง Error)
    /// </summary>
    public class MusicManager : MonoBehaviour
    {
        public static MusicManager Instance { get; private set; }

        [Header("Master Settings")]
        [Range(0f, 1f)]
        [Tooltip("ระดับเสียงเพลงประกอบรวมทั้งหมด (Master BGM Volume)")]
        [SerializeField] private float _masterMusicVolume = 0.8f;

        [Tooltip("ระยะเวลาการ Crossfade สลับเพลง (วินาที)")]
        [SerializeField] private float _defaultCrossfadeDuration = 1.5f;

        [Tooltip("ถ้าติ๊กถูก จะไม่ทำลาย MusicManager เมื่อเปลี่ยน Scene")]
        [SerializeField] private bool _dontDestroyOnLoad = false;

        [Tooltip("AudioMixerGroup สำหรับดนตรีประกอบ (ถ้ามี)")]
        [SerializeField] private AudioMixerGroup _musicMixerGroup;

        [Header("1. Exploration Track (ช่วงเดินสำรวจ)")]
        [Tooltip("เพลงช่วงสำรวจ / ก่อนเริ่มสู้บอส (Room 1 หรือ ทางเดิน)")]
        [SerializeField] private AudioClip _explorationMusic;
        [Range(0f, 1f)]
        [Tooltip("ระดับเสียงเฉพาะของเพลงสำรวจ (0.0 - 1.0)")]
        [SerializeField] private float _explorationVolume = 0.8f;

        [Header("2. Boss Phase 1 Track: Orc Rider")]
        [Tooltip("เพลงบอส Phase 1: Orc Rider")]
        [SerializeField] private AudioClip _phase1Music;
        [Range(0f, 1f)]
        [Tooltip("ระดับเสียงเฉพาะของเพลงบอส Phase 1")]
        [SerializeField] private float _phase1Volume = 1.0f;

        [Header("3. Boss Phase 2 Track: Elite Orc")]
        [Tooltip("เพลงบอส Phase 2: Elite Orc")]
        [SerializeField] private AudioClip _phase2Music;
        [Range(0f, 1f)]
        [Tooltip("ระดับเสียงเฉพาะของเพลงบอส Phase 2")]
        [SerializeField] private float _phase2Volume = 1.0f;

        [Header("4. Ghost Transition Track (ผีลอยข้ามห้อง)")]
        [Tooltip("เพลงช่วงผีบินข้ามห้องจาก Room 2 ไป Room 3 (ถ้าไม่ใส่จะเล่นเพลงเดิม)")]
        [SerializeField] private AudioClip _ghostTransitionMusic;
        [Range(0f, 1f)]
        [Tooltip("ระดับเสียงเฉพาะของเพลงช่วงผีบิน")]
        [SerializeField] private float _ghostTransitionVolume = 0.9f;

        [Header("5. Boss Phase 3 Track: Necromancer")]
        [Tooltip("เพลงบอส Phase 3: Necromancer")]
        [SerializeField] private AudioClip _phase3Music;
        [Range(0f, 1f)]
        [Tooltip("ระดับเสียงเฉพาะของเพลงบอส Phase 3")]
        [SerializeField] private float _phase3Volume = 1.0f;

        [Header("6. Victory Track (ชนะเกม)")]
        [Tooltip("เพลงเมื่อชนะบอสครบทุก Phase (Victory)")]
        [SerializeField] private AudioClip _victoryMusic;
        [Range(0f, 1f)]
        [Tooltip("ระดับเสียงเฉพาะของเพลงชนะเกม")]
        [SerializeField] private float _victoryVolume = 1.0f;

        [Header("Auto-Sync Settings")]
        [Tooltip("ให้ MusicManager ดักฟังและสลับเพลงตาม BossManager โดยอัตโนมัติหรือไม่")]
        [SerializeField] private bool _autoSyncWithBossManager = true;

        private AudioSource _sourceA;
        private AudioSource _sourceB;
        private bool _isUsingSourceA = true;
        private Coroutine _fadeCoroutine;
        private AudioClip _currentClip;
        private float _currentTrackVolume = 1.0f;

        public float MasterMusicVolume
        {
            get => _masterMusicVolume;
            set
            {
                _masterMusicVolume = Mathf.Clamp01(value);
                UpdateActiveSourceVolume();
            }
        }

        public AudioClip CurrentClip => _currentClip;
        public float CurrentTrackVolume => _currentTrackVolume;

        // Individual volume properties
        public float ExplorationVolume { get => _explorationVolume; set => _explorationVolume = Mathf.Clamp01(value); }
        public float Phase1Volume { get => _phase1Volume; set => _phase1Volume = Mathf.Clamp01(value); }
        public float Phase2Volume { get => _phase2Volume; set => _phase2Volume = Mathf.Clamp01(value); }
        public float GhostTransitionVolume { get => _ghostTransitionVolume; set => _ghostTransitionVolume = Mathf.Clamp01(value); }
        public float Phase3Volume { get => _phase3Volume; set => _phase3Volume = Mathf.Clamp01(value); }
        public float VictoryVolume { get => _victoryVolume; set => _victoryVolume = Mathf.Clamp01(value); }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            SetupAudioSources();
        }

        private void SetupAudioSources()
        {
            GameObject container = new GameObject("[Music_Sources]");
            container.transform.SetParent(transform);

            _sourceA = container.AddComponent<AudioSource>();
            _sourceB = container.AddComponent<AudioSource>();

            ConfigureSource(_sourceA);
            ConfigureSource(_sourceB);
        }

        private void ConfigureSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f; // 2D Stereo BGM
            source.volume = 0f;
            source.outputAudioMixerGroup = _musicMixerGroup;
        }

        private void Start()
        {
            if (_autoSyncWithBossManager && BossManager.Instance != null)
            {
                BossManager.Instance.OnPhaseChanged += HandlePhaseChanged;
                BossManager.Instance.OnBossDefeatedAll += HandleBossDefeatedAll;
            }

            // เริ่มต้นถ้ามี Exploration Music ให้เล่นทันที
            if (_explorationMusic != null)
            {
                PlayExplorationMusic(_defaultCrossfadeDuration);
            }
        }

        private void OnDestroy()
        {
            if (_autoSyncWithBossManager && BossManager.Instance != null)
            {
                BossManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
                BossManager.Instance.OnBossDefeatedAll -= HandleBossDefeatedAll;
            }
        }

        private void HandlePhaseChanged(int phase)
        {
            PlayPhaseMusic(phase);
        }

        private void HandleBossDefeatedAll()
        {
            PlayVictoryMusic();
        }

        // ─── Public Playback Methods ──────────────────────────────────────

        /// <summary>
        /// สลับเพลงตาม Phase ของบอส (1 = Phase 1, 2 = Phase 2, 3 = Phase 3) พร้อมใช้ Volume เฉพาะของเฟสนั้น
        /// </summary>
        public void PlayPhaseMusic(int phase, float fadeDuration = -1f)
        {
            float duration = fadeDuration >= 0f ? fadeDuration : _defaultCrossfadeDuration;

            switch (phase)
            {
                case 1:
                    if (_phase1Music != null) PlayMusic(_phase1Music, _phase1Volume, duration);
                    break;
                case 2:
                    if (_phase2Music != null) PlayMusic(_phase2Music, _phase2Volume, duration);
                    break;
                case 3:
                    if (_phase3Music != null) PlayMusic(_phase3Music, _phase3Volume, duration);
                    break;
            }
        }

        public void PlayExplorationMusic(float fadeDuration = -1f)
        {
            float duration = fadeDuration >= 0f ? fadeDuration : _defaultCrossfadeDuration;
            if (_explorationMusic != null)
            {
                PlayMusic(_explorationMusic, _explorationVolume, duration);
            }
        }

        public void PlayGhostTransitionMusic(float fadeDuration = -1f)
        {
            float duration = fadeDuration >= 0f ? fadeDuration : _defaultCrossfadeDuration;
            if (_ghostTransitionMusic != null)
            {
                PlayMusic(_ghostTransitionMusic, _ghostTransitionVolume, duration);
            }
        }

        public void PlayVictoryMusic(float fadeDuration = -1f)
        {
            float duration = fadeDuration >= 0f ? fadeDuration : _defaultCrossfadeDuration;
            if (_victoryMusic != null)
            {
                PlayMusic(_victoryMusic, _victoryVolume, duration, loop: false);
            }
        }

        /// <summary>
        /// สลับไปเล่นเพลงที่กำหนดแบบ Crossfade พร้อมระบุระดับเสียงเฉพาะเพลง (trackVolume)
        /// </summary>
        public void PlayMusic(AudioClip newClip, float trackVolume = 1.0f, float fadeDuration = 1.5f, bool loop = true)
        {
            if (newClip == null) return;
            if (_currentClip == newClip && (_sourceA.isPlaying || _sourceB.isPlaying)) return;

            _currentClip = newClip;
            _currentTrackVolume = Mathf.Clamp01(trackVolume);

            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }
            _fadeCoroutine = StartCoroutine(CrossfadeRoutine(newClip, _currentTrackVolume, fadeDuration, loop));
        }

        /// <summary>
        /// ค่อยๆ ดับเพลงทั้งหมดลงตามระยะเวลาที่กำหนด
        /// </summary>
        public void StopMusic(float fadeDuration = 1.0f)
        {
            _currentClip = null;
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }
            _fadeCoroutine = StartCoroutine(FadeOutRoutine(fadeDuration));
        }

        public void SetMasterVolume(float volume)
        {
            MasterMusicVolume = volume;
        }

        /// <summary>
        /// ปรับระดับเสียงเฉพาะเพลงของบอสในแต่ละ Phase แบบ Realtime
        /// </summary>
        public void SetPhaseVolume(int phase, float volume)
        {
            float clamped = Mathf.Clamp01(volume);
            switch (phase)
            {
                case 1: _phase1Volume = clamped; break;
                case 2: _phase2Volume = clamped; break;
                case 3: _phase3Volume = clamped; break;
            }

            // ถ้ากำลังเล่นเพลงเฟสนั้นอยู่ ให้อัปเดตความดังทันที
            if (BossManager.Instance != null && BossManager.Instance.CurrentPhase == phase)
            {
                _currentTrackVolume = clamped;
                UpdateActiveSourceVolume();
            }
        }

        // ─── Crossfade Coroutines ──────────────────────────────────────────

        private IEnumerator CrossfadeRoutine(AudioClip newClip, float trackVolume, float duration, bool loop)
        {
            AudioSource fadeOutSource = _isUsingSourceA ? _sourceA : _sourceB;
            AudioSource fadeInSource = _isUsingSourceA ? _sourceB : _sourceA;
            _isUsingSourceA = !_isUsingSourceA;

            float targetVolume = trackVolume * _masterMusicVolume;

            fadeInSource.clip = newClip;
            fadeInSource.loop = loop;
            fadeInSource.volume = 0f;
            fadeInSource.Play();

            float elapsed = 0f;
            float startOutVolume = fadeOutSource.volume;

            if (duration <= 0.05f)
            {
                fadeInSource.volume = targetVolume;
                fadeOutSource.volume = 0f;
                fadeOutSource.Stop();
            }
            else
            {
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);

                    fadeInSource.volume = Mathf.Lerp(0f, targetVolume, t);
                    fadeOutSource.volume = Mathf.Lerp(startOutVolume, 0f, t);

                    yield return null;
                }

                fadeInSource.volume = targetVolume;
                fadeOutSource.volume = 0f;
                fadeOutSource.Stop();
            }

            _fadeCoroutine = null;
        }

        private IEnumerator FadeOutRoutine(float duration)
        {
            AudioSource activeSource = _isUsingSourceA ? _sourceA : _sourceB;
            float startVol = activeSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                activeSource.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
                yield return null;
            }

            activeSource.volume = 0f;
            activeSource.Stop();
            _sourceA.Stop();
            _sourceB.Stop();
            _fadeCoroutine = null;
        }

        private void UpdateActiveSourceVolume()
        {
            if (_fadeCoroutine == null)
            {
                AudioSource active = _isUsingSourceA ? _sourceA : _sourceB;
                if (active != null && active.isPlaying)
                {
                    active.volume = _currentTrackVolume * _masterMusicVolume;
                }
            }
        }
    }
}
