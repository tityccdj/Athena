using System;
using Athena.Player;
using Athena.Rooms;
using UnityEngine;

namespace Athena.Boss.Core
{
    /// <summary>
    /// ควบคุมภาพรวมของ Boss Fight ทั้ง 3 Phase ตาม GDD:
    /// - แบ่งแต่ละ Phase เป็น GameObject แยกกัน และใช้คำสั่ง SetActive สลับตัวเมื่อเปลี่ยน Phase
    /// - จัดการกรณี Player ตาย: เริ่มใหม่ที่ Phase ล่าสุดของบอส (เลือดบอสจะรีเซ็ตกลับเต็ม MAX ของ Phase นั้นๆ)
    /// - จัดการประตูกั้นห้อง RoomGate
    /// </summary>
    public class BossManager : MonoBehaviour
    {
        public static BossManager Instance { get; private set; }

        [Header("Phase GameObjects (GDD: SetActive สลับตัวเมื่อเปลี่ยน Phase)")]
        [SerializeField] private GameObject _phase1Object;
        [SerializeField] private GameObject _phase2Object;
        [SerializeField] private GameObject _phase3Object;

        [Header("Spawn Points")]
        [SerializeField] private Transform _phase1SpawnPoint;
        [SerializeField] private Transform _phase2SpawnPoint;
        [SerializeField] private Transform _phase3SpawnPoint;

        [Header("Room Gate")]
        [SerializeField] private RoomGate _bossRoomGate;

        [Header("Player")]
        [SerializeField] private PlayerController _player;

        private int _currentPhase = 1;
        private bool _isFightActive = false;

        public int CurrentPhase => _currentPhase;
        public bool IsFightActive => _isFightActive;

        public event Action<int> OnPhaseChanged; // new phase index (1, 2, 3)
        public event Action OnBossDefeatedAll;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_player == null)
            {
                _player = FindFirstObjectByType<PlayerController>();
            }

            if (_player != null)
            {
                _player.Health.OnDeath += HandlePlayerDeath;
            }
        }

        private void OnDestroy()
        {
            if (_player != null)
            {
                _player.Health.OnDeath -= HandlePlayerDeath;
            }
        }

        /// <summary>
        /// เริ่มต้นการต่อสู้กับบอส (เช่น เมื่อ Player เดินเข้าห้องบอส)
        /// </summary>
        public void StartBossFight()
        {
            _isFightActive = true;

            // ปิดประตูกั้นห้อง
            if (_bossRoomGate != null)
            {
                _bossRoomGate.CloseGate();
            }

            // เปิดเฉพาะ Phase ปัจจุบัน
            ActivateCurrentPhaseObject();

            UnityEngine.Debug.Log($"[BossManager] Boss Fight Started! (Phase {_currentPhase})");
        }

        /// <summary>
        /// เปลี่ยนไปสู่ Phase ถัดไป (เรียกเมื่อ Boss ในเฟสปัจจุบันถูกกำจัด)
        /// </summary>
        public void AdvanceToNextPhase()
        {
            int nextPhase = _currentPhase + 1;

            if (nextPhase > 3)
            {
                // บอสตายครบ 3 เฟส -> ชนะเกม
                _isFightActive = false;
                OnBossDefeatedAll?.Invoke();
                UnityEngine.Debug.Log("[BossManager] ALL BOSS PHASES DEFEATED! YOU WIN!");
                return;
            }

            SwitchToPhase(nextPhase);
        }

        public void SwitchToPhase(int phaseIndex)
        {
            GameObject prevObj = GetCurrentPhaseObject();
            Vector3 lastPos = prevObj != null ? prevObj.transform.position : Vector3.zero;

            _currentPhase = phaseIndex;
            ActivateCurrentPhaseObject();

            // ให้ตัว Phase ใหม่เกิดตรงจุดที่ตัวเดิมตาย (เพื่อความต่อเนื่องในการต่อสู้)
            GameObject newObj = GetCurrentPhaseObject();
            if (newObj != null && prevObj != null)
            {
                Transform designatedSpawn = GetCurrentPhaseSpawnPoint();
                newObj.transform.position = (designatedSpawn != null) ? designatedSpawn.position : lastPos;
            }

            OnPhaseChanged?.Invoke(_currentPhase);

            UnityEngine.Debug.Log($"[BossManager] Switched to Phase {_currentPhase}");
        }

        private void ActivateCurrentPhaseObject()
        {
            if (_phase1Object != null) _phase1Object.SetActive(_currentPhase == 1);
            if (_phase2Object != null) _phase2Object.SetActive(_currentPhase == 2);
            if (_phase3Object != null) _phase3Object.SetActive(_currentPhase == 3);
        }

        /// <summary>
        /// จัดการเมื่อ Player ตาย: GDD กำหนดว่าเริ่มใหม่ที่ Phase ล่าสุด และรีเซ็ตเลือดบอสกลับเต็ม MAX
        /// </summary>
        private void HandlePlayerDeath()
        {
            if (!_isFightActive) return;

            UnityEngine.Debug.Log($"[BossManager] Player died! Resetting Boss Phase {_currentPhase} to full HP...");

            // ค้นหา BossHealth ของ Phase ปัจจุบันและสั่งรีเซ็ต
            GameObject currentObj = GetCurrentPhaseObject();
            if (currentObj != null)
            {
                if (currentObj.TryGetComponent<BossHealth>(out var health))
                {
                    health.ResetHealth();
                }

                // รีเซ็ตตำแหน่งบอสกลับที่ Spawn Point ถ้ามี
                Transform spawnPt = GetCurrentPhaseSpawnPoint();
                if (spawnPt != null)
                {
                    currentObj.transform.position = spawnPt.position;
                }
            }
        }

        public GameObject GetCurrentPhaseObject()
        {
            switch (_currentPhase)
            {
                case 1: return _phase1Object;
                case 2: return _phase2Object;
                case 3: return _phase3Object;
                default: return null;
            }
        }

        private Transform GetCurrentPhaseSpawnPoint()
        {
            switch (_currentPhase)
            {
                case 1: return _phase1SpawnPoint;
                case 2: return _phase2SpawnPoint;
                case 3: return _phase3SpawnPoint;
                default: return null;
            }
        }
    }
}
