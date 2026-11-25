using System;
using System.Collections;
using Athena.CameraSystem;
using Athena.Player.Health;
using Athena.Player.Input;
using Athena.Player.Movement;
using UnityEngine;

namespace Athena.Player.Combat
{
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Melee Combo Settings (GDD: 2 Hits, Hit 2 stronger)")]
        [SerializeField] private int _hit1Damage = 1;
        [SerializeField] private int _hit2Damage = 2;
        [SerializeField] private float _attackCooldown = 0.25f;
        [SerializeField] private float _comboEndCooldown = 0.45f;
        [SerializeField] private float _comboResetTime = 0.8f;
        [SerializeField] private float _hit1Knockback = 4f;
        [SerializeField] private float _hit2Knockback = 7f;

        [Header("Melee Movement Lock Durations")]
        [SerializeField] private float _attack1LockDuration = 0.35f;
        [SerializeField] private float _attack2LockDuration = 0.4f;

        [Header("Hit Stop / Freeze Frame Settings")]
        [Tooltip("ระยะเวลา Freeze Frame เมื่อฟัน Hit ที่สองโดนศัตรู (วินาที เช่น 0.08 - 0.12) 0 = ปิด")]
        [SerializeField] private float _hit2FreezeDuration = 0.08f;

        [Header("Unity Events (สำหรับเชื่อมต่อ ParticleManager หรือเสียง)")]
        [SerializeField] private UnityEngine.Events.UnityEvent _onAttack1Event;
        [SerializeField] private UnityEngine.Events.UnityEvent _onAttack2Event;
        [SerializeField] private UnityEngine.Events.UnityEvent _onDashAttackEvent;

        [Header("Dash Attack Settings (GDD: Projectile, 2 Damage)")]
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private Transform _projectileSpawnPoint;
        [Tooltip("ระยะเวลาคูลดาวน์ของการยิง Projectile (วินาที)")]
        [SerializeField] private float _projectileCooldown = 3.0f;

        [Header("Cinematic Zoom & Slow-Motion Settings")]
        [Tooltip("เปิดใช้งานระบบซูมกล้องและสโลว์โมชั่นเวลาเล็ง/ชาร์จยิง Projectile หรือไม่")]
        [SerializeField] private bool _enableCinematicZoom = true;
        [Tooltip("ขนาด Orthographic Size ของกล้องตอนซูมเข้า (ยิ่งค่าน้อย ยิ่งซูมใกล้ ค่าปกติประมาณ 3.0 - 3.5)")]
        [SerializeField] private float _zoomInSize = 3.2f;
        [Tooltip("ความเร็วในการซูมเข้า (วินาที, เวลาจริง)")]
        [SerializeField] private float _zoomInSpeed = 0.15f;
        [Tooltip("ความเร็วในการซูมออกกลับเป็นปกติ (วินาที, เวลาจริง)")]
        [SerializeField] private float _zoomOutSpeed = 0.25f;
        [Range(0.05f, 1f)]
        [Tooltip("สปีดเวลาช่วง Slow-Motion (เช่น 0.2 = เวลาช้าลง 5 เท่า, 1.0 = ปกติ)")]
        [SerializeField] private float _slowMotionTimeScale = 0.2f;
        [Tooltip("ระยะเวลาความปลอดภัยสูงสุดสำหรับ Cinematic Zoom (วินาที Realtime) หากไม่มี Event สั่งปิด หรือออกท่าไม่สำเร็จ จะคืนค่ากล้องและเวลาอัตโนมัติ")]
        [SerializeField] private float _cinematicSafetyTimeout = 0.6f;

        [Header("Components")]
        [SerializeField] private PlayerAttackHitbox _hitbox;

        private IPlayerInput _input;
        private PlayerMovement _movement;
        private PlayerDash _dash;
        private PlayerHealth _health;

        private int _currentComboStep = 0; // 0 = ready for Hit 1, 1 = ready for Hit 2
        private float _lastAttackTime = -999f;
        private float _lastProjectileAttackTime = -999f;
        private bool _hasFiredDashAttackInCurrentDash = false;
        private bool _isCinematicActive = false;
        private Coroutine _attackLockCoroutine;
        private Coroutine _cinematicTimeoutCoroutine;

        // Track last executed combo step separately for Animation Event lookup
        // (since _currentComboStep resets to 0 after Hit 2)
        private int _lastExecutedComboStep = 0;

        public event Action<int> OnMeleeAttackStarted; // comboStep: 1 or 2
        public event Action OnDashAttackTriggered;

        public bool CanFireProjectile => Time.time >= _lastProjectileAttackTime + _projectileCooldown;
        public float ProjectileCooldownRemaining => Mathf.Max(0f, (_lastProjectileAttackTime + _projectileCooldown) - Time.time);
        public float ProjectileCooldownDuration => _projectileCooldown;
        public float ProjectileCooldownNormalized => _projectileCooldown > 0f ? Mathf.Clamp01(ProjectileCooldownRemaining / _projectileCooldown) : 0f;

