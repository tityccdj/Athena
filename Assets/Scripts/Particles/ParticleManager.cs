using System;
using System.Collections;
using System.Collections.Generic;
using Athena.Boss.Core;
using Athena.Player;
using UnityEngine;

namespace Athena.Particles
{
    [Serializable]
    public class ParticleEntry
    {
        [Tooltip("ชื่อ ID ของ Particle เช่น 'PlayerHit', 'PlayerJumpDust', 'DashSparks', 'BossHit'")]
        public string Name;

        [Tooltip("Prefab ของ ParticleSystem หรือ Visual Effect ที่ต้องการเล่น")]
        public GameObject Prefab;

        [Tooltip("จำนวนที่สร้างเตรียมไว้ล่วงหน้า (Object Pooling) 0 = สร้างเมื่อเรียกใช้")]
        public int PrewarmCount = 0;

        [Tooltip("ระยะเวลาบังคับคืน Pool (วินาที, 0 = คำนวณตาม ParticleSystem ให้อัตโนมัติ)")]
        public float CustomDuration = 0f;
    }

    /// <summary>
    /// ควบคุมและจัดการการแสดงผล Particle Effects / VFX ในเกมทั้งหมด:
    /// 1. มีระบบ Object Pooling ช่วยลดการ Garbage Collection และอาการหน่วง (Lag)
    /// 2. มีฟังก์ชันรองรับทั้งการเรียกผ่านโค้ด C# และการเชื่อมต่อผ่าน UnityEvent ใน Inspector
    /// 3. ผู้เล่นสามารถตั้งชื่อ Particle Entry และใส่ Prefab ใน Inspector ได้ตามต้องการ
    /// </summary>
    public class ParticleManager : MonoBehaviour
    {
        public static ParticleManager Instance { get; private set; }

        [Header("Particle Presets (รายการ Particle ที่ลงทะเบียนไว้)")]
        [SerializeField] private List<ParticleEntry> _particlePresets = new List<ParticleEntry>();

        [Header("Settings")]
        [Tooltip("ถ้าติ๊กถูก จะไม่ทำลาย ParticleManager เมื่อเปลี่ยน Scene")]
        [SerializeField] private bool _dontDestroyOnLoad = false;

        private readonly Dictionary<string, ParticleEntry> _entryLookup = new Dictionary<string, ParticleEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Queue<GameObject>> _pools = new Dictionary<string, Queue<GameObject>>(StringComparer.OrdinalIgnoreCase);
        private Transform _poolContainer;

        private PlayerController _cachedPlayer;

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
            GameObject containerObj = new GameObject("[Particle_Pool]");
            containerObj.transform.SetParent(transform);
            _poolContainer = containerObj.transform;

            InitializePools();
        }

        private void InitializePools()
        {
            _entryLookup.Clear();
            _pools.Clear();

            foreach (var entry in _particlePresets)
            {
                if (string.IsNullOrWhiteSpace(entry.Name) || entry.Prefab == null) continue;

                _entryLookup[entry.Name] = entry;
                _pools[entry.Name] = new Queue<GameObject>();

                // Prewarm instances
                for (int i = 0; i < entry.PrewarmCount; i++)
                {
                    GameObject obj = Instantiate(entry.Prefab, _poolContainer);
                    obj.name = $"{entry.Name}_Pooled";
                    obj.SetActive(false);
                    _pools[entry.Name].Enqueue(obj);
                }
            }
        }

        // ─── Core Play Methods ──────────────────────────

        /// <summary>
        /// สั่งเล่น Particle ตามชื่อ Preset ที่ระบุ
        /// </summary>
        public GameObject PlayParticle(string particleName, Vector3 position, Quaternion rotation = default, Transform parent = null)
        {
            if (string.IsNullOrWhiteSpace(particleName)) return null;

            if (!_entryLookup.TryGetValue(particleName, out var entry))
            {
                UnityEngine.Debug.LogWarning($"[ParticleManager] Particle name '{particleName}' not found in presets!");
                return null;
            }

            if (entry.Prefab == null) return null;

            GameObject instance = GetOrCreateInstance(particleName, entry);
            if (instance == null) return null;

            // กำหนดตำแหน่งและการหมุน
            if (rotation == default) rotation = Quaternion.identity;

            if (parent != null)
            {
                instance.transform.SetParent(parent, false);
                instance.transform.position = position;
                instance.transform.rotation = rotation;
                if (entry.Prefab != null)
                {
                    instance.transform.localScale = entry.Prefab.transform.localScale;
                }
            }
            else
            {
                instance.transform.SetParent(_poolContainer, false);
                instance.transform.position = position;
                instance.transform.rotation = rotation;
                if (entry.Prefab != null)
                {
                    instance.transform.localScale = entry.Prefab.transform.localScale;
                }
            }

            instance.SetActive(true);

            // สั่งเล่น ParticleSystem
            ParticleSystem[] psList = instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in psList)
            {
                ps.Clear();
                ps.Play();
            }

