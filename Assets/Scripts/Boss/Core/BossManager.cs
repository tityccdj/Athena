using System;
using System.Collections.Generic;
using Athena.Audio;
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
        [SerializeField] private RoomGate _room3Gate;

        [Header("Phase 2 -> 3 Ghost Transition")]
        [SerializeField] private BossGhostTransition _ghostTransition;
        [SerializeField] private Transform _corridorWaypoint;

        [Header("Unity Events (สำหรับเชื่อมต่อ ParticleManager หรือเสียง)")]
        [SerializeField] private UnityEngine.Events.UnityEvent _onPhaseChangedEvent;
        [SerializeField] private UnityEngine.Events.UnityEvent _onAllBossDefeatedEvent;

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

        private void OnEnable()
        {
            RoomArea.OnRoomEntered += HandleRoomEntered;
        }

        private void OnDisable()
        {
            RoomArea.OnRoomEntered -= HandleRoomEntered;
        }

        private void HandleRoomEntered(RoomArea room)
        {
            if (room == null) return;

            // เมื่อ Player เดินเข้า Room2_boss ให้เริ่มการต่อสู้บอสทันที
            if (room.RoomName.Equals("Room2_boss", StringComparison.OrdinalIgnoreCase) && !_isFightActive && _currentPhase <= 2)
            {
                StartBossFight();
            }

            // เมื่อ Player เดินเข้า Room3_Boss ให้ปิดประตูห้อง 3 และเริ่มการต่อสู้ทันที
            if (room.RoomName.Equals("Room3_Boss", StringComparison.OrdinalIgnoreCase))
            {
                if (_room3Gate != null)
                {
                    _room3Gate.CloseGate();
                }

                if (_currentPhase == 3)
                {
                    _isFightActive = true;
                    ActivateCurrentPhaseObject();
                }
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

            // เล่นเพลงตาม Phase ปัจจุบัน
            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.PlayPhaseMusic(_currentPhase);
            }

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
                _onAllBossDefeatedEvent?.Invoke();

                if (MusicManager.Instance != null)
                {
                    MusicManager.Instance.PlayVictoryMusic();
                }

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

            // เล่นเพลงช่วงผีบินไปห้อง 3
            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.PlayGhostTransitionMusic();
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

            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.PlayPhaseMusic(3);
            }

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
            _onPhaseChangedEvent?.Invoke();

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
        /// จัดการเมื่อ Player ตาย: หยุดสถานะสู้ทันทีเพื่อให้บอสหยุด action ทุกอย่าง
        /// </summary>
        private void HandlePlayerDeath()
        {
            _isFightActive = false;
        }

        /// <summary>
        /// รีเซ็ตบอสใน Phase ปัจจุบันให้เลือดเต็ม MAX และเริ่มสู้ใหม่ (ใช้เมื่อ Player Respawn)
        /// ประตูจะเปิดออกเพื่อให้ผู้เล่นเดินเข้าห้องได้ และบอสจะรอจนกว่าผู้เล่นจะเข้าห้อง
        /// </summary>
        public void ResetCurrentPhaseBoss()
        {
            // ทำลายกระสุนหรือเอฟเฟกต์ตกค้างในฉาก
            var shockwaves = FindObjectsByType<Phase2.BossShockwaveProjectile>(FindObjectsSortMode.None);
            foreach (var sw in shockwaves) if (sw != null) Destroy(sw.gameObject);

            var telegraphs = FindObjectsByType<Phase3.ExplosionTelegraphEffect>(FindObjectsSortMode.None);
            foreach (var tg in telegraphs) if (tg != null) Destroy(tg.gameObject);

            var explosions = FindObjectsByType<Phase3.BossExplosionEffect>(FindObjectsSortMode.None);
            foreach (var exp in explosions) if (exp != null) Destroy(exp.gameObject);

            if (_currentPhase == 1)
            {
                if (_phase2Object != null) _phase2Object.SetActive(false);
                if (_phase3Object != null) _phase3Object.SetActive(false);

                if (_phase1Object != null)
                {
                    _phase1Object.SetActive(true);
                    Transform spawnPt = _phase1SpawnPoint;
                    Vector3 pos = spawnPt != null ? spawnPt.position : _phase1Object.transform.position;
                    if (_phase1Object.TryGetComponent<Phase1.OrcRiderBossController>(out var c1))
                    {
                        c1.ResetBoss(pos);
                    }
                    else if (_phase1Object.TryGetComponent<BossHealth>(out var h1))
                    {
                        h1.ResetHealth();
                        _phase1Object.transform.position = pos;
                    }
                }

                // เปิดประตู Room 2 เพื่อให้ผู้เล่นเดินเข้าห้องได้
                if (_bossRoomGate != null) _bossRoomGate.OpenGate();
            }
            else if (_currentPhase == 2)
            {
                if (_phase1Object != null) _phase1Object.SetActive(false);
                if (_phase3Object != null) _phase3Object.SetActive(false);

                if (_phase2Object != null)
                {
                    _phase2Object.SetActive(true);
                    Transform spawnPt = _phase2SpawnPoint;
                    Vector3 pos = spawnPt != null ? spawnPt.position : _phase2Object.transform.position;
                    if (_phase2Object.TryGetComponent<Phase2.EliteOrcBossController>(out var c2))
                    {
                        c2.ResetBoss(pos);
                    }
                    else if (_phase2Object.TryGetComponent<BossHealth>(out var h2))
                    {
                        h2.ResetHealth();
                        _phase2Object.transform.position = pos;
                    }
                }

                // เปิดประตู Room 2 เพื่อให้ผู้เล่นเดินเข้าห้องได้
                if (_bossRoomGate != null) _bossRoomGate.OpenGate();
            }
            else if (_currentPhase == 3)
            {
                if (_phase1Object != null) _phase1Object.SetActive(false);
                if (_phase2Object != null) _phase2Object.SetActive(false);
                if (_ghostTransition != null) _ghostTransition.gameObject.SetActive(false);

                if (_phase3Object != null)
                {
                    _phase3Object.SetActive(true);
                    Transform spawnPt = _phase3SpawnPoint;
                    Vector3 pos = spawnPt != null ? spawnPt.position : _phase3Object.transform.position;
                    if (_phase3Object.TryGetComponent<Phase3.NecromancerBossController>(out var c3))
                    {
                        c3.ResetBoss(pos);
                    }
                    else if (_phase3Object.TryGetComponent<BossHealth>(out var h3))
                    {
                        h3.ResetHealth();
                        _phase3Object.transform.position = pos;
                    }
                }

                // เปิดประตูทั้ง Room 2 และ Room 3 เมื่อ Respawn ใน Phase 3
                if (_bossRoomGate != null) _bossRoomGate.OpenGate();
                if (_room3Gate != null) _room3Gate.OpenGate();
            }

            // สลับกลับเป็นเพลงช่วงสำรวจจนกว่าจะเดินเข้าห้องบอสอีกครั้ง
            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.PlayExplorationMusic();
            }

            // รอจนกว่าผู้เล่นจะเดินเข้าห้องบอส
            _isFightActive = false;
            UnityEngine.Debug.Log($"[BossManager] Reset Phase {_currentPhase} Boss. Gates opened! Waiting for player to enter room...");
        }

        public GameObject Phase1Object => _phase1Object;
        public GameObject Phase2Object => _phase2Object;
        public GameObject Phase3Object => _phase3Object;

        public BossHealth GetPhaseHealth(int phase)
        {
            GameObject obj = null;
            switch (phase)
            {
                case 1: obj = _phase1Object; break;
                case 2: obj = _phase2Object; break;
                case 3: obj = _phase3Object; break;
            }
            return obj != null ? obj.GetComponent<BossHealth>() : null;
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
