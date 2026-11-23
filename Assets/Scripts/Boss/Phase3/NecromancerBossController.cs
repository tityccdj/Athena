using System;
using System.Collections;
using System.Collections.Generic;
using Athena.Core;
using Athena.Boss.Core;
using Athena.Player;
using UnityEngine;

namespace Athena.Boss.Phase3
{
    public enum NecromancerAttackType
    {
        None,
        MeleeSlash,    // ท่า 1: พุ่งฟันด้านหน้า (ใกล้) บอสขยับช้าลงเล็กน้อยตอนฟัน
        ExplosionCast, // ท่า 2: สกิลระเบิด (แทน Meteor) เสก Telegraph ใต้ผู้เล่นแล้วระเบิด
        MeteorSummon = ExplosionCast // สำหรับ backward compatibility
    }

    public enum NecromancerBossState
    {
        Intro,
        Idle,
        FloatChase,
        Attacking,
        Recovery,
        Defeated
    }

    /// <summary>
    /// AI Controller ของบอส Phase 3 (Necromancer) ตาม GDD:
    /// - เลือด 60 HP (1% - 19%)
    /// - ขยับไวมาก, ลอยกลางอากาศ (บินทะลุสิ่งกีดขวางได้) โดยไม่มี Rigidbody (Pure Transform)
    /// - สุ่มท่าตามระยะห่าง (Distance):
    ///     1. Melee: โจมตีด้านหน้า (ระหว่างปล่อยท่าจะเคลื่อนที่ช้าลงเล็กน้อย) (Player ใกล้)
    ///     2. Meteor: เรียกอุกกาบาตตกลงมาตรงตำแหน่งที่ Player ยืนอยู่ (Player ไกล)
    /// - ห้ามออกท่าเดิมซ้ำกันเกิน 2 ครั้งติดกัน
    /// - Win State: เมื่อตาย แสดง Effect Slow-motion + Zoom-in และค่อยๆ Zoom-out กลับมาปกติ
    /// </summary>
    [RequireComponent(typeof(BossHealth))]
    public class NecromancerBossController : MonoBehaviour
    {
        [Header("Intro Settings")]
        [SerializeField] private bool _playIntroOnStart = true;
        [SerializeField] private float _introDuration = 2.0f;

        [Header("Movement Settings (GDD: ขยับไวมาก, ลอยกลางอากาศ บินทะลุสิ่งกีดขวาง)")]
        [SerializeField] private float _floatSpeed = 7.5f;
        [SerializeField] private float _preferredHoverHeight = 2.4f;
        [SerializeField] private float _bobbingFrequency = 2.5f;
        [SerializeField] private float _bobbingAmplitude = 0.4f;
        [SerializeField] private float _meleeRange = 3.6f;
        [SerializeField] private float _meleeAttackMoveSpeedRatio = 0.3f; // เคลื่อนที่ช้าลงเล็กน้อยตอนฟัน

        [Header("AI Timing Settings")]
        [SerializeField] private float _idleTimeBetweenAttacks = 0.7f;
        [SerializeField] private float _attackRecoveryTime = 0.5f;

        [Header("Attack Hitboxes")]
        [SerializeField] private BossDamageHitbox _meleeAttackHitbox;

        [Header("Explosion Skill Settings (ท่า 2: สกิลระเบิด แทน Meteor)")]
        [SerializeField] private GameObject _explosionTelegraphPrefab;
        [SerializeField] private float _explosionYOffset = 0f;

        [Header("Phase Transition & Win State Settings")]
        [SerializeField] private float _deathAnimationDuration = 2.5f;
        [SerializeField] private float _slowMoTimeScale = 0.2f;
        [SerializeField] private float _slowMoDuration = 1.5f; // เวลาแบบ Realtime
        [SerializeField] private float _zoomInCameraSize = 3.2f;

        [Header("Visual & Direction")]
        [SerializeField] private Transform _visualTransform;
        [SerializeField] private bool _spriteFacesRightByDefault = true;

        private BossHealth _health;
        private PlayerController _player;
        private NecromancerAnimation _anim;
        private NecromancerAnimationEventReceiver _eventReceiver;

