using System;
using System.Collections;
using Athena.Boss.Core;
using Athena.CameraSystem;
using Athena.Player;
using Athena.Rooms;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Athena.UI
{
    /// <summary>
    /// ควบคุมระบบ Pause Game และระบบ Respawn:
    /// 1. ระบบ Respawn:
    ///    - เมื่อ Player ตาย จะเปิด Panel ให้กดปุ่ม Respawn
    ///    - เกิดตามจุดเกิดที่ผู้เล่นสร้างเอง:
    ///        * Phase 1 และ Phase 2 (Room 2) -> ใช้ _phase1And2RespawnPoint
    ///        * Phase 3 (Room 3) -> ใช้ _phase3RespawnPoint
    ///    - เลือดบอสใน Phase นั้นๆ จะรีเซ็ตกลับเต็ม MAX และเปิดประตูรอให้ผู้เล่นเดินเข้าห้อง
    /// 2. ระบบ Pause:
    ///    - กดปุ่ม Escape หรือ P เพื่อหยุดเกม (Pause)
    ///    - หยุดได้นานเท่าไรก็ได้ (Time.timeScale = 0)
    ///    - ขึ้น Panel เดียวกัน โดยมีปุ่ม Resume เพื่อเล่นต่อ และปุ่ม Respawn เพื่อเริ่มใหม่ได้ทันที
    /// </summary>
    public class PauseRespawnUI : MonoBehaviour
    {
        public static PauseRespawnUI Instance { get; private set; }

        [Header("Respawn Points (จุดเกิดสำหรับ Respawn ที่สร้างเอง)")]
        [Tooltip("จุดเกิดสำหรับ Phase 1 และ Phase 2 (Room 2) ลาก GameObject / Transform มาใส่ตรงนี้")]
        [SerializeField] private Transform _phase1And2RespawnPoint;

        [Tooltip("จุดเกิดสำหรับ Phase 3 (Room 3) ลาก GameObject / Transform มาใส่ตรงนี้")]
        [SerializeField] private Transform _phase3RespawnPoint;

        [Header("UI Panels & Windows")]
        [Tooltip("Panel หลักสำหรับแสดงเมนู Pause / Respawn (หากแนบสคริปต์นี้ไว้บนตัว Panel เอง จะใช้ CanvasGroup ซ่อน/แสดงให้อัตโนมัติ ป้องกันสคริปต์ดับ)")]
        [SerializeField] private GameObject _menuPanelRoot;

        [Tooltip("Panel แยกสำหรับหน้า Pause โดยเฉพาะ (ถ้าไม่ใส่จะใช้ _menuPanelRoot)")]
        [SerializeField] private GameObject _pausePanelOverride;

        [Tooltip("Panel แยกสำหรับหน้า GameOver/Death โดยเฉพาะ (ถ้าไม่ใส่จะใช้ _menuPanelRoot)")]
        [SerializeField] private GameObject _deathPanelOverride;

        [Header("UI Buttons")]
        [Tooltip("ปุ่มสำหรับกด Respawn")]
        [SerializeField] private Button _respawnButton;

        [Tooltip("ปุ่ม Respawn สำรอง (กรณีแยกปุ่มในหน้า Pause)")]
        [SerializeField] private Button _pauseRespawnButton;

        [Tooltip("ปุ่มสำหรับกดเล่นเกมต่อ (Resume)")]
        [SerializeField] private Button _resumeButton;

        [Header("UI Texts (แสดงสถานะ เช่น PAUSED หรือ GAME OVER)")]
        [Tooltip("TextMeshPro สำหรับแสดงหัวข้อ (ใส่หรือไม่ใส่ก็ได้)")]
        [SerializeField] private TextMeshProUGUI _titleTMP;

        [Tooltip("Legacy Text สำรองสำหรับแสดงหัวข้อ (ใส่หรือไม่ใส่ก็ได้)")]
        [SerializeField] private Text _titleLegacy;

        [SerializeField] private string _pauseTitle = "PAUSED";
        [SerializeField] private string _deathTitle = "GAME OVER";

        [Header("Settings")]
        [Tooltip("หน่วงเวลาก่อนเปิด Panel ตอน Player ตาย เพื่อให้เล่นท่าตายจนจบ (วินาที)")]
        [SerializeField] private float _deathPanelDelay = 0.35f;

        [Header("References (ถ้าไม่ใส่จะค้นหาในฉากให้อัตโนมัติ)")]
        [SerializeField] private PlayerController _player;
        [SerializeField] private CameraController2D _cameraController;
        [SerializeField] private BossHealthBarUI _bossHealthBarUI;

        private bool _isPaused = false;
        private bool _isPlayerDead = false;
        private bool _isSubscribedToPlayer = false;
        private CanvasGroup _canvasGroup;
        private Coroutine _deathRoutine;

        public bool IsPaused => _isPaused;
        public bool IsPlayerDead => _isPlayerDead;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // หากไม่ได้ลาก Panel มาใส่ใน Inspector ให้ใช้ GameObject ตัวเองเป็นค่าตั้งต้น
            if (_menuPanelRoot == null)
            {
                _menuPanelRoot = gameObject;
            }

            // ถ้าสคริปต์อยู่บน Panel เดียวกัน ให้เตรียม CanvasGroup ไว้จัดการการมองเห็นโดยไม่ต้องปิด GameObject
            if (_menuPanelRoot == gameObject || GetComponent<CanvasGroup>() != null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            FindReferencesIfNeeded();
            AutoDetectButtonsIfNeeded();

            // ซ่อน Panels ทั้งหมดเมื่อเริ่มเกม
            HideAllPanels();

            // ผูก Event ปุ่ม
            if (_respawnButton != null)
            {
                _respawnButton.onClick.RemoveListener(RespawnPlayer);
                _respawnButton.onClick.AddListener(RespawnPlayer);
            }
            if (_pauseRespawnButton != null)
            {
                _pauseRespawnButton.onClick.RemoveListener(RespawnPlayer);
                _pauseRespawnButton.onClick.AddListener(RespawnPlayer);
            }
            if (_resumeButton != null)
            {
                _resumeButton.onClick.RemoveListener(ResumeGame);
                _resumeButton.onClick.AddListener(ResumeGame);
            }
        }

        private void OnEnable()
        {
            TrySubscribePlayerDeath();
        }

        private void OnDisable()
        {
            UnsubscribePlayerDeath();
        }

        private void Start()
        {
            FindReferencesIfNeeded();
            AutoDetectButtonsIfNeeded();
            TrySubscribePlayerDeath();
        }

        private void AutoDetectButtonsIfNeeded()
        {
            // ค้นหาปุ่มอัตโนมัติหากยังไม่ได้ลากมาใส่ใน Inspector
            Button[] allButtons = GetComponentsInChildren<Button>(true);
            foreach (var btn in allButtons)
            {
                string btnName = btn.gameObject.name.ToLower();
                if (_respawnButton == null && (btnName.Contains("respawn") || btnName.Contains("retry") || btnName.Contains("restart")))
                {
                    _respawnButton = btn;
                    _respawnButton.onClick.RemoveListener(RespawnPlayer);
                    _respawnButton.onClick.AddListener(RespawnPlayer);
                }
                else if (_resumeButton == null && (btnName.Contains("resume") || btnName.Contains("continue")))
                {
                    _resumeButton = btn;
                    _resumeButton.onClick.RemoveListener(ResumeGame);
                    _resumeButton.onClick.AddListener(ResumeGame);
                }
            }
        }

        private void FindReferencesIfNeeded()
        {
            if (_player == null)
            {
                _player = FindFirstObjectByType<PlayerController>();
            }

            if (_cameraController == null)
            {
                _cameraController = FindFirstObjectByType<CameraController2D>();
            }

            if (_bossHealthBarUI == null)
            {
                _bossHealthBarUI = FindFirstObjectByType<BossHealthBarUI>();
            }
        }

        private void TrySubscribePlayerDeath()
        {
            FindReferencesIfNeeded();

            if (_player != null && _player.Health != null)
            {
                if (!_isSubscribedToPlayer)
                {
                    _player.Health.OnDeath -= HandlePlayerDeath;
                    _player.Health.OnDeath += HandlePlayerDeath;
                    _isSubscribedToPlayer = true;
                }
            }
        }

        private void UnsubscribePlayerDeath()
        {
            if (_player != null && _player.Health != null)
            {
                _player.Health.OnDeath -= HandlePlayerDeath;
            }
            _isSubscribedToPlayer = false;
        }

        private void Update()
        {
            // ตรวจสอบการผูก Player อยู่เสมอเผื่อ Player เกิดทีหลัง
            if (!_isSubscribedToPlayer)
            {
                TrySubscribePlayerDeath();
            }

            // ตรวจสอบการกดปุ่ม Pause (Escape หรือ P)
            if (CheckPauseInputDown())
            {
                // ถ้าตายอยู่ ห้ามกด Pause/Resume (ต้องกดปุ่ม Respawn เท่านั้น)
                if (_isPlayerDead) return;

                if (_isPaused)
                {
                    ResumeGame();
                }
                else
                {
                    PauseGame();
                }
            }
        }

        /// <summary>
        /// ตรวจสอบการกดปุ่ม Pause รองรับทั้ง New Input System และ Legacy Input
        /// </summary>
        private bool CheckPauseInputDown()
        {
            bool triggered = false;

            // ตรวจสอบผ่าน Keyboard ใน New Input System
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame)
                {
                    triggered = true;
                }
            }

            // Fallback ตรวจสอบผ่าน Legacy Input
            if (!triggered)
            {
                try
                {
                    if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) || UnityEngine.Input.GetKeyDown(KeyCode.P))
                    {
                        triggered = true;
                    }
                }
                catch
                {
                    // กรณีโปรเจกต์ตั้งค่า Input System Package Only
                }
            }

            return triggered;
        }

        // ─── Pause System ───────────────────────────────

        /// <summary>
        /// หยุดเกมชั่วคราว (Pause) โดยหยุดเวลา Time.timeScale = 0 และแสดง Panel
        /// </summary>
        public void PauseGame()
        {
            if (_isPlayerDead) return;

            _isPaused = true;
            Time.timeScale = 0f;

            ShowPanel(isDeath: false);

            UnityEngine.Debug.Log("[PauseRespawnUI] Game Paused (TimeScale = 0)");
        }

        /// <summary>
        /// เล่นเกมต่อ (Resume) คืนค่า Time.timeScale = 1 และซ่อน Panel
        /// </summary>
        public void ResumeGame()
        {
            if (_isPlayerDead) return;

            _isPaused = false;
            Time.timeScale = 1f;

            HideAllPanels();

            UnityEngine.Debug.Log("[PauseRespawnUI] Game Resumed (TimeScale = 1)");
        }

        // ─── Death & Respawn System ─────────────────────

        private void HandlePlayerDeath()
        {
            UnityEngine.Debug.Log("[PauseRespawnUI] Player death detected! Preparing respawn panel...");
            _isPlayerDead = true;

            if (_deathRoutine != null) StopCoroutine(_deathRoutine);
            _deathRoutine = StartCoroutine(ShowDeathPanelRoutine());
        }

        private IEnumerator ShowDeathPanelRoutine()
        {
            // รอให้อนิเมชันตายเล่น
            yield return new WaitForSecondsRealtime(_deathPanelDelay);

            ShowPanel(isDeath: true);

            // หยุดเวลาไว้ขณะหน้าต่างความตายเปิดอยู่
            Time.timeScale = 0f;

            UnityEngine.Debug.Log("[PauseRespawnUI] Player Died -> Showing Respawn Panel");
        }

        /// <summary>
        /// ชุบชีวิต Player และเริ่มสู้ใน Phase ปัจจุบันใหม่:
        /// - ย้ายตำแหน่งไปยังจุดเกิดที่กำหนด (Phase 1-2 ใช้จุด 1, Phase 3 ใช้จุด 2)
        /// - รีเซ็ตเลือด Player กลับเต็ม MAX (5 HP) และรีเซ็ตคูลดาวน์ยา
        /// - รีเซ็ตเลือดบอสใน Phase นั้นๆ กลับเต็ม MAX และเปิดประตูรอ
        /// - คืนค่า Time.timeScale = 1
        /// </summary>
        public void RespawnPlayer()
        {
            FindReferencesIfNeeded();

            _isPlayerDead = false;
            _isPaused = false;
            Time.timeScale = 1f;

            if (_deathRoutine != null)
            {
                StopCoroutine(_deathRoutine);
                _deathRoutine = null;
            }

            HideAllPanels();

            int currentPhase = (BossManager.Instance != null) ? BossManager.Instance.CurrentPhase : 1;

            // 1. คำนวณหาตำแหน่ง Respawn ที่ถูกต้อง
            Vector3 targetSpawnPos = DetermineRespawnPosition(currentPhase);

            // 2. ชุบชีวิตและเคลื่อนย้าย Player
            if (_player != null)
            {
                _player.Respawn(targetSpawnPos);
            }

            // 3. ปรับขอบเขตห้องและกล้องให้ตรงกับจุดเกิดทันที
            ApplyRoomCameraMode(targetSpawnPos);

            if (_cameraController != null)
            {
                _cameraController.SnapToTarget();
            }

            // 4. รีเซ็ตบอสใน Phase นั้นๆ กลับเต็ม MAX และเปิดประตูห้อง
            if (BossManager.Instance != null)
            {
                BossManager.Instance.ResetCurrentPhaseBoss();
            }

            // 5. รีเซ็ตหลอดเลือด UI ของบอสให้เต็มทันที
            if (_bossHealthBarUI != null)
            {
                _bossHealthBarUI.ResetUIForPhase(currentPhase);
            }

            UnityEngine.Debug.Log($"[PauseRespawnUI] Respawn completed at Phase {currentPhase} (Pos: {targetSpawnPos})");
        }

        private Vector3 DetermineRespawnPosition(int phase)
        {
            if (phase == 3)
            {
                if (_phase3RespawnPoint != null)
                {
                    return _phase3RespawnPoint.position;
                }
            }
            else // Phase 1 or 2
            {
                if (_phase1And2RespawnPoint != null)
                {
                    return _phase1And2RespawnPoint.position;
                }
            }

            // Fallback กรณีที่ยังไม่ได้ลาก Transform มาใส่
            if (_player != null)
            {
                return _player.transform.position;
            }

            return Vector3.zero;
        }

        private void ApplyRoomCameraMode(Vector3 position)
        {
            var rooms = FindObjectsByType<RoomArea>(FindObjectsSortMode.None);
            foreach (var room in rooms)
            {
                if (room != null && room.RoomBounds.Contains(position))
                {
                    room.ApplyCameraMode();
                    break;
                }
            }
        }

        // ─── Visibility Management ──────────────────────

        private void ShowPanel(bool isDeath)
        {
            AutoDetectButtonsIfNeeded();

            GameObject specificPanel = isDeath ? _deathPanelOverride : _pausePanelOverride;
            GameObject targetPanel = specificPanel != null ? specificPanel : _menuPanelRoot;

            if (targetPanel != null)
            {
                if (targetPanel == gameObject)
                {
                    // ถ้าแนบสคริปต์ไว้บนตัว Panel ให้ใช้ CanvasGroup แสดงขึ้นมา
                    if (_canvasGroup != null)
                    {
                        _canvasGroup.alpha = 1f;
                        _canvasGroup.interactable = true;
                        _canvasGroup.blocksRaycasts = true;
                    }
                    else
                    {
                        // เปิดลูกทั้งหมดของตัว Panel
                        foreach (Transform child in transform)
                        {
                            child.gameObject.SetActive(true);
                        }
                    }
                }
                else
                {
                    targetPanel.SetActive(true);
                }
            }

            // จัดการปุ่ม
            if (isDeath)
            {
                if (_resumeButton != null) _resumeButton.gameObject.SetActive(false);
                if (_respawnButton != null) _respawnButton.gameObject.SetActive(true);
                if (_pauseRespawnButton != null) _pauseRespawnButton.gameObject.SetActive(true);
                SetTitleText(_deathTitle);
            }
            else
            {
                if (_resumeButton != null) _resumeButton.gameObject.SetActive(true);
                if (_respawnButton != null) _respawnButton.gameObject.SetActive(true);
                if (_pauseRespawnButton != null) _pauseRespawnButton.gameObject.SetActive(true);
                SetTitleText(_pauseTitle);
            }
        }

        private void HideAllPanels()
        {
            if (_pausePanelOverride != null) _pausePanelOverride.SetActive(false);
            if (_deathPanelOverride != null) _deathPanelOverride.SetActive(false);

            if (_menuPanelRoot != null)
            {
                if (_menuPanelRoot == gameObject)
                {
                    // ถ้าแนบสคริปต์ไว้บนตัว Panel ห้ามเรียก SetActive(false) บนตัวมันเอง
                    if (_canvasGroup != null)
                    {
                        _canvasGroup.alpha = 0f;
                        _canvasGroup.interactable = false;
                        _canvasGroup.blocksRaycasts = false;
                    }
                    else
                    {
                        // ปิดลูกทั้งหมดของตัว Panel เพื่อซ่อนกราฟิก
                        foreach (Transform child in transform)
                        {
                            child.gameObject.SetActive(false);
                        }
                    }
                }
                else
                {
                    _menuPanelRoot.SetActive(false);
                }
            }
        }

        private void SetTitleText(string title)
        {
            if (_titleTMP != null) _titleTMP.text = title;
            if (_titleLegacy != null) _titleLegacy.text = title;
        }
    }
}