        public void Initialize(IPlayerInput input, PlayerMovement movement, PlayerDash dash, PlayerHealth health = null)
        {
            _input = input;
            _movement = movement;
            _dash = dash;
            _health = health;

            if (_dash != null)
            {
                _dash.OnDashStarted += HandleDashStarted;
                _dash.OnDashEnded += HandleDashEnded;
            }

            if (_health != null)
            {
                _health.OnDamaged += HandlePlayerDamaged;
                _health.OnDeath += HandlePlayerDeath;
            }
        }

        private void Awake()
        {
            if (_hitbox == null) _hitbox = GetComponentInChildren<PlayerAttackHitbox>();
            if (_projectileSpawnPoint == null) _projectileSpawnPoint = transform;
        }

        public void ResetCombat()
        {
            if (_isCinematicActive)
            {
                AnimEvent_EndCinematicZoom();
            }
            if (_attackLockCoroutine != null)
            {
                StopCoroutine(_attackLockCoroutine);
                _attackLockCoroutine = null;
            }
            _currentComboStep = 0;
            _lastAttackTime = -999f;
            _lastProjectileAttackTime = -999f;
            _hasFiredDashAttackInCurrentDash = false;
            if (_hitbox != null)
            {
                _hitbox.Close();
            }
        }

        private void OnDisable()
        {
            if (_isCinematicActive)
            {
                AnimEvent_EndCinematicZoom();
            }
        }

        private void OnDestroy()
        {
            if (_dash != null)
            {
                _dash.OnDashStarted -= HandleDashStarted;
                _dash.OnDashEnded -= HandleDashEnded;
            }

            if (_health != null)
            {
                _health.OnDamaged -= HandlePlayerDamaged;
                _health.OnDeath -= HandlePlayerDeath;
            }
        }

        private void Update()
        {
            if (_input == null || _movement == null) return;

            // Reset combo step if player waited too long
            if (_currentComboStep > 0 && Time.time > _lastAttackTime + _comboResetTime)
            {
                _currentComboStep = 0;
            }

            if (_input.HasBufferedAttack)
            {
                TryAttack();
            }
        }

        private void TryAttack()
        {
            // Case 1: Dash Attack (GDD: Attack during Dash fires projectile)
            if (_dash != null && _dash.IsDashing)
            {
                if (!_hasFiredDashAttackInCurrentDash && CanFireProjectile)
                {
                    _input.ConsumeAttackBuffer();
                    ExecuteDashAttack();
                }
                return;
            }

            // Case 2: Melee Combo (2 steps)
            float requiredCooldown = (_currentComboStep == 1) ? _attackCooldown : _comboEndCooldown;
            if (Time.time >= _lastAttackTime + requiredCooldown)
            {
                _input.ConsumeAttackBuffer();
                ExecuteMeleeAttack();
            }
        }

        private void ExecuteMeleeAttack()
        {
            // หากมีการซูมหรือสโลว์โมชั่นค้างอยู่จากการเล็ง/แดช ให้ยกเลิกทันทีเมื่อกดโจมตีระยะประชิด
            if (_isCinematicActive)
            {
                AnimEvent_EndCinematicZoom();
            }

            _lastAttackTime = Time.time;
            _currentComboStep++;
            _lastExecutedComboStep = _currentComboStep; // remember for Animation Event

            // Lock movement and facing direction during melee attack
            float lockDuration = (_currentComboStep == 1) ? _attack1LockDuration : _attack2LockDuration;
            LockMovementForDuration(lockDuration);

            // Fire event → PlayerAnimation will play correct clip
            // Hitbox open/close is handled by Animation Events via PlayerAnimationEventReceiver
            OnMeleeAttackStarted?.Invoke(_currentComboStep);
            if (_lastExecutedComboStep == 1) _onAttack1Event?.Invoke();
            else if (_lastExecutedComboStep == 2) _onAttack2Event?.Invoke();

            // GDD: Combo has 2 hits max, then resets to 0
            if (_currentComboStep >= 2)
            {
                _currentComboStep = 0;
            }
        }

        // ─── Methods called by PlayerAnimationEventReceiver (Animation Events) ───────

        /// <summary>
        /// Called by Animation Event: เปิด hitbox ในจุดที่กำหนด
        /// </summary>
        public void AnimEvent_OpenHitbox()
        {
            if (_hitbox == null) return;
            // Use _lastExecutedComboStep which is preserved even after _currentComboStep resets
            int damage = (_lastExecutedComboStep == 1) ? _hit1Damage : _hit2Damage;
            float knockback = (_lastExecutedComboStep == 1) ? _hit1Knockback : _hit2Knockback;
            bool isHit2 = (_lastExecutedComboStep == 2);
            _hitbox.Open(damage, _movement.FacingDirection, knockback, gameObject, isHit2, _hit2FreezeDuration);
        }

        /// <summary>
        /// Called by Animation Event: ปิด hitbox
        /// </summary>
        public void AnimEvent_CloseHitbox()
        {
            if (_hitbox != null) _hitbox.Close();
        }

