using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Athena.Audio
{
    [Serializable]
    public class SFXEntry
    {
        [Tooltip("ชื่อ ID ของเสียง เช่น 'Player_Attack1', 'Player_Jump', 'Boss_Roar'")]
        public string Name;

        [Tooltip("AudioClip หลัก")]
        public AudioClip Clip;

        [Tooltip("AudioClip ทางเลือก (ถ้าใส่ระบบจะสุ่มเล่นจากรายการนี้หรือ Clip หลัก ช่วยลดความซ้ำซาก)")]
        public AudioClip[] Variations;

        [Range(0f, 1f)]
        [Tooltip("ความดังของเสียงนี้ (0.0 - 1.0)")]
        public float Volume = 1.0f;

        [Range(0.1f, 3f)]
        [Tooltip("ระดับ Pitch เริ่มต้น (1.0 = ปกติ)")]
        public float Pitch = 1.0f;

        [Range(0f, 0.5f)]
        [Tooltip("ความสุ่มของ Pitch (+/- จากค่า Pitch) ทำให้เสียงดูเป็นธรรมชาติ")]
        public float RandomPitchVariation = 0.05f;

        [Tooltip("คูลดาวน์ขั้นต่ำระหว่างการเล่นเสียงนี้ซ้ำ (วินาที) ป้องกันเสียงซ้อนทับกันจนแตก")]
        public float Cooldown = 0.03f;

        [Tooltip("AudioMixerGroup สำหรับจัดกลุ่มเสียง (ไม่ใส่ก็ได้)")]
        public AudioMixerGroup OutputGroup;

        [HideInInspector]
        public float LastPlayedTime = -999f;

        public SFXEntry() { }

        public SFXEntry(string name, float volume = 1f, float pitch = 1f, float pitchVar = 0.05f)
        {
            Name = name;
            Volume = volume;
            Pitch = pitch;
            RandomPitchVariation = pitchVar;
        }
    }

    /// <summary>
    /// ควบคุมและจัดการระบบเสียงเอฟเฟกต์ (SFX) ทั้งหมดในเกม:
    /// 1. มีระบบ AudioSource Pooling เพื่อประสิทธิภาพสูงสุด ไม่เกิดขยะในหน่วยความจำ (Garbage Collection)
    /// 2. มีระบบ Preset พร้อมชื่อ ID สามารถสุ่ม Variations และสุ่ม Pitch เล็กน้อยเพื่อความเป็นธรรมชาติ
    /// 3. รองรับการเชื่อมต่อกับ UnityEvent และ Animation Event ได้อย่างสมบูรณ์
    /// 4. ปลอดภัยกรณีที่ยังไม่ได้ใส่ไฟล์เสียง (ไม่เกิด Error / NullReferenceException)
    /// </summary>
    public class SFXManager : MonoBehaviour
    {
        public static SFXManager Instance { get; private set; }

        [Header("Master Volume Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float _masterSFXVolume = 1.0f;

        [Tooltip("ถ้าติ๊กถูก จะไม่ทำลาย SFXManager เมื่อเปลี่ยน Scene")]
        [SerializeField] private bool _dontDestroyOnLoad = false;

        [Header("AudioSource Pool Settings")]
        [Tooltip("จำนวน AudioSource ที่สร้างเตรียมไว้ใน Pool")]
        [SerializeField] private int _initialPoolSize = 16;
        [Tooltip("ขีดจำกัดจำนวน AudioSource สูงสุดใน Pool")]
        [SerializeField] private int _maxPoolSize = 32;

        [Header("Default AudioMixer Group")]
        [Tooltip("Mixer Group เริ่มต้นสำหรับเสียงที่ไม่ได้กำหนด Group เฉพาะ")]
        [SerializeField] private AudioMixerGroup _defaultSFXGroup;

        [Header("SFX Presets (รายการเสียงในเกม)")]
        [SerializeField] private List<SFXEntry> _sfxPresets = new List<SFXEntry>();

        private readonly Dictionary<string, SFXEntry> _entryLookup = new Dictionary<string, SFXEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly List<AudioSource> _sourcePool = new List<AudioSource>();
        private Transform _poolContainer;

        public float MasterSFXVolume
        {
            get => _masterSFXVolume;
            set => _masterSFXVolume = Mathf.Clamp01(value);
        }

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

            // สร้าง Container จัดระเบียบ Hierarchy
            GameObject containerObj = new GameObject("[SFX_Pool]");
            containerObj.transform.SetParent(transform);
            _poolContainer = containerObj.transform;

            InitializeLookup();
            InitializePool();
        }

        private void InitializeLookup()
        {
            _entryLookup.Clear();
            foreach (var entry in _sfxPresets)
            {
                if (string.IsNullOrWhiteSpace(entry.Name)) continue;
                _entryLookup[entry.Name] = entry;
            }
        }

        private void InitializePool()
        {
            _sourcePool.Clear();
            for (int i = 0; i < _initialPoolSize; i++)
            {
                CreateNewPooledSource();
            }
        }

        private AudioSource CreateNewPooledSource()
        {
            GameObject sourceObj = new GameObject($"SFX_Source_{_sourcePool.Count}");
            sourceObj.transform.SetParent(_poolContainer);
            AudioSource source = sourceObj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f; // Default 2D
            _sourcePool.Add(source);
            return source;
        }

        private AudioSource GetAvailableSource()
        {
            // 1. หา AudioSource ใน Pool ที่ว่างอยู่
            for (int i = 0; i < _sourcePool.Count; i++)
            {
                if (_sourcePool[i] != null && !_sourcePool[i].isPlaying)
                {
                    return _sourcePool[i];
                }
            }

            // 2. ถ้าเต็มแต่ยังไม่เกิน Max Pool Size ให้สร้างเพิ่ม
            if (_sourcePool.Count < _maxPoolSize)
            {
                return CreateNewPooledSource();
            }

            // 3. ถ้าถึงขีดจำกัดแล้ว ให้ใช้ตัวแรกที่มีความสำคัญน้อยที่สุด หรือเล่นใกล้จบที่สุด
            return _sourcePool[0];
        }

        // ─── Playback Methods ──────────────────────────────────────────────

        /// <summary>
        /// สั่งเล่น SFX ตามชื่อ Preset (เหมาะสำหรับ UnityEvent และโค้ดทั่วไป)
        /// </summary>
        public AudioSource PlaySFX(string sfxName)
        {
            return PlaySFXInternal(sfxName, null, 1f, 1f, false, Vector3.zero);
        }

        /// <summary>
        /// สั่งเล่น SFX ตามชื่อ Preset พร้อมระบุตัวคูณความดัง
        /// </summary>
        public AudioSource PlaySFX(string sfxName, float volumeMultiplier)
        {
            return PlaySFXInternal(sfxName, null, volumeMultiplier, 1f, false, Vector3.zero);
        }

        /// <summary>
        /// สั่งเล่น SFX ตามชื่อ Preset ณ พิกัดในฉาก (3D Positional Sound)
        /// </summary>
        public AudioSource PlaySFXAtPosition(string sfxName, Vector3 position, float volumeMultiplier = 1f)
        {
            return PlaySFXInternal(sfxName, null, volumeMultiplier, 1f, true, position);
        }

        /// <summary>
        /// เล่น AudioClip โดยตรง ไม่ต้องผ่าน Preset
        /// </summary>
        public AudioSource PlayClip(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            return PlayClipInternal(clip, volume, pitch, false, Vector3.zero);
        }

        /// <summary>
        /// เล่น AudioClip โดยตรง ณ พิกัดในฉาก
        /// </summary>
        public AudioSource PlayClipAtPosition(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            return PlayClipInternal(clip, volume, pitch, true, position);
        }

        /// <summary>
        /// ฟังก์ชัน Alias สำหรับ UnityEvent (รับค่า string)
        /// </summary>
        public void Play(string sfxName)
        {
            PlaySFX(sfxName);
        }

        /// <summary>
        /// หยุดเสียง SFX ทั้งหมดที่กำลังเล่นอยู่
        /// </summary>
        public void StopAllSFX()
        {
            foreach (var src in _sourcePool)
            {
                if (src != null && src.isPlaying)
                {
                    src.Stop();
                }
            }
        }

        public void SetMasterSFXVolume(float volume)
        {
            MasterSFXVolume = volume;
        }

        // ─── Internal Implementation ───────────────────────────────────────

        private AudioSource PlaySFXInternal(string sfxName, AudioClip directClip, float volumeMultiplier, float pitchMultiplier, bool is3D, Vector3 position)
        {
            if (string.IsNullOrWhiteSpace(sfxName) && directClip == null) return null;

            SFXEntry entry = null;
            if (!string.IsNullOrWhiteSpace(sfxName))
            {
                if (!_entryLookup.TryGetValue(sfxName, out entry))
                {
                    // ถ้ายังไม่มีใน Dictionary ให้ลองค้นหาใน List เผื่อมีการเพิ่ม runtime
                    InitializeLookup();
                    _entryLookup.TryGetValue(sfxName, out entry);
                }

                if (entry != null)
                {
                    // Spam protection cooldown
                    if (Time.unscaledTime < entry.LastPlayedTime + entry.Cooldown)
                    {
                        return null;
                    }
                    entry.LastPlayedTime = Time.unscaledTime;
                }
            }

            AudioClip clipToPlay = directClip;
            float baseVolume = 1f;
            float basePitch = 1f;
            float pitchVariation = 0.05f;
            AudioMixerGroup mixerGroup = _defaultSFXGroup;

            if (entry != null)
            {
                clipToPlay = GetRandomClipFromEntry(entry);
                baseVolume = entry.Volume;
                basePitch = entry.Pitch;
                pitchVariation = entry.RandomPitchVariation;
                if (entry.OutputGroup != null) mixerGroup = entry.OutputGroup;
            }

            // ถ้าไม่มีไฟล์เสียงให้เล่น ให้ข้ามไปเงียบๆ (ไม่ฟ้อง Error)
            if (clipToPlay == null) return null;

            AudioSource source = GetAvailableSource();
            if (source == null) return null;

            source.clip = clipToPlay;
            source.outputAudioMixerGroup = mixerGroup;

            float finalVolume = baseVolume * volumeMultiplier * _masterSFXVolume;
            source.volume = Mathf.Clamp01(finalVolume);

            float randomPitch = (pitchVariation > 0f) ? UnityEngine.Random.Range(-pitchVariation, pitchVariation) : 0f;
            source.pitch = Mathf.Clamp(basePitch * pitchMultiplier + randomPitch, 0.1f, 3f);

            source.loop = false;

            if (is3D)
            {
                source.spatialBlend = 1f;
                source.transform.position = position;
                source.minDistance = 3f;
                source.maxDistance = 25f;
            }
            else
            {
                source.spatialBlend = 0f;
            }

            source.Play();
            return source;
        }

        private AudioSource PlayClipInternal(AudioClip clip, float volume, float pitch, bool is3D, Vector3 position)
        {
            if (clip == null) return null;
            return PlaySFXInternal(null, clip, volume, pitch, is3D, position);
        }

        private AudioClip GetRandomClipFromEntry(SFXEntry entry)
        {
            if (entry.Variations != null && entry.Variations.Length > 0)
            {
                int totalCandidates = entry.Variations.Length + (entry.Clip != null ? 1 : 0);
                int randomIndex = UnityEngine.Random.Range(0, totalCandidates);

                if (entry.Clip != null && randomIndex == 0)
                {
                    return entry.Clip;
                }

                int variationIndex = (entry.Clip != null) ? randomIndex - 1 : randomIndex;
                AudioClip selected = entry.Variations[variationIndex];
                return selected != null ? selected : entry.Clip;
            }

            return entry.Clip;
        }

        // ─── Default Preset Initializer (Reset Context Menu) ───────────────

        /// <summary>
        /// เตรียมโครงสร้าง Preset พื้นฐานของเกมให้อัตโนมัติเมื่อกด Reset หรือใส่ Component ครั้งแรก
        /// </summary>
        private void Reset()
        {
            if (_sfxPresets == null || _sfxPresets.Count == 0)
            {
                _sfxPresets = new List<SFXEntry>
                {
                    // Player Actions
                    new SFXEntry("Player_Attack1", 1f, 1f, 0.06f),
                    new SFXEntry("Player_Attack2", 1f, 0.95f, 0.06f),
                    new SFXEntry("Player_Dash", 0.9f, 1.1f, 0.05f),
                    new SFXEntry("Player_DashAttack", 1f, 1f, 0.05f),
                    new SFXEntry("Player_Jump", 0.8f, 1.05f, 0.04f),
                    new SFXEntry("Player_Land", 0.7f, 0.95f, 0.04f),
                    new SFXEntry("Player_Hurt", 1f, 1f, 0.08f),
                    new SFXEntry("Player_Heal", 1f, 1f, 0.02f),
                    new SFXEntry("Player_Death", 1f, 0.9f, 0f),
                    new SFXEntry("Player_Projectile_Shoot", 0.9f, 1.1f, 0.05f),
                    new SFXEntry("Player_Projectile_Hit", 1f, 1f, 0.06f),

                    // Combat Impact
                    new SFXEntry("Hit_Impact", 1f, 1f, 0.08f),

                    // Boss Phase 1 (Orc Rider)
                    new SFXEntry("Boss1_Telegraph", 0.9f, 0.9f, 0.03f),
                    new SFXEntry("Boss1_Cleave", 1f, 0.95f, 0.05f),
                    new SFXEntry("Boss1_Spin", 1f, 1.05f, 0.05f),
                    new SFXEntry("Boss1_Charge", 1f, 1f, 0.04f),
                    new SFXEntry("Boss1_Hurt", 0.9f, 0.9f, 0.05f),
                    new SFXEntry("Boss1_Death", 1f, 0.85f, 0f),

                    // Boss Phase 2 (Elite Orc)
                    new SFXEntry("Boss2_Telegraph", 0.9f, 0.85f, 0.03f),
                    new SFXEntry("Boss2_Cleave", 1f, 0.9f, 0.05f),
                    new SFXEntry("Boss2_Tornado", 1f, 1.1f, 0.05f),
                    new SFXEntry("Boss2_Shockwave", 1f, 0.95f, 0.05f),
                    new SFXEntry("Boss2_Death", 1f, 0.8f, 0f),

                    // Boss Phase 3 (Necromancer)
                    new SFXEntry("Boss3_Teleport", 0.9f, 1.1f, 0.05f),
                    new SFXEntry("Boss3_MeteorCast", 1f, 1f, 0.04f),
                    new SFXEntry("Boss3_Explosion", 1f, 0.95f, 0.06f),
                    new SFXEntry("Boss3_Death", 1f, 0.75f, 0f),

                    // UI & Environment
                    new SFXEntry("UI_Click", 0.8f, 1f, 0f),
                    new SFXEntry("Gate_Open", 1f, 0.9f, 0f),
                    new SFXEntry("Gate_Close", 1f, 0.9f, 0f)
                };
            }
        }
    }
}
