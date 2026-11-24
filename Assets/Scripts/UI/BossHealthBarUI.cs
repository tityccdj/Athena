using System;
using Athena.Boss.Core;
using Athena.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Athena.UI
{
    /// <summary>
    /// ควบคุมการแสดงผลหลอดเลือดของบอส (Boss Health Bar) ทั้ง 3 Phase:
    /// 1. Room 2: หลอดเลือดของ Phase 1 และ Phase 2 รวม Max HP กันเป็นหลอดเดียว
    ///    - Phase 1: เลือดจะลดจาก 100% ลงมาที่ 50%
    ///    - Phase 2: เลือดจะลดต่อจาก 50% ลงไปจนถึง 0%
    /// 2. Room 3: ใช้หลอดแยกสำหรับ Phase 3 (Necromancer)
    /// 3. หลอดเลือดจะเริ่มแสดงเมื่อ Player เดินเข้าห้องบอสแต่ละห้อง (RoomArea)
    /// </summary>
    public class BossHealthBarUI : MonoBehaviour
    {
        [Header("Room 2 Boss Bar (Phase 1 & Phase 2 รวม Max HP กัน)")]
        [Tooltip("Root GameObject ของหลอดเลือด Room 2 (จะเปิดเมื่อเข้า Room2_boss)")]
        [SerializeField] private GameObject _room2BarRoot;
        [Tooltip("Slider หลอดเลือด Room 2 (ใส่ Slider หรือ Fill Image อย่างใดอย่างหนึ่ง)")]
        [SerializeField] private Slider _room2Slider;
        [Tooltip("Image หลอดเลือดแบบ Filled ของ Room 2 (กรณีไม่ใช้ Slider)")]
        [SerializeField] private Image _room2FillImage;
        [Tooltip("Text แสดงชื่อบอส Room 2 เช่น 'Orc & Rider' (ใส่หรือไม่ใส่ก็ได้)")]
        [SerializeField] private TextMeshProUGUI _room2BossNameTMP;
        [SerializeField] private Text _room2BossNameLegacy;
        [SerializeField] private string _phase1BossName = "Orc Rider";
        [SerializeField] private string _phase2BossName = "Elite Orc";

        [Header("Room 3 Boss Bar (Phase 3 ใช้หลอดแยก)")]
        [Tooltip("Root GameObject ของหลอดเลือด Room 3 (จะเปิดเมื่อเข้า Room3_Boss)")]
        [SerializeField] private GameObject _room3BarRoot;
        [Tooltip("Slider หลอดเลือด Room 3 (ใส่ Slider หรือ Fill Image อย่างใดอย่างหนึ่ง)")]
        [SerializeField] private Slider _room3Slider;
        [Tooltip("Image หลอดเลือดแบบ Filled ของ Room 3 (กรณีไม่ใช้ Slider)")]
        [SerializeField] private Image _room3FillImage;
        [Tooltip("Text แสดงชื่อบอส Room 3 เช่น 'Necromancer' (ใส่หรือไม่ใส่ก็ได้)")]
        [SerializeField] private TextMeshProUGUI _room3BossNameTMP;
        [SerializeField] private Text _room3BossNameLegacy;
        [SerializeField] private string _phase3BossName = "Necromancer";

        [Header("Room Names")]
        [SerializeField] private string _room2Name = "Room2_boss";
        [SerializeField] private string _room3Name = "Room3_Boss";

        [Header("Smooth Lerp Animation")]
        [SerializeField] private bool _useSmoothLerp = true;
        [SerializeField] private float _lerpSpeed = 6f;

        private BossHealth _phase1Health;
        private BossHealth _phase2Health;
        private BossHealth _phase3Health;

        private int _currentPhase = 1;

        private float _room2TargetRatio = 1f;
        private float _room2CurrentRatio = 1f;

        private float _room3TargetRatio = 1f;
        private float _room3CurrentRatio = 1f;

        private bool _isPlayerInRoom2 = false;
        private bool _isPlayerInRoom3 = false;
        private bool _isPhase2Defeated = false;
        private bool _isPhase3Defeated = false;

        private void Awake()
        {
            // เริ่มต้นซ่อนหลอดเลือดไว้ก่อน จนกว่าจะเข้าห้องบอส
            if (_room2BarRoot != null) _room2BarRoot.SetActive(false);
            if (_room3BarRoot != null) _room3BarRoot.SetActive(false);
        }

        private void OnEnable()
        {
            RoomArea.OnRoomEntered += HandleRoomEntered;
            RoomArea.OnRoomExited += HandleRoomExited;

            if (BossManager.Instance != null)
            {
                BossManager.Instance.OnPhaseChanged += HandlePhaseChanged;
                BossManager.Instance.OnBossDefeatedAll += HandleAllDefeated;
            }
        }

        private void OnDisable()
        {
            RoomArea.OnRoomEntered -= HandleRoomEntered;
            RoomArea.OnRoomExited -= HandleRoomExited;

            if (BossManager.Instance != null)
            {
                BossManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
                BossManager.Instance.OnBossDefeatedAll -= HandleAllDefeated;
            }

            UnhookAllBossHealths();
        }

        private void Start()
        {
            EnsureSubscribedToBossManager();
            HookInitialBossHealths();
        }

        private void EnsureSubscribedToBossManager()
        {
            if (BossManager.Instance != null)
            {
                BossManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
                BossManager.Instance.OnPhaseChanged += HandlePhaseChanged;
                BossManager.Instance.OnBossDefeatedAll -= HandleAllDefeated;
                BossManager.Instance.OnBossDefeatedAll += HandleAllDefeated;
            }
        }

        private void Update()
        {
            // ตรวจสอบการเชื่อมต่อกับ BossManager หาก Start ทำงานก่อน
            EnsureSubscribedToBossManager();

            // คำนวณ ratio เลือดสดๆ ทุกเฟรมสำหรับห้องที่กำลังแสดงผล
            if (_room2BarRoot != null && _room2BarRoot.activeInHierarchy)
            {
                UpdateRoom2CombinedHealthRatio();
            }

            if (_room3BarRoot != null && _room3BarRoot.activeInHierarchy)
            {
                UpdateRoom3HealthRatio();
            }

            // Smooth Lerp อัปเดตการลดลงของหลอดเลือดอย่างนุ่มนวล
            if (_useSmoothLerp)
            {
                if (_room2BarRoot != null && _room2BarRoot.activeInHierarchy)
                {
                    _room2CurrentRatio = Mathf.MoveTowards(_room2CurrentRatio, _room2TargetRatio, _lerpSpeed * Time.deltaTime);
                    ApplyBarRatio(_room2Slider, _room2FillImage, _room2CurrentRatio);
                }

                if (_room3BarRoot != null && _room3BarRoot.activeInHierarchy)
                {
                    _room3CurrentRatio = Mathf.MoveTowards(_room3CurrentRatio, _room3TargetRatio, _lerpSpeed * Time.deltaTime);
                    ApplyBarRatio(_room3Slider, _room3FillImage, _room3CurrentRatio);
                }
            }
            else
            {
                if (_room2BarRoot != null && _room2BarRoot.activeInHierarchy)
                {
                    _room2CurrentRatio = _room2TargetRatio;
                    ApplyBarRatio(_room2Slider, _room2FillImage, _room2CurrentRatio);
                }

                if (_room3BarRoot != null && _room3BarRoot.activeInHierarchy)
                {
                    _room3CurrentRatio = _room3TargetRatio;
                    ApplyBarRatio(_room3Slider, _room3FillImage, _room3CurrentRatio);
                }
            }
        }

        private void HookInitialBossHealths()
        {
            if (BossManager.Instance == null) return;

            _currentPhase = BossManager.Instance.CurrentPhase;

            // เชื่อมต่อ Phase 1 และ Phase 2 สำหรับหลอดรวม Room 2
            _phase1Health = BossManager.Instance.GetPhaseHealth(1);
            if (_phase1Health != null)
            {
                _phase1Health.OnHealthChanged += HandlePhase1HealthChanged;
            }

            _phase2Health = BossManager.Instance.GetPhaseHealth(2);
            if (_phase2Health != null)
            {
                _phase2Health.OnHealthChanged += HandlePhase2HealthChanged;
            }

            SetRoom2BossName(_currentPhase == 1 ? _phase1BossName : _phase2BossName);
            UpdateRoom2CombinedHealthRatio();
            _room2CurrentRatio = _room2TargetRatio;
            ApplyBarRatio(_room2Slider, _room2FillImage, _room2CurrentRatio);

            // เชื่อมต่อ Phase 3 สำหรับหลอด Room 3
            _phase3Health = BossManager.Instance.GetPhaseHealth(3);
            if (_phase3Health != null)
            {
                _phase3Health.OnHealthChanged += HandleRoom3HealthChanged;
                SetRoom3BossName(_phase3BossName);
                _room3TargetRatio = (_phase3Health.MaxHealth > 0) ? (float)_phase3Health.CurrentHealth / _phase3Health.MaxHealth : 1f;
                _room3CurrentRatio = _room3TargetRatio;
                ApplyBarRatio(_room3Slider, _room3FillImage, _room3CurrentRatio);
            }
        }

        private void UnhookAllBossHealths()
        {
            if (_phase1Health != null)
            {
                _phase1Health.OnHealthChanged -= HandlePhase1HealthChanged;
                _phase1Health = null;
            }

            if (_phase2Health != null)
            {
                _phase2Health.OnHealthChanged -= HandlePhase2HealthChanged;
                _phase2Health = null;
            }

            if (_phase3Health != null)
            {
                _phase3Health.OnHealthChanged -= HandleRoom3HealthChanged;
                _phase3Health = null;
            }
        }

        // ─── Room Area Events ───────────────────────────

        private void HandleRoomEntered(RoomArea room)
        {
            if (room == null) return;

            if (room.RoomName.Equals(_room2Name, StringComparison.OrdinalIgnoreCase))
            {
                _isPlayerInRoom2 = true;
                _isPlayerInRoom3 = false;
                UpdateBarsVisibility();
            }
            else if (room.RoomName.Equals(_room3Name, StringComparison.OrdinalIgnoreCase))
            {
                _isPlayerInRoom2 = false;
                _isPlayerInRoom3 = true;
                UpdateBarsVisibility();
            }
            else
            {
                // เข้า Room 1 หรือทางเดินระหว่างห้อง
                _isPlayerInRoom2 = false;
                _isPlayerInRoom3 = false;
                UpdateBarsVisibility();
            }
        }

        private void HandleRoomExited(RoomArea room)
        {
            if (room == null) return;

            if (room.RoomName.Equals(_room2Name, StringComparison.OrdinalIgnoreCase))
            {
                _isPlayerInRoom2 = false;
                UpdateBarsVisibility();
            }
            else if (room.RoomName.Equals(_room3Name, StringComparison.OrdinalIgnoreCase))
            {
                _isPlayerInRoom3 = false;
                UpdateBarsVisibility();
            }
        }

        private void UpdateBarsVisibility()
        {
            // หลอด Room 2: แสดงเมื่ออยู่ใน Room 2 และ Phase 2 ยังไม่ตาย
            bool showRoom2 = _isPlayerInRoom2 && !_isPhase2Defeated;
            if (_room2BarRoot != null)
            {
                _room2BarRoot.SetActive(showRoom2);
            }

            // หลอด Room 3: แสดงเมื่ออยู่ใน Room 3 และ Phase 3 ยังไม่ตาย
            bool showRoom3 = _isPlayerInRoom3 && !_isPhase3Defeated;
            if (_room3BarRoot != null)
            {
                _room3BarRoot.SetActive(showRoom3);
            }
        }

        // ─── Phase Change Handling ──────────────────────

        private void HandlePhaseChanged(int newPhase)
        {
            _currentPhase = newPhase;

            if (newPhase == 2)
            {
                // เข้าสู่ Phase 2: ตรวจสอบและเชื่อมต่อ Phase 2 Health อีกครั้งเพื่อความชัวร์
                if (BossManager.Instance != null)
                {
                    BossHealth p2 = BossManager.Instance.GetPhaseHealth(2);
                    if (p2 != null)
                    {
                        if (_phase2Health != null) _phase2Health.OnHealthChanged -= HandlePhase2HealthChanged;
                        _phase2Health = p2;
                        _phase2Health.OnHealthChanged += HandlePhase2HealthChanged;
                    }
                }

                // เปลี่ยนชื่อบอสเป็น Elite Orc
                SetRoom2BossName(_phase2BossName);
                UpdateRoom2CombinedHealthRatio();
            }
            else if (newPhase == 3)
            {
                // Phase 2 ตายแล้ว ผีบินไป Room 3 -> ปิดหลอด Room 2 ทันที
                _isPhase2Defeated = true;
                if (_room2BarRoot != null) _room2BarRoot.SetActive(false);

                // ตรวจสอบและเชื่อมต่อ Phase 3 Health
                if (BossManager.Instance != null)
                {
                    BossHealth p3 = BossManager.Instance.GetPhaseHealth(3);
                    if (p3 != null)
                    {
                        if (_phase3Health != null) _phase3Health.OnHealthChanged -= HandleRoom3HealthChanged;
                        _phase3Health = p3;
                        _phase3Health.OnHealthChanged += HandleRoom3HealthChanged;
                    }
                }

                SetRoom3BossName(_phase3BossName);
                UpdateRoom3HealthRatio();
                _room3CurrentRatio = 1f;
                ApplyBarRatio(_room3Slider, _room3FillImage, _room3CurrentRatio);

                UpdateBarsVisibility();
            }
        }

        private void HandleAllDefeated()
        {
            _isPhase3Defeated = true;
            if (_room3BarRoot != null) _room3BarRoot.SetActive(false);
        }

        // ─── Room 2 Combined Health Calculation ─────────

        private void HandlePhase1HealthChanged(int current, int max)
        {
            UpdateRoom2CombinedHealthRatio();
        }

        private void HandlePhase2HealthChanged(int current, int max)
        {
            UpdateRoom2CombinedHealthRatio();
        }

        /// <summary>
        /// คำนวณอัตราส่วนเลือดของ Room 2 โดยรวม Max HP ของ Phase 1 และ Phase 2 เข้าด้วยกัน
        /// - Phase 1: เลือดจะลดจาก 100% -> 50%
        /// - Phase 2: เลือดจะลดต่อจาก 50% -> 0%
        /// </summary>
        private void UpdateRoom2CombinedHealthRatio()
        {
            if (BossManager.Instance != null)
            {
                if (_phase1Health == null) _phase1Health = BossManager.Instance.GetPhaseHealth(1);
                if (_phase2Health == null) _phase2Health = BossManager.Instance.GetPhaseHealth(2);
            }

            int phase1Max = (_phase1Health != null) ? _phase1Health.MaxHealth : 0;
            int phase2Max = (_phase2Health != null) ? _phase2Health.MaxHealth : 0;
            int totalMax = phase1Max + phase2Max;

            if (totalMax <= 0) return;

            int currentPhase = (BossManager.Instance != null) ? BossManager.Instance.CurrentPhase : _currentPhase;

            // ตัดสินใจว่า Phase 1 ตายหรือยัง:
            // ถ้า currentPhase >= 2 หรือเลือด Phase 1 เหลือ 0 หรือ GameObject Phase 1 ปิดอยู่ -> ให้คิดเฉพาะเลือด Phase 2
            bool isPhase1Dead = currentPhase >= 2 || (_phase1Health != null && _phase1Health.CurrentHealth <= 0);

            int p1Current = 0;
            int p2Current = 0;

            if (isPhase1Dead)
            {
                p1Current = 0;
                p2Current = (_phase2Health != null) ? Mathf.Max(0, _phase2Health.CurrentHealth) : 0;
            }
            else
            {
                p1Current = (_phase1Health != null) ? Mathf.Max(0, _phase1Health.CurrentHealth) : 0;
                p2Current = phase2Max; // ยังไม่เข้า Phase 2 เลือด Phase 2 ยังเต็ม 100%
            }

            int currentTotal = p1Current + p2Current;
            _room2TargetRatio = Mathf.Clamp01((float)currentTotal / totalMax);
        }

        // ─── Room 3 Health Calculation ──────────────────

        private void HandleRoom3HealthChanged(int current, int max)
        {
            UpdateRoom3HealthRatio();
        }

        private void UpdateRoom3HealthRatio()
        {
            if (BossManager.Instance != null && _phase3Health == null)
            {
                _phase3Health = BossManager.Instance.GetPhaseHealth(3);
            }

            if (_phase3Health != null && _phase3Health.MaxHealth > 0)
            {
                _room3TargetRatio = Mathf.Clamp01((float)_phase3Health.CurrentHealth / _phase3Health.MaxHealth);
            }
        }

        private void ApplyBarRatio(Slider slider, Image fillImage, float ratio)
        {
            if (slider != null)
            {
                slider.value = ratio;
            }

            if (fillImage != null)
            {
                fillImage.fillAmount = ratio;
            }
        }

        private void SetRoom2BossName(string name)
        {
            if (_room2BossNameTMP != null) _room2BossNameTMP.text = name;
            if (_room2BossNameLegacy != null) _room2BossNameLegacy.text = name;
        }

        private void SetRoom3BossName(string name)
        {
            if (_room3BossNameTMP != null) _room3BossNameTMP.text = name;
            if (_room3BossNameLegacy != null) _room3BossNameLegacy.text = name;
        }

        /// <summary>
        /// รีเซ็ตการแสดงผลหลอดเลือดบอสตาม Phase ที่กำหนด (ใช้เมื่อ Player Respawn)
        /// เพื่อให้หลอดเลือดแสดงค่าเต็ม MAX ทันที
        /// </summary>
        public void ResetUIForPhase(int phase)
        {
            _currentPhase = phase;

            if (phase <= 2)
            {
                _isPhase2Defeated = false;
                _isPlayerInRoom2 = true;
                _isPlayerInRoom3 = false;

                if (_room2BarRoot != null) _room2BarRoot.SetActive(true);
                if (_room3BarRoot != null) _room3BarRoot.SetActive(false);

                SetRoom2BossName(phase == 1 ? _phase1BossName : _phase2BossName);
                UpdateRoom2CombinedHealthRatio();
                _room2CurrentRatio = _room2TargetRatio;
                ApplyBarRatio(_room2Slider, _room2FillImage, _room2CurrentRatio);
            }
            else
            {
                _isPhase3Defeated = false;
                _isPlayerInRoom2 = false;
                _isPlayerInRoom3 = true;

                if (_room2BarRoot != null) _room2BarRoot.SetActive(false);
                if (_room3BarRoot != null) _room3BarRoot.SetActive(true);

                SetRoom3BossName(_phase3BossName);
                UpdateRoom3HealthRatio();
                _room3CurrentRatio = _room3TargetRatio;
                ApplyBarRatio(_room3Slider, _room3FillImage, _room3CurrentRatio);
            }
        }
    }
}