            // คำนวณระยะเวลาแล้วคืนสู่ Pool
            float duration = entry.CustomDuration > 0f ? entry.CustomDuration : CalculateMaxDuration(psList);
            StartCoroutine(ReturnToPoolRoutine(particleName, instance, duration));

            return instance;
        }

        /// <summary>
        /// สั่งเล่น Particle โดยตรงจาก Prefab โดยไม่ต้องลงทะเบียน Preset
        /// </summary>
        public GameObject PlayPrefab(GameObject prefab, Vector3 position, Quaternion rotation = default, Transform parent = null)
        {
            if (prefab == null) return null;

            if (rotation == default) rotation = Quaternion.identity;

            GameObject instance = Instantiate(prefab, position, rotation, parent != null ? parent : _poolContainer);
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            instance.transform.localScale = prefab.transform.localScale;
            instance.SetActive(true);

            ParticleSystem[] psList = instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in psList)
            {
                ps.Clear();
                ps.Play();
            }

            float duration = CalculateMaxDuration(psList);
            Destroy(instance, duration > 0f ? duration : 2.0f);

            return instance;
        }

        // ─── UnityEvent Friendly Methods ────────────────

        /// <summary>
        /// สั่งเล่น Particle ตามชื่อ (เล่นที่ตำแหน่ง Player หรือตัว Manager) - เหมาะสำหรับ UnityEvent (string)
        /// </summary>
        public void PlayParticle(string particleName)
        {
            Vector3 spawnPos = GetDefaultSpawnPosition();
            PlayParticle(particleName, spawnPos);
        }

        /// <summary>
        /// สั่งเล่น Particle ตามชื่อที่ตำแหน่งระบุ
        /// </summary>
        public void PlayParticleAtPosition(string particleName, Vector3 position)
        {
            PlayParticle(particleName, position);
        }

        /// <summary>
        /// สั่งเล่น Particle ตามชื่อ ณ ตำแหน่ง Transform ที่ระบุ
        /// </summary>
        public void PlayParticleAtTransform(string particleName, Transform target)
        {
            if (target == null)
            {
                PlayParticle(particleName);
                return;
            }

            PlayParticle(particleName, target.position, target.rotation);
        }

        /// <summary>
        /// สั่งเล่น Particle ณ ตำแหน่งของ Player ปัจจุบัน
        /// </summary>
        public void PlayParticleAtPlayer(string particleName)
        {
            EnsurePlayerCached();
            Vector3 pos = _cachedPlayer != null ? _cachedPlayer.transform.position : GetDefaultSpawnPosition();
            PlayParticle(particleName, pos);
        }

        /// <summary>
        /// สั่งเล่น Particle ณ ตำแหน่งของบอสใน Phase ปัจจุบัน
        /// </summary>
        public void PlayParticleAtBoss(string particleName)
        {
            Vector3 pos = GetDefaultSpawnPosition();
            if (BossManager.Instance != null)
            {
                GameObject bossObj = BossManager.Instance.GetCurrentPhaseObject();
                if (bossObj != null) pos = bossObj.transform.position;
            }

            PlayParticle(particleName, pos);
        }

        // ─── Helper Internal Methods ────────────────────

        private GameObject GetOrCreateInstance(string name, ParticleEntry entry)
        {
            if (!_pools.TryGetValue(name, out var queue))
            {
                queue = new Queue<GameObject>();
                _pools[name] = queue;
            }

            while (queue.Count > 0)
            {
                GameObject candidate = queue.Dequeue();
                if (candidate != null)
                {
                    return candidate;
                }
            }

            // ถ้าไม่มีในคิว ให้ instantiate ใหม่
            GameObject newObj = Instantiate(entry.Prefab, _poolContainer);
            newObj.name = $"{name}_Pooled";
            return newObj;
        }

        private IEnumerator ReturnToPoolRoutine(string name, GameObject instance, float delay)
        {
            if (delay > 0.05f)
            {
                yield return new WaitForSeconds(delay);
            }
            else
            {
                yield return null;
            }

            if (instance != null)
            {
                instance.SetActive(false);
                instance.transform.SetParent(_poolContainer);

                if (_pools.TryGetValue(name, out var queue))
                {
                    queue.Enqueue(instance);
                }
                else
                {
                    Destroy(instance);
                }
            }
        }

        private float CalculateMaxDuration(ParticleSystem[] psList)
        {
            if (psList == null || psList.Length == 0) return 1.5f;

            float maxTime = 0.5f;
            foreach (var ps in psList)
            {
                if (ps == null) continue;
                var main = ps.main;
                float duration = main.duration;
                float lifetime = main.startLifetime.constantMax;
                float total = duration + lifetime;
                if (total > maxTime) maxTime = total;
            }

            return maxTime;
        }

        private Vector3 GetDefaultSpawnPosition()
        {
            EnsurePlayerCached();
            if (_cachedPlayer != null) return _cachedPlayer.transform.position;
            return transform.position;
        }

        private void EnsurePlayerCached()
        {
            if (_cachedPlayer == null)
            {
                _cachedPlayer = FindFirstObjectByType<PlayerController>();
            }
        }
    }
}