        /// <summary>
        /// Called by Animation Event: ยิง projectile สำหรับ Dash Attack
        /// </summary>
        public void AnimEvent_FireProjectile()
        {
            // หากมีการซูมกล้องและสโลว์โมชั่นค้างอยู่ ให้ซูมออกและคืนเวลาสู่ปกติทันที
            if (_isCinematicActive)
            {
                AnimEvent_EndCinematicZoom();
            }

            if (_projectilePrefab == null) return;
            Vector3 spawnPos = _projectileSpawnPoint != null ? _projectileSpawnPoint.position : transform.position;
            GameObject projObj = Instantiate(_projectilePrefab, spawnPos, Quaternion.identity);
            if (projObj.TryGetComponent<PlayerProjectile>(out var projectile))
            {
                Vector2 dir = new Vector2(_movement.FacingDirection, 0f);
                projectile.Launch(dir, gameObject);
            }
        }

        private void LockMovementForDuration(float duration)
        {
            // Cancel previous lock if still running (e.g. combo Hit 2 fired before Hit 1 lock expired)
            if (_attackLockCoroutine != null)
            {
                StopCoroutine(_attackLockCoroutine);
            }

            _movement.SetMovementLocked(true);
            _movement.StopHorizontalVelocity();
            _attackLockCoroutine = StartCoroutine(UnlockMovementAfter(duration));
        }

        private IEnumerator UnlockMovementAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            _movement.SetMovementLocked(false);
            _attackLockCoroutine = null;
        }

        private void ExecuteDashAttack()
        {
            _hasFiredDashAttackInCurrentDash = true;
            _lastProjectileAttackTime = Time.time;

            // Projectile is fired by Animation Event (AnimEvent_FireProjectile)
            // This just notifies subscribers (e.g. PlayerAnimation to play DashAttack clip)
            OnDashAttackTriggered?.Invoke();
            _onDashAttackEvent?.Invoke();
        }

        private void HandleDashStarted(int direction)
        {
            _hasFiredDashAttackInCurrentDash = false;
        }

        private void HandleDashEnded()
        {
            // หาก Dash จบลงแล้วแต่ยังมี Cinematic Zoom ค้างอยู่ (เช่น ออกท่าไม่สำเร็จ หรือถูกขัดจังหวะ) ให้คืนค่าทันที
            if (_isCinematicActive)
            {
                AnimEvent_EndCinematicZoom();
            }
        }

        private void HandlePlayerDamaged(int damage)
        {
            // หากได้รับดาเมจระหว่างชาร์จ/สโลว์โมชั่น ให้แคนเซิลและคืนค่ากล้องกับเวลาทันที
            if (_isCinematicActive)
            {
                AnimEvent_EndCinematicZoom();
            }
        }

        private void HandlePlayerDeath()
        {
            // หากผู้เล่นเสียชีวิต ให้คืนค่ากล้องกับเวลาทันที
            if (_isCinematicActive)
            {
                AnimEvent_EndCinematicZoom();
            }
        }

        #region Cinematic Zoom & Slow-Motion

        /// <summary>
        /// Animation Event: เริ่มซูมกล้องเข้าหาตัวละครและ Slow-motion เวลา
        /// </summary>
        public void AnimEvent_StartCinematicZoom()
        {
            if (!_enableCinematicZoom) return;

            _isCinematicActive = true;
            Time.timeScale = _slowMotionTimeScale;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;

            if (CameraController2D.Instance != null)
            {
                CameraController2D.Instance.ZoomTo(_zoomInSize, _zoomInSpeed);
            }

            // เริ่มนับเวลา Safety Timeout (Realtime) เพื่อป้องกันการค้างกรณีไม่มี Event ปิด หรือโดนขัดจังหวะ
            if (_cinematicTimeoutCoroutine != null)
            {
                StopCoroutine(_cinematicTimeoutCoroutine);
            }
            _cinematicTimeoutCoroutine = StartCoroutine(CinematicSafetyTimeoutRoutine(_cinematicSafetyTimeout));
        }

        /// <summary>
        /// Animation Event: สิ้นสุด Slow-motion และคืนขนาดกล้องกลับเป็นปกติ
        /// </summary>
        public void AnimEvent_EndCinematicZoom()
        {
            if (_cinematicTimeoutCoroutine != null)
            {
                StopCoroutine(_cinematicTimeoutCoroutine);
                _cinematicTimeoutCoroutine = null;
            }

            if (!_isCinematicActive) return;

            _isCinematicActive = false;
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = 0.02f;

            if (CameraController2D.Instance != null)
            {
                CameraController2D.Instance.ResetZoom(_zoomOutSpeed);
            }
        }

        private IEnumerator CinematicSafetyTimeoutRoutine(float timeout)
        {
            yield return new WaitForSecondsRealtime(timeout);
            if (_isCinematicActive)
            {
                AnimEvent_EndCinematicZoom();
            }
        }

        #endregion
    }
}

