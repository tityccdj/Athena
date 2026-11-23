using System;
using System.Collections;
using System.Collections.Generic;
using Athena.Core;
using Athena.Boss.Core;
using Athena.Player;
using UnityEngine;

namespace Athena.Boss.Phase2
{
    public enum EliteOrcAttackType
    {
        None,
        FrontSlash,   // ท่า 1: ชาร์จ -> ฟันกวาดด้านหน้า (ใกล้)
        SpinTornado,  // ท่า 2: ชาร์จ 1 วินาที -> เพิ่มความเร็ววิ่ง พุ่งตัวหมุนเป็นลูกข่างต่อเนื่อง (ใกล้/ไกล)
        SlashAndWave  // ท่า 3: ชาร์จ -> ฟันด้านหน้า และปล่อย Shockwave เรียดพื้น (ใกล้/ไกล)
    }

    public enum TornadoMovementMode
    {
        TrackPlayer,       // หมุนไล่ตาม Player ตามแกน X (เลี้ยวหา Player)
        RandomWander,      // สุ่มสลับทิศทางซ้าย-ขวาไปมาระหว่างหมุน
        RandomDirection,   // สุ่มเลือกทิศทางซ้ายหรือขวา (50/50) ตอนเริ่มหมุน
        FacingDirection    // พุ่งไปข้างหน้าตามทิศทางเดิมที่หันอยู่
    }

    public enum EliteOrcBossState
    {
        Intro,
        Idle,
        Chase,
        Attacking,
        Recovery,
        Defeated
    }