        private NecromancerBossState _currentState = NecromancerBossState.Idle;
        private NecromancerAttackType _pendingAttack = NecromancerAttackType.None;
        private NecromancerAttackType _lastAttack = NecromancerAttackType.None;
        private int _consecutiveAttackCount = 0;

        private float _stateTimer = 0f;
        private int _facingDirection = 1;
        private float _baseYPosition;
        private bool _isAttackingMelee = false;
        private Coroutine _introCoroutine;
        private Coroutine _safetyTimeoutCoroutine;
        private Coroutine _winStateCoroutine;

        public NecromancerBossState CurrentState => _currentState;
        public int FacingDirection => _facingDirection;

        public static event Action OnNecromancerDefeated;

        private void Awake()
        {
            _health = GetComponent<BossHealth>();
            _anim = GetComponentInChildren<NecromancerAnimation>();
            _eventReceiver = GetComponentInChildren<NecromancerAnimationEventReceiver>();

            if (_eventReceiver != null)
            {
                _eventReceiver.Initialize(this, _anim);
            }

            if (_visualTransform == null)
            {
                _visualTransform = transform;
            }

            _baseYPosition = transform.position.y;
            _health.OnDefeated += HandleDefeated;
        }

        private void OnEnable()
        {
            if (_playIntroOnStart)
            {
                SetState(NecromancerBossState.Intro);
            }
        }

        private void Start()
        {
            _player = FindFirstObjectByType<PlayerController>();
            _baseYPosition = transform.position.y;
            _meleeAttackHitbox?.UpdateFacing(_facingDirection);

            if (_currentState != NecromancerBossState.Intro)
            {
                SetState(_playIntroOnStart ? NecromancerBossState.Intro : NecromancerBossState.Idle);
            }
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.OnDefeated -= HandleDefeated;
            }
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.K))
            {
                DebugKillPhase3();
                return;
            }
