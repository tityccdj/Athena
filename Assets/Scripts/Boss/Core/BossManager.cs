using System;
using System.Collections.Generic;
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

        [Header("Phase 2 -> 3 Ghost Transition")]
        [SerializeField] private BossGhostTransition _ghostTransition;
        [SerializeField] private Transform _corridorWaypoint;

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

        private void Update()
        {
#if UNITY_EDITOR
            // ปุ่มลัดสำหรับทดสอบสลับ Phase ได้ทันทีใน Editor
            if (Input.GetKeyDown(KeyCode.F1))
            {
                SwitchToPhase(1);
            }
            else if (Input.GetKeyDown(KeyCode.F2))
            {
                SwitchToPhase(2);
            }
            else if (Input.GetKeyDown(KeyCode.F3))
            {
                SwitchToPhase(3);
            }
#endif
        }

        [ContextMenu("Debug: Switch to Phase 1")]
        public void DebugSwitchToPhase1() => SwitchToPhase(1);

        [ContextMenu("Debug: Switch to Phase 2")]
        public void DebugSwitchToPhase2() => SwitchToPhase(2);

        [ContextMenu("Debug: Switch to Phase 3")]
        public void DebugSwitchToPhase3() => SwitchToPhase(3);

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

            // GDD: เมื่อ Phase 2 ตาย ร่างจะกลายเป็นผีและบินไป Room 3, ประตูเปิดให้ Player วิ่งตาม
            if (_currentPhase == 2 && nextPhase == 3)
            {
                StartGhostFlightToRoom3();
                return;
            }

            SwitchToPhase(nextPhase);
        }

        /// <summary>
        /// จัดการ Transition จาก Phase 2 ไป 3 ตาม GDD:
        /// 1. เปิดประตูกั้นห้องเพื่อให้ Player เดินผ่านเข้า Corridor ไป Room 3
        /// 2. สั่ง Ghost บินลอยข้ามไปยัง Room 3
        /// 3. ปิด GameObject ของ Phase 2
        /// </summary>
        public void StartGhostFlightToRoom3()
        {
            UnityEngine.Debug.Log("[BossManager] Starting Ghost Flight from Room 2 to Room 3...");

            // 1. เปิดประตูกั้นห้องให้ Player วิ่งตามไป Room 3
            if (_bossRoomGate != null)
            {
                _bossRoomGate.OpenGate();
            }

            // 2. คำนวณตำแหน่งเริ่มต้นและปลายทางของ Ghost
            Vector3 startPos = _phase2Object != null ? _phase2Object.transform.position : transform.position;
            Transform designatedSpawn = GetCurrentPhaseSpawnPoint();
            Vector3 targetPos = _phase3SpawnPoint != null ? _phase3SpawnPoint.position : startPos + new Vector3(45f, 0f, 0f);

            // ปิด GameObject Phase 2
            if (_phase2Object != null)
            {
                _phase2Object.SetActive(false);
            }

            // 3. ปล่อย Ghost บินไป Room 3
            if (_ghostTransition != null)
            {
                List<Vector3> waypoints = new List<Vector3>();
                if (_corridorWaypoint != null)
                {
                    waypoints.Add(_corridorWaypoint.position);
                }
                _ghostTransition.StartFlight(startPos, targetPos, waypoints);
            }
            else
            {
                // ถ้าไม่มี GameObject Ghost ให้สลับตรงเข้า Phase 3 ทันทีเป็น Fallback
                SwitchToPhase(3);
            }
        }

        /// <summary>
        /// เรียกเมื่อ Player วิ่งมาถึง Room 3 และ Ghost อยู่ที่ Room 3 -> ปลุก Phase 3 Necromancer ขึ้นมาสู้
        /// </summary>
        public void StartPhase3FromGhost()
        {
            _currentPhase = 3;

            if (_phase3Object != null)
            {
                _phase3Object.SetActive(true);
                Transform spawnPt = _phase3SpawnPoint != null ? _phase3SpawnPoint : (_ghostTransition != null ? _ghostTransition.transform : null);
                if (spawnPt != null)
                {
                    _phase3Object.transform.position = spawnPt.position;
                }
            }

            OnPhaseChanged?.Invoke(3);
            UnityEngine.Debug.Log("[BossManager] Phase 3 Necromancer awakened in Room 3!");
        }

        public void SwitchToPhase(int phaseIndex)
        {
            GameObject prevObj = GetCurrentPhaseObject();
            Vector3 lastPos = prevObj != null ? prevObj.transform.position : Vector3.zero;

            _currentPhase = phaseIndex;

            // เมื่อเปลี่ยนจาก Phase 1 ไป 2 ให้คง Phase 1 ไว้ก่อนจนกว่า Phase 2 จะเล่น Intro เสร็จ
            if (phaseIndex == 2 && _phase1Object != null)
            {
                if (_phase2Object != null)
                {
                    _phase2Object.SetActive(true);
                    Transform designatedSpawn = GetCurrentPhaseSpawnPoint();
                    _phase2Object.transform.position = (designatedSpawn != null) ? designatedSpawn.position : lastPos;
                }
            }
            else
            {
                ActivateCurrentPhaseObject();
                GameObject newObj = GetCurrentPhaseObject();
                if (newObj != null && prevObj != null)
                {
                    Transform designatedSpawn = GetCurrentPhaseSpawnPoint();
                    newObj.transform.position = (designatedSpawn != null) ? designatedSpawn.position : lastPos;
                }
            }

            OnPhaseChanged?.Invoke(_currentPhase);

            UnityEngine.Debug.Log($"[BossManager] Switched to Phase {_currentPhase}");
        }

        /// <summary>
        /// เรียกเมื่อ Phase 2 เล่น Intro Animation เสร็จสมบูรณ์ -> ทำการปิด GameObject ของ Phase 1
        /// </summary>
        public void OnPhase2IntroComplete()
        {
            if (_phase1Object != null)
            {
                _phase1Object.SetActive(false);
                UnityEngine.Debug.Log("[BossManager] Phase 2 Intro completed -> Phase 1 object deactivated.");
            }
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

            // ตรวจสอบให้แน่ใจว่า Phase 1 ปิดอยู่เมื่อเกิดใหม่ใน Phase 2
            if (_currentPhase == 2 && _phase1Object != null)
            {
                _phase1Object.SetActive(false);
            }

            if (_currentPhase == 3)
            {
                if (_phase1Object != null) _phase1Object.SetActive(false);
                if (_phase2Object != null) _phase2Object.SetActive(false);
                if (_ghostTransition != null) _ghostTransition.gameObject.SetActive(false);
            }

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