    /// <summary>
    /// AI Controller ของบอส Phase 2 (Elite Orc) ตาม GDD:
    /// - เลือด 120 HP (20% - 59%)
    /// - ความเร็วระดับกลาง
    /// - สุ่มท่าตามระยะห่าง (Distance)
    /// - ห้ามออกท่าเดิมซ้ำกันเกิน 2 ครั้งติดกัน
    /// - ท่า 1: ฟันด้านหน้า (ใกล้)
    /// - ท่า 2: พุ่งตัวหมุนเป็นลูกข่างโจมตีต่อเนื่องระยะเวลาหนึ่ง (ใกล้/ไกล)
    /// - ท่า 3: ฟันด้านหน้า และปล่อยคลื่น Shockwave (ใกล้/ไกล)
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BossHealth))]
    public class EliteOrcBossController : MonoBehaviour
    {
        [Header("Intro Settings (GDD: Phase 2 Start)")]
        [Tooltip("เล่นแอนิเมชันเปิดตัว EliteOrc_Start เมื่อเริ่ม Phase 2")]
        [SerializeField] private bool _playIntroOnStart = true;
        [Tooltip("ระยะเวลาเล่น Intro ก่อนเริ่มต่อสู้ (หากมี Animation Event 'OnIntroComplete' จะสิ้นสุดทันที)")]
        [SerializeField] private float _introDuration = 2.08f;

        [Header("Movement Settings (GDD: ความเร็วระดับกลาง)")]
        [SerializeField] private float _walkSpeed = 4.2f;
        [SerializeField] private float _meleeRange = 3.0f;

        [Header("Tornado Movement Settings (ท่า 2: Spin Tornado)")]
        [Tooltip("โหมดการเคลื่อนที่ของท่า Tornado:\n" +
                 "- TrackPlayer: หมุนไล่ตาม Player ตามแกน X (เลี้ยวหา Player)\n" +
                 "- RandomWander: สุ่มสลับทิศทางซ้าย-ขวาไปมาระหว่างหมุน\n" +
                 "- RandomDirection: สุ่มเลือกทิศทางซ้ายหรือขวา (50/50) ตอนเริ่มหมุน\n" +
                 "- FacingDirection: พุ่งไปข้างหน้าตามทิศทางเดิมที่หันอยู่")]
        [SerializeField] private TornadoMovementMode _tornadoMovementMode = TornadoMovementMode.TrackPlayer;
        [SerializeField] private float _spinTornadoSpeed = 9f;
        [Tooltip("ระยะเผื่อแกน X สำหรับโหมด TrackPlayer (เพื่อป้องกันการสั่นเมื่ออยู่ใกล้ผู้เล่น)")]
        [SerializeField] private float _tornadoTrackDeadzone = 0.35f;
        [Tooltip("ช่วงเวลาสุ่มเปลี่ยนทิศต่ำสุด สำหรับโหมด RandomWander (วินาที)")]
        [SerializeField] private float _tornadoMinWanderTime = 0.4f;
        [Tooltip("ช่วงเวลาสุ่มเปลี่ยนทิศสูงสุด สำหรับโหมด RandomWander (วินาที)")]
        [SerializeField] private float _tornadoMaxWanderTime = 0.8f;
        [Tooltip("เด้งกลับทิศทางเมื่อพุ่งชนกำแพง/สิ่งกีดขวาง")]
        [SerializeField] private bool _bounceOnWallHit = true;

        [Header("AI Timing Settings")]
        [SerializeField] private float _idleTimeBetweenAttacks = 0.8f;
        [SerializeField] private float _attackRecoveryTime = 0.6f;

        [Header("Attack Hitboxes")]
        [SerializeField] private BossDamageHitbox _frontAttackHitbox;
        [SerializeField] private BossDamageHitbox _spinAttackHitbox;

        [Header("Shockwave Projectile (ท่า 3: Slash and Wave)")]
        [SerializeField] private GameObject _shockwavePrefab;
        [SerializeField] private Transform _shockwaveSpawnPoint;
        [SerializeField] private LayerMask _targetPlayerLayer;
        [SerializeField] private LayerMask _obstacleLayer;

        [Header("Phase Transition Settings")]
        [SerializeField] private float _deathAnimationDuration = 1.6f;

        [Header("Death Zoom Effect (Slow-motion + Zoom-in)")]
        [SerializeField] private float _deathSlowMoTimeScale = 0.2f;
        [SerializeField] private float _deathSlowMoDuration = 1.5f;
        [SerializeField] private float _deathZoomCameraSize = 3.2f;

        [Header("Visual & Direction")]
        [SerializeField] private Transform _visualTransform;
        [SerializeField] private bool _spriteFacesRightByDefault = true;

        private Rigidbody2D _rb;
        private BossHealth _health;
        private PlayerController _player;
        private EliteOrcAnimation _anim;
        private EliteOrcAnimationEventReceiver _eventReceiver;

        private EliteOrcBossState _currentState = EliteOrcBossState.Idle;
        private EliteOrcAttackType _pendingAttack = EliteOrcAttackType.None;
        private EliteOrcAttackType _lastAttack = EliteOrcAttackType.None;
        private int _consecutiveAttackCount = 0;

        private float _stateTimer = 0f;
        private int _facingDirection = 1; // 1 = Right, -1 = Left
        private bool _isSpinRushing = false;
        private int _tornadoCurrentDir = 1;
        private float _nextTornadoDirectionChangeTime = 0f;
        private float _initialShockwaveSpawnPosX;
        private Coroutine _introCoroutine;
        private Coroutine _safetyTimeoutCoroutine;
        private Coroutine _deathTransitionCoroutine;

        public EliteOrcBossState CurrentState => _currentState;
        public int FacingDirection => _facingDirection;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _health = GetComponent<BossHealth>();
            _anim = GetComponentInChildren<EliteOrcAnimation>();
            _eventReceiver = GetComponentInChildren<EliteOrcAnimationEventReceiver>();

            if (_rb != null)
            {
                // น้ำหนัก 5000 + ล็อคแกน X ตอนยืนนิ่ง ป้องกัน Player เดินดันบอส
                _rb.mass = 5000f;
                _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                SetHorizontalLock(true);
            }

            if (_eventReceiver != null)
            {
                _eventReceiver.Initialize(this, _anim);
            }

            if (_visualTransform == null)
            {
                _visualTransform = transform;
            }

            if (_shockwaveSpawnPoint == null)
            {
                _shockwaveSpawnPoint = transform;
            }
            else if (_shockwaveSpawnPoint != transform)
            {
                _initialShockwaveSpawnPosX = Mathf.Abs(_shockwaveSpawnPoint.localPosition.x);
            }

            _health.OnDefeated += HandleDefeated;
        }

        private void OnEnable()
        {
            if (_playIntroOnStart)
            {
                SetState(EliteOrcBossState.Intro);
            }
        }

        private void Start()
        {
            _player = FindFirstObjectByType<PlayerController>();
            _frontAttackHitbox?.UpdateFacing(_facingDirection);
            UpdateShockwaveSpawnPointFacing(_facingDirection);

            if (_currentState != EliteOrcBossState.Intro)
            {
                SetState(_playIntroOnStart ? EliteOrcBossState.Intro : EliteOrcBossState.Idle);
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
            if (_currentState == EliteOrcBossState.Defeated) return;
            if (_player == null)
            {
                _player = FindFirstObjectByType<PlayerController>();
                if (_player == null) return;
            }

            // หันหน้าเข้าหา Player ตอนไม่ได้โจมตี
            if (_currentState == EliteOrcBossState.Idle || _currentState == EliteOrcBossState.Chase)
            {
                UpdateFacingDirection();
            }

            // State Machine Logic
            switch (_currentState)
            {
                case EliteOrcBossState.Idle:
                    UpdateIdleState();
                    break;
                case EliteOrcBossState.Chase:
                    UpdateChaseState();
                    break;
            }
        }

        private void FixedUpdate()
        {
            // ถ้ากำลังหมุนเป็นลูกข่างพุ่งไปข้างหน้า (ท่า 2 Spin Tornado)
            if (_currentState == EliteOrcBossState.Attacking && _isSpinRushing)
            {
                UpdateTornadoMovement();
            }
        }

        private void SetTornadoDirection(int dir)
        {
            if (dir == 0) return;
            _tornadoCurrentDir = dir;
            _facingDirection = dir;

            if (_visualTransform != null)
            {
                Vector3 scale = _visualTransform.localScale;
                float sign = _spriteFacesRightByDefault ? _facingDirection : -_facingDirection;
                scale.x = Mathf.Abs(scale.x) * sign;
                _visualTransform.localScale = scale;
            }

            _frontAttackHitbox?.UpdateFacing(_facingDirection);
            UpdateShockwaveSpawnPointFacing(_facingDirection);
        }

        private void UpdateTornadoMovement()
        {
            switch (_tornadoMovementMode)
            {
                case TornadoMovementMode.TrackPlayer:
                    if (_player != null)
                    {
                        float diffX = _player.transform.position.x - transform.position.x;
                        if (Mathf.Abs(diffX) > _tornadoTrackDeadzone)
                        {
                            int targetDir = diffX > 0 ? 1 : -1;
                            if (targetDir != _tornadoCurrentDir)
                            {
                                SetTornadoDirection(targetDir);
                            }
                        }
                    }
                    break;

                case TornadoMovementMode.RandomWander:
                    if (Time.time >= _nextTornadoDirectionChangeTime)
                    {
                        SetTornadoDirection(-_tornadoCurrentDir);
                        _nextTornadoDirectionChangeTime = Time.time + UnityEngine.Random.Range(_tornadoMinWanderTime, _tornadoMaxWanderTime);
                    }
                    break;

                case TornadoMovementMode.RandomDirection:
                case TornadoMovementMode.FacingDirection:
                    // คงทิศทางเดิม (เด้งกลับเมื่อชนกำแพงหากเปิด _bounceOnWallHit)
                    break;
            }

            _rb.linearVelocity = new Vector2(_tornadoCurrentDir * _spinTornadoSpeed, _rb.linearVelocity.y);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            HandleTornadoWallCollision(collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            HandleTornadoWallCollision(collision);
        }

        private void HandleTornadoWallCollision(Collision2D collision)
        {
            if (!_isSpinRushing || !_bounceOnWallHit || _currentState != EliteOrcBossState.Attacking) return;

            if (_obstacleLayer != 0 && ((1 << collision.gameObject.layer) & _obstacleLayer.value) != 0)
            {
                foreach (ContactPoint2D contact in collision.contacts)
                {
                    if (Vector2.Dot(contact.normal, Vector2.right * _tornadoCurrentDir) < -0.5f)
                    {
                        SetTornadoDirection(-_tornadoCurrentDir);
                        _nextTornadoDirectionChangeTime = Time.time + UnityEngine.Random.Range(_tornadoMinWanderTime, _tornadoMaxWanderTime);
                        break;
                    }
                }
            }
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

                _frontAttackHitbox?.UpdateFacing(_facingDirection);
                UpdateShockwaveSpawnPointFacing(_facingDirection);
            }
        }

        private void UpdateShockwaveSpawnPointFacing(int dir)
        {
            if (_shockwaveSpawnPoint != null && _shockwaveSpawnPoint != transform && _initialShockwaveSpawnPosX > 0.001f)
            {
                Vector3 pos = _shockwaveSpawnPoint.localPosition;
                pos.x = _initialShockwaveSpawnPosX * dir;
                _shockwaveSpawnPoint.localPosition = pos;
            }
        }

        private void SetHorizontalLock(bool locked)
        {
            if (_rb == null) return;
            if (locked)
            {
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                _rb.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;
            }
            else
            {
                _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
        }

        private void SetState(EliteOrcBossState newState)
        {
            _currentState = newState;
            _stateTimer = Time.time;

            switch (newState)
            {
                case EliteOrcBossState.Intro:
                    _isSpinRushing = false;
                    SetHorizontalLock(true);
                    if (_health != null) _health.SetInvulnerable(true);
                    if (_anim != null) _anim.PlayStart();
                    if (_introCoroutine != null) StopCoroutine(_introCoroutine);
                    _introCoroutine = StartCoroutine(IntroRoutine());
                    break;

                case EliteOrcBossState.Idle:
                    _isSpinRushing = false;
                    SetHorizontalLock(true);
                    if (_anim != null) _anim.PlayIdle();
                    break;

                case EliteOrcBossState.Chase:
                    _isSpinRushing = false;
                    SetHorizontalLock(false);
                    if (_anim != null) _anim.PlayWalk();
                    break;

                case EliteOrcBossState.Attacking:
                    _isSpinRushing = false;
                    SetHorizontalLock(true); // ล็อคไว้ขณะชาร์จเตือน
                    ExecuteAttackAnimation(_pendingAttack);
                    break;

                case EliteOrcBossState.Recovery:
                    _isSpinRushing = false;
                    SetHorizontalLock(true);
                    if (_anim != null) _anim.PlayIdle();
                    StartCoroutine(RecoveryRoutine());
                    break;

                case EliteOrcBossState.Defeated:
                    _isSpinRushing = false;
                    StopAllCoroutines();
                    CloseAllHitboxes();

                    // ล็อคให้อยู่กับที่ และตั้งเป็น Kinematic ไม่ให้ตกทะลุพื้น
                    if (_rb != null)
                    {
                        _rb.linearVelocity = Vector2.zero;
                        _rb.constraints = RigidbodyConstraints2D.FreezeAll;
                        _rb.bodyType = RigidbodyType2D.Kinematic;
                    }

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

        private void UpdateChaseState()
        {
            float distance = Mathf.Abs(_player.transform.position.x - transform.position.x);

            // ถ้าเข้าสู่ระยะประชิด ให้เริ่มโจมตี
            if (distance <= _meleeRange)
            {
                DecideNextAction();
                return;
            }

            // วิ่งเข้าหา Player
            _rb.linearVelocity = new Vector2(_facingDirection * _walkSpeed, _rb.linearVelocity.y);
        }

        /// <summary>
        /// สุ่มเลือกท่าตามระยะห่าง (Distance) + ป้องกันท่าซ้ำเกิน 2 ครั้ง (GDD)
        /// </summary>
        private void DecideNextAction()
        {
            float distance = Mathf.Abs(_player.transform.position.x - transform.position.x);
            List<EliteOrcAttackType> candidateAttacks = new List<EliteOrcAttackType>();

            if (distance <= _meleeRange)
            {
                // ระยะใกล้: สุ่มได้ทั้ง 3 ท่า
                candidateAttacks.Add(EliteOrcAttackType.FrontSlash);
                candidateAttacks.Add(EliteOrcAttackType.SpinTornado);
                candidateAttacks.Add(EliteOrcAttackType.SlashAndWave);
            }
            else
            {
                // ระยะไกล: ใช้ท่า Spin Tornado หรือ Slash and Wave
                candidateAttacks.Add(EliteOrcAttackType.SpinTornado);
                candidateAttacks.Add(EliteOrcAttackType.SlashAndWave);

                // มีโอกาสเลือกวิ่งเข้าหาแทน
                if (UnityEngine.Random.value < 0.35f)
                {
                    SetState(EliteOrcBossState.Chase);
                    return;
                }
            }

            // GDD: "จะไม่สุ่มท่าเดิมซ้ำกันเกิน 2 ครั้งติดกัน"
            if (_consecutiveAttackCount >= 2 && candidateAttacks.Contains(_lastAttack))
            {
                candidateAttacks.Remove(_lastAttack);
            }

            if (candidateAttacks.Count == 0)
            {
                SetState(EliteOrcBossState.Chase);
                return;
            }

            EliteOrcAttackType chosen = candidateAttacks[UnityEngine.Random.Range(0, candidateAttacks.Count)];

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
            SetState(EliteOrcBossState.Attacking);
        }

        private void ExecuteAttackAnimation(EliteOrcAttackType attackType)
        {
            if (_anim != null)
            {
                _anim.StartTelegraphFlash(); // กะพริบชาร์จเตือน

                switch (attackType)
                {
                    case EliteOrcAttackType.FrontSlash:
                        _anim.PlayAttack01_Slash();
                        break;
                    case EliteOrcAttackType.SpinTornado:
                        _anim.PlayAttack02_Spin();
                        break;
                    case EliteOrcAttackType.SlashAndWave:
                        _anim.PlayAttack03_SlashAndWave();
                        break;
                }
            }

            if (_safetyTimeoutCoroutine != null) StopCoroutine(_safetyTimeoutCoroutine);
            _safetyTimeoutCoroutine = StartCoroutine(AttackSafetyTimeoutRoutine(3.5f));
        }

        private IEnumerator AttackSafetyTimeoutRoutine(float maxDuration)
        {
            yield return new WaitForSeconds(maxDuration);
            if (_currentState == EliteOrcBossState.Attacking)
            {
                UnityEngine.Debug.LogWarning("[EliteOrcBossController] Attack timed out! Please check Animation Events.");
                AnimEvent_OnAttackComplete();
            }
        }

        // ─── Methods called by EliteOrcAnimationEventReceiver ───────

        /// <summary>
        /// Animation Event: สิ้นสุดช่วงชาร์จเตือน (เริ่มพุ่งตัวสำหรับท่า Spin Tornado)
        /// </summary>
        public void AnimEvent_OnTelegraphEnd()
        {
            if (_currentState != EliteOrcBossState.Attacking) return;
            if (_anim != null) _anim.StopTelegraph();

            if (_pendingAttack == EliteOrcAttackType.SpinTornado)
            {
                SetHorizontalLock(false);
                _isSpinRushing = true;

                // กำหนดทิศทางเริ่มต้นของ Tornado ตามโหมดที่ตั้งไว้ใน Inspector
                switch (_tornadoMovementMode)
                {
                    case TornadoMovementMode.TrackPlayer:
                        if (_player != null)
                        {
                            float diffX = _player.transform.position.x - transform.position.x;
                            SetTornadoDirection(diffX >= 0 ? 1 : -1);
                        }
                        else
                        {
                            SetTornadoDirection(_facingDirection);
                        }
                        break;

                    case TornadoMovementMode.RandomDirection:
                    case TornadoMovementMode.RandomWander:
                        int randomDir = UnityEngine.Random.value < 0.5f ? -1 : 1;
                        SetTornadoDirection(randomDir);
                        _nextTornadoDirectionChangeTime = Time.time + UnityEngine.Random.Range(_tornadoMinWanderTime, _tornadoMaxWanderTime);
                        break;

                    case TornadoMovementMode.FacingDirection:
                    default:
                        SetTornadoDirection(_facingDirection);
                        break;
                }

                _rb.linearVelocity = new Vector2(_tornadoCurrentDir * _spinTornadoSpeed, _rb.linearVelocity.y);
            }
            else if (_pendingAttack == EliteOrcAttackType.FrontSlash)
            {
                SetHorizontalLock(false);
                _rb.linearVelocity = new Vector2(_facingDirection * 2f, _rb.linearVelocity.y);
            }
        }

        /// <summary>
        /// Animation Event: เปิดใช้งาน Hitbox
        /// </summary>
        public void AnimEvent_OpenHitbox()
        {
            if (_currentState != EliteOrcBossState.Attacking) return;

            if (_pendingAttack == EliteOrcAttackType.SpinTornado)
            {
                if (_spinAttackHitbox != null) _spinAttackHitbox.OpenHitbox();
            }
            else
            {
                if (_frontAttackHitbox != null)
                {
                    _frontAttackHitbox.UpdateFacing(_facingDirection);
                    _frontAttackHitbox.OpenHitbox();
                }
            }
        }

        /// <summary>
        /// Animation Event: ปิดใช้งาน Hitbox
        /// </summary>
        public void AnimEvent_CloseHitbox()
        {
            CloseAllHitboxes();
            _isSpinRushing = false;
            SetHorizontalLock(true);
        }

        /// <summary>
        /// Animation Event: ปล่อยคลื่น Shockwave พุ่งเรียดพื้น (สำหรับ Attack03)
        /// </summary>
        public void AnimEvent_OnFireShockwave()
        {
            if (_currentState != EliteOrcBossState.Attacking) return;

            UpdateShockwaveSpawnPointFacing(_facingDirection);

            if (_shockwavePrefab != null)
            {
                Vector3 spawnPos = _shockwaveSpawnPoint != null ? _shockwaveSpawnPoint.position : transform.position;
                GameObject projObj = Instantiate(_shockwavePrefab, spawnPos, Quaternion.identity);

                if (projObj.TryGetComponent<BossShockwaveProjectile>(out var projectile))
                {
                    projectile.Launch(_facingDirection, _targetPlayerLayer, _obstacleLayer);
                }
            }
        }

        /// <summary>
        /// Animation Event: จบท่าโจมตี เข้าสู่ช่วง Recovery
        /// </summary>
        public void AnimEvent_OnAttackComplete()
        {
            if (_currentState != EliteOrcBossState.Attacking) return;

            if (_safetyTimeoutCoroutine != null)
            {
                StopCoroutine(_safetyTimeoutCoroutine);
                _safetyTimeoutCoroutine = null;
            }

            _isSpinRushing = false;
            SetHorizontalLock(true);
            CloseAllHitboxes();
            if (_anim != null) _anim.StopTelegraph();

            SetState(EliteOrcBossState.Recovery);
        }

        private IEnumerator RecoveryRoutine()
        {
            yield return new WaitForSeconds(_attackRecoveryTime);

            if (_currentState == EliteOrcBossState.Recovery)
            {
                SetState(EliteOrcBossState.Idle);
            }
        }

        private void CloseAllHitboxes()
        {
            if (_frontAttackHitbox != null) _frontAttackHitbox.CloseHitbox();
            if (_spinAttackHitbox != null) _spinAttackHitbox.CloseHitbox();
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
            if (_currentState != EliteOrcBossState.Intro) return;

            // ปลด I-Frame (กลายเป็นตีเข้าตามปกติ)
            if (_health != null)
            {
                _health.SetInvulnerable(false);
            }

            // แจ้งให้ BossManager สั่งปิดตัว Phase 1 ที่นอนอยู่บนพื้น
            if (BossManager.Instance != null)
            {
                BossManager.Instance.OnPhase2IntroComplete();
            }

            SetState(EliteOrcBossState.Idle);
            UnityEngine.Debug.Log("[EliteOrcBossController] Phase 2 Intro Finished! Boss is now active and vulnerable.");
        }

        private void HandleDefeated()
        {
            SetState(EliteOrcBossState.Defeated);
            if (_deathTransitionCoroutine != null) StopCoroutine(_deathTransitionCoroutine);
            _deathTransitionCoroutine = StartCoroutine(DefeatedTransitionRoutine());
        }

        private IEnumerator DefeatedTransitionRoutine()
        {
            SetHorizontalLock(true);
            CloseAllHitboxes();
            if (_anim != null)
            {
                _anim.StopTelegraph();
                _anim.PlayDeath();
            }

            // Slow-Motion + Camera Zoom-In (เหมือน Phase 3 Win State)
            Time.timeScale = _deathSlowMoTimeScale;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;

            if (Athena.CameraSystem.CameraController2D.Instance != null)
            {
                Athena.CameraSystem.CameraController2D.Instance.TriggerWinZoom(
                    transform, _deathZoomCameraSize, _deathSlowMoDuration);
            }

            // รอ slow-mo ด้วย realtime เพื่อไม่ให้ถูกชะลอ
            yield return new WaitForSecondsRealtime(_deathSlowMoDuration);

            // คืน Time.timeScale กลับปกติ
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = 0.02f;

            // รอให้อนิเมชั่นตายเล่นจบ
            yield return new WaitForSeconds(_deathAnimationDuration);

            TriggerPhaseAdvance();
        }

        public void AnimEvent_OnDeathComplete()
        {
            if (_deathTransitionCoroutine != null)
            {
                StopCoroutine(_deathTransitionCoroutine);
                _deathTransitionCoroutine = null;
            }

            // คืน timeScale กลับปกติเผื่อ coroutine ยังไม่ถึงจุดคืนค่า
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = 0.02f;

            TriggerPhaseAdvance();
        }

        private void TriggerPhaseAdvance()
        {
            UnityEngine.Debug.Log("[EliteOrcBossController] Phase 2 Defeated! Ghost flying to Room 3...");

            if (BossManager.Instance != null)
            {
                BossManager.Instance.AdvanceToNextPhase();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = transform.position;

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(center, _meleeRange);

            Gizmos.color = new Color(1f, 0f, 1f, 0.9f);
            Gizmos.DrawLine(center + Vector3.left * _meleeRange + Vector3.up * 2f, center + Vector3.left * _meleeRange + Vector3.down * 0.5f);
            Gizmos.DrawLine(center + Vector3.right * _meleeRange + Vector3.up * 2f, center + Vector3.right * _meleeRange + Vector3.down * 0.5f);
            Gizmos.DrawLine(center + Vector3.left * _meleeRange, center + Vector3.right * _meleeRange);

            #if UNITY_EDITOR
            UnityEditor.Handles.color = new Color(1f, 0f, 1f, 0.08f);
            UnityEditor.Handles.DrawSolidDisc(center, Vector3.forward, _meleeRange);

            GUIStyle labelStyle = new GUIStyle
            {
                normal = { textColor = Color.magenta },
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            UnityEditor.Handles.Label(center + Vector3.up * (_meleeRange + 0.4f), $"Phase 2 Melee Range: {_meleeRange:F1}m", labelStyle);
            #endif
        }
    }
}