#endif

            if (_currentState == NecromancerBossState.Defeated) return;
            if (_player == null)
            {
                _player = FindFirstObjectByType<PlayerController>();
                if (_player == null) return;
            }

            // หันหน้าเข้าหา Player ตอนไม่ได้อยู่ในจังหวะฟัน melee
            if (_currentState != NecromancerBossState.Attacking || !_isAttackingMelee)
            {
                UpdateFacingDirection();
            }

            // อัปเดตตำแหน่งการลอยกลางอากาศ (Pure Transform Movement - ไม่มี Rigidbody)
            UpdateFloatingMovement();

            switch (_currentState)
            {
                case NecromancerBossState.Idle:
                    UpdateIdleState();
                    break;
                case NecromancerBossState.FloatChase:
                    UpdateFloatChaseState();
                    break;
            }
        }

        private void UpdateFloatingMovement()
        {
            if (_currentState == NecromancerBossState.Defeated || _currentState == NecromancerBossState.Intro)
            {
                return;
            }

            if (_player == null) return;

            // การเคลื่อนที่แบบลอยอากาศ (Pure Transform Flying Movement)
            if (_currentState == NecromancerBossState.FloatChase)
            {
                ApplyFloatingMovement(_floatSpeed);
            }
            else if (_currentState == NecromancerBossState.Attacking && _isAttackingMelee)
            {
                // GDD: ระหว่างปล่อยท่าโจมตี ตัวบอสจะเคลื่อนที่ช้าลงเล็กน้อย
                ApplyFloatingMovement(_floatSpeed * _meleeAttackMoveSpeedRatio);
            }
            else
            {
                // ยืนลอยนิ่งๆ พร้อม Sine Wave กระเพื่อมเบาๆ
                float bobOffset = Mathf.Sin(Time.time * _bobbingFrequency) * _bobbingAmplitude;
                float targetY = _player.transform.position.y + _preferredHoverHeight + bobOffset;
                float newY = Mathf.MoveTowards(transform.position.y, targetY, _floatSpeed * 0.5f * Time.deltaTime);
                transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            }
        }

        private void ApplyFloatingMovement(float speed)
        {
            Vector3 playerPos = _player.transform.position;
            float bobOffset = Mathf.Sin(Time.time * _bobbingFrequency) * _bobbingAmplitude;
            float targetY = playerPos.y + _preferredHoverHeight + bobOffset;

            // บินเข้าหา หรือรักษาระยะห่าง
            float targetX = playerPos.x + (_facingDirection * -1.5f);
            Vector3 targetPos = new Vector3(targetX, targetY, transform.position.z);

            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
        }

        private void UpdateFacingDirection()
        {
            float diffX = _player.transform.position.x - transform.position.x;
            if (Mathf.Abs(diffX) > 0.15f)
            {
                _facingDirection = diffX > 0 ? 1 : -1;
                Vector3 scale = _visualTransform.localScale;
                float sign = _spriteFacesRightByDefault ? _facingDirection : -_facingDirection;
                scale.x = Mathf.Abs(scale.x) * sign;
                _visualTransform.localScale = scale;

                _meleeAttackHitbox?.UpdateFacing(_facingDirection);
            }
        }

        private void SetState(NecromancerBossState newState)
        {
            _currentState = newState;
            _stateTimer = Time.time;

            switch (newState)
            {
                case NecromancerBossState.Intro:
                    _isAttackingMelee = false;
                    if (_health != null) _health.SetInvulnerable(true);
                    if (_anim != null) _anim.PlayStart();
                    if (_introCoroutine != null) StopCoroutine(_introCoroutine);
                    _introCoroutine = StartCoroutine(IntroRoutine());
                    break;

                case NecromancerBossState.Idle:
                    _isAttackingMelee = false;
                    if (_anim != null) _anim.PlayIdle();
                    break;

                case NecromancerBossState.FloatChase:
                    _isAttackingMelee = false;
                    if (_anim != null) _anim.PlayWalk();
                    break;

                case NecromancerBossState.Attacking:
                    ExecuteAttackAnimation(_pendingAttack);
                    break;

                case NecromancerBossState.Recovery:
                    _isAttackingMelee = false;
                    if (_anim != null) _anim.PlayIdle();
                    StartCoroutine(RecoveryRoutine());
                    break;

                case NecromancerBossState.Defeated:
                    _isAttackingMelee = false;
                    StopAllCoroutines();
                    CloseAllHitboxes();
                    if (_anim != null)
                    {
                        _anim.StopTelegraph();
                        _anim.PlayDeath();
                    }
                    break;
            }
        }

        private void UpdateIdleState()
        {
            if (Time.time >= _stateTimer + _idleTimeBetweenAttacks)
            {
                DecideNextAction();
            }
        }

        private void UpdateFloatChaseState()
        {
            float distance = Mathf.Abs(_player.transform.position.x - transform.position.x);

            if (distance <= _meleeRange)
            {
                DecideNextAction();
                return;
            }
        }

        /// <summary>
        /// สุ่มเลือกท่าตามระยะห่าง (Distance) + ป้องกันท่าซ้ำเกิน 2 ครั้ง (GDD)
        /// </summary>
        private void DecideNextAction()
        {
            float distance = Mathf.Abs(_player.transform.position.x - transform.position.x);
            List<NecromancerAttackType> candidates = new List<NecromancerAttackType>();

            if (distance <= _meleeRange)
            {
                // ใกล้: เน้นท่า Melee (หรือมีโอกาสเสกระเบิดสวน)
                candidates.Add(NecromancerAttackType.MeleeSlash);
                candidates.Add(NecromancerAttackType.MeleeSlash);
                candidates.Add(NecromancerAttackType.ExplosionCast);
            }
            else
            {
                // ไกล: สกิลระเบิด (Explosion) หรือบินเข้าหา
                candidates.Add(NecromancerAttackType.ExplosionCast);

                if (UnityEngine.Random.value < 0.4f)
                {
                    SetState(NecromancerBossState.FloatChase);
                    return;
                }
            }

            // GDD: จะไม่สุ่มท่าเดิมซ้ำกันเกิน 2 ครั้งติดกัน
            if (_consecutiveAttackCount >= 2 && candidates.Contains(_lastAttack))
            {
                candidates.RemoveAll(a => a == _lastAttack);
            }

            if (candidates.Count == 0)
            {
                SetState(NecromancerBossState.FloatChase);
                return;
            }

            NecromancerAttackType chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];

            if (chosen == _lastAttack)
            {
                _consecutiveAttackCount++;
            }
            else
            {
                _lastAttack = chosen;
                _consecutiveAttackCount = 1;
            }

            _pendingAttack = chosen;
            SetState(NecromancerBossState.Attacking);
        }

        private void ExecuteAttackAnimation(NecromancerAttackType attackType)
        {
            if (_anim != null)
            {
                _anim.StartTelegraphFlash();

                switch (attackType)
                {
                    case NecromancerAttackType.MeleeSlash:
                        _isAttackingMelee = true;
                        _anim.PlayAttack01_Melee();
                        break;
                    case NecromancerAttackType.ExplosionCast:
                        _isAttackingMelee = false;
                        _anim.PlayAttack02_Explosion();
                        SpawnExplosionTelegraph();
                        break;
                }
            }

            if (_safetyTimeoutCoroutine != null) StopCoroutine(_safetyTimeoutCoroutine);
            _safetyTimeoutCoroutine = StartCoroutine(AttackSafetyTimeoutRoutine(3.5f));
        }

        private IEnumerator AttackSafetyTimeoutRoutine(float maxDuration)
        {
            yield return new WaitForSeconds(maxDuration);
            if (_currentState == NecromancerBossState.Attacking)
            {
                UnityEngine.Debug.LogWarning("[NecromancerBossController] Attack timed out!");
                AnimEvent_OnAttackComplete();
            }
        }

        // ─── Animation Events Hooks ───────

        public void AnimEvent_OnTelegraphEnd()
        {
            if (_currentState != NecromancerBossState.Attacking) return;
            if (_anim != null) _anim.StopTelegraph();
        }

        public void AnimEvent_OpenHitbox()
        {
            if (_currentState != NecromancerBossState.Attacking) return;

            if (_pendingAttack == NecromancerAttackType.MeleeSlash)
            {
                if (_meleeAttackHitbox != null)
                {
                    _meleeAttackHitbox.UpdateFacing(_facingDirection);
                    _meleeAttackHitbox.OpenHitbox();
                }
            }
        }

        public void AnimEvent_CloseHitbox()
        {
            CloseAllHitboxes();
        }

        // ─── Explosion Skill Methods ───────

        /// <summary>
        /// เสก Telegraph ที่ตำแหน่ง Player — Telegraph จัดการ lifecycle ตัวเอง:
        /// เล่น anim จบ → spawn Explosion → ทำลายตัวเอง
        /// Boss ไม่ต้อง track หรือ cleanup
        /// </summary>
        private void SpawnExplosionTelegraph()
        {
            if (_player == null) return;

            Vector3 targetPos = new Vector3(
                _player.transform.position.x,
                _player.transform.position.y + _explosionYOffset,
                _player.transform.position.z
            );

            if (_explosionTelegraphPrefab != null)
            {
                Instantiate(_explosionTelegraphPrefab, targetPos, Quaternion.identity);
                // ExplosionTelegraphEffect บน prefab จะจัดการ:
                // 1. เล่น anim จนจบ
                // 2. spawn Explosion prefab
                // 3. ทำลายตัวเอง
            }
        }

        /// <summary>
        /// Animation Event: เสก Telegraph (สำรองกรณีเรียกจาก anim event)
        /// </summary>
        public void AnimEvent_OnSpawnTelegraph()
        {
            if (_currentState != NecromancerBossState.Attacking) return;
            if (_pendingAttack != NecromancerAttackType.ExplosionCast) return;
            SpawnExplosionTelegraph();
        }

        /// <summary>
        /// Animation Event: สำรองไว้เพื่อ backward compatibility กับ EventReceiver
        /// ในระบบใหม่ telegraph จัดการ spawn explosion เอง
        /// </summary>
        public void AnimEvent_OnFireExplosion()
        {
            // ไม่ต้องทำอะไร — ExplosionTelegraphEffect จัดการเองแล้ว
        }

        public void AnimEvent_OnAttackComplete()
        {
            if (_currentState != NecromancerBossState.Attacking) return;

            if (_safetyTimeoutCoroutine != null)
            {
                StopCoroutine(_safetyTimeoutCoroutine);
                _safetyTimeoutCoroutine = null;
            }

            _isAttackingMelee = false;
            CloseAllHitboxes();
            if (_anim != null) _anim.StopTelegraph();

            // Boss เข้า Recovery ทันที — telegraph/explosion จัดการตัวเองอิสระ
            SetState(NecromancerBossState.Recovery);
        }

        private IEnumerator RecoveryRoutine()
        {
            yield return new WaitForSeconds(_attackRecoveryTime);

            if (_currentState == NecromancerBossState.Recovery)
            {
                SetState(NecromancerBossState.Idle);
            }
        }

        private void CloseAllHitboxes()
        {
            if (_meleeAttackHitbox != null) _meleeAttackHitbox.CloseHitbox();
        }

        private IEnumerator IntroRoutine()
        {
            yield return new WaitForSeconds(_introDuration);
            FinishIntro();
        }

        public void AnimEvent_OnIntroComplete()
        {
            if (_introCoroutine != null)
            {
                StopCoroutine(_introCoroutine);
                _introCoroutine = null;
            }
            FinishIntro();
        }

        private void FinishIntro()
        {
            if (_currentState != NecromancerBossState.Intro) return;

            if (_health != null) _health.SetInvulnerable(false);
            SetState(NecromancerBossState.Idle);
            UnityEngine.Debug.Log("[NecromancerBossController] Phase 3 Intro complete! Fight starts!");
        }

        // ─── Win State (GDD: Slow-motion + Zoom-in and Zoom-out) ───────

        private void HandleDefeated()
        {
            SetState(NecromancerBossState.Defeated);
            if (_winStateCoroutine != null) StopCoroutine(_winStateCoroutine);
            _winStateCoroutine = StartCoroutine(WinStateSequenceRoutine());
        }

        private IEnumerator WinStateSequenceRoutine()
        {
            UnityEngine.Debug.Log("[NecromancerBossController] WIN STATE TRIGGERED! Starting Slow-Mo & Zoom sequence...");

            // 1. Slow-Motion: ลด Time.timeScale ลงเหลือ _slowMoTimeScale
            Time.timeScale = _slowMoTimeScale;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;

            // 2. Camera Zoom-In: สั่ง CameraController2D ให้ซูมเข้าไปที่ร่าง Necromancer
            if (Athena.CameraSystem.CameraController2D.Instance != null)
            {
                Athena.CameraSystem.CameraController2D.Instance.TriggerWinZoom(transform, _zoomInCameraSize, _slowMoDuration);
            }

            // รอระยะเวลาสโลว์โมชั่น (ใช้ WaitForSecondsRealtime เพื่อไม่ให้ดีเลย์ตาม timeScale)
            yield return new WaitForSecondsRealtime(_slowMoDuration);

            // 3. คืนค่า Time.timeScale กลับสู่ปกติ
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = 0.02f;

            // 4. รอให้แอนิเมชั่นตายเล่นจบ
            yield return new WaitForSeconds(_deathAnimationDuration);

            TriggerGameVictory();
        }

        public void AnimEvent_OnDeathComplete()
        {
            // ถ้ามี Animation Event
        }

        private void TriggerGameVictory()
        {
            UnityEngine.Debug.Log("[NecromancerBossController] Necromancer defeated! Game Complete!");
            OnNecromancerDefeated?.Invoke();

            if (BossManager.Instance != null)
            {
                BossManager.Instance.AdvanceToNextPhase(); // Phase 4 -> Victory
            }
        }

        [ContextMenu("Debug: Kill Phase 3 (Instant 0 HP)")]
        public void DebugKillPhase3()
        {
            if (_health != null)
            {
                _health.TakeDamage(new DamageInfo(9999, transform.position, Vector2.zero, 0f, gameObject));
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _meleeRange);
        }
    }
}
