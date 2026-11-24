using UnityEngine;

namespace Athena.Particles
{
    /// <summary>
    /// Component เสริมสำหรับแปะบน GameObject ใดๆ (เช่น Player, Boss, อาวุธ, หรือพื้น)
    /// เพื่อให้สามารถผูกเรียกเล่น Particle ผ่าน UnityEvent หรือ Animation Event ได้ง่ายๆ โดยไม่ต้องเขียนโค้ดเพิ่ม
    /// </summary>
    public class ParticleEventTrigger : MonoBehaviour
    {
        [Header("Particle Settings")]
        [Tooltip("ชื่อ ID ของ Particle ใน ParticleManager ที่ต้องการเล่น")]
        [SerializeField] private string _particleName;

        [Tooltip("Prefab สำรองกรณีต้องการเล่น Prefab นี้โดยตรงโดยไม่ต้องผ่านชื่อใน ParticleManager")]
        [SerializeField] private GameObject _directPrefab;

        [Header("Spawn Position Settings")]
        [Tooltip("จุดกำเนิดของ Particle (ถ้าไม่ใส่จะใช้ตำแหน่งของ GameObject นี้)")]
        [SerializeField] private Transform _customSpawnPoint;

        [Tooltip("ระยะ Offset เพิ่มเติมจากจุดกำเนิด (เช่น เลื่อนขึ้นจากเท้า +0.2)")]
        [SerializeField] private Vector3 _offset = Vector3.zero;

        [Tooltip("หากติ๊กถูก จะผูก Particle ให้เคลื่อนที่ติดไปกับ Transform นี้")]
        [SerializeField] private bool _parentToThis = false;

        /// <summary>
        /// เล่น Particle ตามชื่อ _particleName หรือ _directPrefab ที่กำหนดไว้ (เหมาะสำหรับ Animation Event และ UnityEvent ที่ไม่มี Parameter)
        /// </summary>
        public void Play()
        {
            Vector3 spawnPos = GetSpawnPosition();
            Transform targetParent = _customSpawnPoint != null ? _customSpawnPoint : transform;
            Quaternion rot = targetParent.rotation;
            Transform parent = _parentToThis ? targetParent : null;

            if (_directPrefab != null && ParticleManager.Instance != null)
            {
                ParticleManager.Instance.PlayPrefab(_directPrefab, spawnPos, rot, parent);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_particleName) && ParticleManager.Instance != null)
            {
                ParticleManager.Instance.PlayParticle(_particleName, spawnPos, rot, parent);
            }
        }

        /// <summary>
        /// เล่น Particle ตามชื่อที่ระบุใน Parameter (เหมาะสำหรับ UnityEvent แบบรับค่า string)
        /// </summary>
        public void PlayNamed(string particleName)
        {
            if (string.IsNullOrWhiteSpace(particleName) || ParticleManager.Instance == null) return;

            Vector3 spawnPos = GetSpawnPosition();
            Transform targetParent = _customSpawnPoint != null ? _customSpawnPoint : transform;
            Quaternion rot = targetParent.rotation;
            Transform parent = _parentToThis ? targetParent : null;

            ParticleManager.Instance.PlayParticle(particleName, spawnPos, rot, parent);
        }

        /// <summary>
        /// เล่น Particle ณ ตำแหน่ง Vector3 ที่ส่งเข้ามา
        /// </summary>
        public void PlayAtPosition(Vector3 position)
        {
            if (ParticleManager.Instance == null) return;

            Transform targetParent = _customSpawnPoint != null ? _customSpawnPoint : transform;
            Transform parent = _parentToThis ? targetParent : null;
            Vector3 spawnPos = position + _offset;

            if (!string.IsNullOrWhiteSpace(_particleName))
            {
                ParticleManager.Instance.PlayParticle(_particleName, spawnPos, targetParent.rotation, parent);
            }
            else if (_directPrefab != null)
            {
                ParticleManager.Instance.PlayPrefab(_directPrefab, spawnPos, targetParent.rotation, parent);
            }
        }

        private Vector3 GetSpawnPosition()
        {
            Transform target = _customSpawnPoint != null ? _customSpawnPoint : transform;
            if (_offset == Vector3.zero) return target.position;
            return target.TransformPoint(_offset);
        }
    }
}
