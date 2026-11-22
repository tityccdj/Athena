using System;
using System.Collections;
using System.Collections.Generic;
using Athena.Core;
using Athena.Boss.Core;
using Athena.Player;
using UnityEngine;

namespace Athena.Boss.Phase1
{
    public enum OrcAttackType
    {
        None,
        DashAttack,  // ท่า 1: เพิ่มความเร็ววิ่งและโจมตี (ใกล้ หรือ ไกล)
        FrontCleave, // ท่า 2: โจมตีด้านหน้า (ใกล้)
        SpinAttack   // ท่า 3: โจมตีรอบตัว (ใกล้)
    }

    public enum OrcBossState
    {
        Idle,
        Chase,
        TelegraphCharge, // Charge 1 วินาทีตาม GDD
        Attacking,
        Recovery,
        Defeated
    }

    /// <summary>
    /// AI Controller ของบอส Phase 1 (Orc + Rider) ตาม GDD:
    /// - เคลื่อนที่ความเร็วระดับกลาง
    /// - สุ่มท่าตามระยะห่าง (Distance)
    /// - ห้ามออกท่าเดิมซ้ำกันเกิน 2 ครั้งติดกัน
    /// - ทุกท่าจะมี Delay (Charge) 1 วินาทีก่อนเสมอ (Telegraphing)
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BossHealth))]
    public class OrcRiderBossController : MonoBehaviour
    {
        [Header("Movement Settings (GDD: ความเร็วระดับกลาง)")]
        [SerializeField] private float _walkSpeed = 4f;
        [SerializeField] private float _dashAttackSpeed = 14f;
        [SerializeField] private float _meleeRange = 3.2f;

        [Header("AI Timing Settings (GDD: Charge 1 วินาที)")]
        [SerializeField] private float _chargeDelay = 1.0f; // Delay ชาร์จเตือนก่อนฟัน
        [SerializeField] private float _idleTimeBetweenAttacks = 0.8f;
        [SerializeField] private float _attackRecoveryTime = 0.5f;

        [Header("Phase Transition (Death Animation)")]
        [Tooltip("ระยะเวลารอให้ท่าตายเล่นจบ ก่อนสลับเป็น Phase 2 (ถ้าใส่ Animation Event 'OnDeathComplete' จะสลับทันที)")]
        [SerializeField] private float _deathAnimationDuration = 1.4f;

        [Header("Attack Hitboxes")]
        [SerializeField] private BossDamageHitbox _frontAttackHitbox;
        [SerializeField] private BossDamageHitbox _spinAttackHitbox;

        [Header("Visual & Direction")]
        [SerializeField] private Transform _visualTransform;
        [SerializeField] private bool _spriteFacesRightByDefault = true;

        private Rigidbody2D _rb;
        private BossHealth _health;
        private PlayerController _player;
        private OrcRiderAnimation _anim;
        private OrcRiderAnimationEventReceiver _eventReceiver;

        private OrcBossState _currentState = OrcBossState.Idle;
        private OrcAttackType _pendingAttack = OrcAttackType.None;
        private OrcAttackType _lastAttack = OrcAttackType.None;
        private int _consecutiveAttackCount = 0;

        private float _stateTimer = 0f;
        private int _facingDirection = 1; // 1 = Right, -1 = Left
        private bool _isDashing = false;
        private Coroutine _safetyTimeoutCoroutine;
        private Coroutine _deathTransitionCoroutine;

        public OrcBossState CurrentState => _currentState;
        public int FacingDirection => _facingDirection;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _health = GetComponent<BossHealth>();
            _anim = GetComponentInChildren<OrcRiderAnimation>();
            _eventReceiver = GetComponentInChildren<OrcRiderAnimationEventReceiver>();

            if (_rb != null)
            {
                // ตั้งน้ำหนักให้เยอะมาก และล็อคไม่ให้ Player วิ่งมาชนแล้วดันบอสขยับได้
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

            _health.OnDefeated += HandleDefeated;
        }

        /// <summary>
        /// ล็อคแกน X เพื่อไม่ให้ Player สามารถดันบอสได้ตอนยืนอยู่กับที่
        /// </summary>
        [ContextMenu("Debug: Kill Phase 1 (Instant 0 HP)")]
        public void DebugKillPhase1()
        {
            if (_health != null)
            {
                _health.TakeDamage(new DamageInfo(9999, transform.position, Vector2.zero, 0f, gameObject));
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

        private void Start()
        {
            _player = FindFirstObjectByType<PlayerController>();
            _frontAttackHitbox?.UpdateFacing(_facingDirection);
            ResetPhysicsState();
            SetState(OrcBossState.Idle);
        }

        private void ResetPhysicsState()
        {
            if (_rb != null)
            {
                _rb.bodyType = RigidbodyType2D.Dynamic;
                _rb.simulated = true;
                _rb.mass = 5000f;
                _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                SetHorizontalLock(true);
            }

            Collider2D[] colliders = GetComponents<Collider2D>();
            foreach (var col in colliders)
            {
                col.enabled = true;
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
            // ปุ่มลัดสำหรับฆ่า Phase 1 ทันที เพื่อทดสอบดูแอนิเมชันตาย + การเปลี่ยนเข้า Phase 2
            if (Input.GetKeyDown(KeyCode.K))
            {
                DebugKillPhase1();
                return;
            }
#endif

            if (_currentState == OrcBossState.Defeated) return;
            if (_player == null)
            {
                _player = FindFirstObjectByType<PlayerController>();
                if (_player == null) return;
            }

            // หันหน้าเข้าหา Player (ถ้าไม่ได้อยู่ในจังหวะพุ่งโจมตี)
            if (_currentState == OrcBossState.Idle || _currentState == OrcBossState.Chase || _currentState == OrcBossState.TelegraphCharge)
            {
                UpdateFacingDirection();
            }

            // State Machine Logic
            switch (_currentState)
            {
                case OrcBossState.Idle:
                    UpdateIdleState();
                    break;
                case OrcBossState.Chase:
                    UpdateChaseState();
                    break;
            }
        }

        private void FixedUpdate()
        {
            // ถ้ากำลังพุ่ง Dash จาก Animation Event ให้ขับเคลื่อนด้วยความเร็ว Dash
            if (_currentState == OrcBossState.Attacking && _isDashing)
            {
                _rb.linearVelocity = new Vector2(_facingDirection * _dashAttackSpeed, _rb.linearVelocity.y);
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
            }
        }

        private void SetState(OrcBossState newState)
        {
            _currentState = newState;
            _stateTimer = Time.time;

            switch (newState)
            {
                case OrcBossState.Idle:
                    _isDashing = false;
                    SetHorizontalLock(true);
                    if (_anim != null) _anim.PlayIdle();
                    break;

                case OrcBossState.Chase:
                    _isDashing = false;
                    SetHorizontalLock(false);
                    if (_anim != null) _anim.PlayWalk();
                    break;

                case OrcBossState.TelegraphCharge:
                    // มีไว้รองรับ Backward compatibility ถ้าต้องการ
                    _isDashing = false;
                    SetHorizontalLock(true);
                    if (_anim != null) _anim.StartTelegraphFlash();
                    break;

                case OrcBossState.Attacking:
                    _isDashing = false;
                    SetHorizontalLock(true); // ล็อคตำแหน่งระหว่างชาร์จเตือน
                    ExecuteAttackAnimation(_pendingAttack);
                    break;

                case OrcBossState.Recovery:
                    _isDashing = false;
                    SetHorizontalLock(true);
                    if (_anim != null) _anim.PlayIdle();
                    StartCoroutine(RecoveryRoutine());
                    break;

                case OrcBossState.Defeated:
                    _isDashing = false;
                    StopAllCoroutines();
                    CloseAllHitboxes();

                    // ล็อคตำแหน่งให้อยู่กับที่ และเปลี่ยนเป็น Kinematic/ปิด Simulated ทันที
                    // เพื่อไม่ให้ Gravity ดึงร่างบอสตกทะลุพื้น Tilemap
                    if (_rb != null)
                    {
                        _rb.linearVelocity = Vector2.zero;
                        _rb.constraints = RigidbodyConstraints2D.FreezeAll;
                        _rb.bodyType = RigidbodyType2D.Kinematic;
                        _rb.simulated = false;
                    }

                    // ปิด Collider ทางกายภาพ เพื่อให้ Player เดินผ่านร่างที่นอนอยู่ได้โดยไม่ติด
                    Collider2D[] colliders = GetComponents<Collider2D>();
                    foreach (var col in colliders)
                    {
                        col.enabled = false;
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

            // เมื่อเข้าสู่ระยะประชิด ให้เริ่มโจมตี
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
            List<OrcAttackType> candidateAttacks = new List<OrcAttackType>();

            if (distance <= _meleeRange)
            {
                // ระยะใกล้: สุ่มได้ทั้ง 3 ท่า (Dash, Front, Spin)
                candidateAttacks.Add(OrcAttackType.FrontCleave);
                candidateAttacks.Add(OrcAttackType.SpinAttack);
                candidateAttacks.Add(OrcAttackType.DashAttack);
            }
            else
            {
                // ระยะไกล: ใช้ท่า Dash Attack หรือวิ่งเข้าหาต่อ
                if (UnityEngine.Random.value < 0.65f)
                {
                    candidateAttacks.Add(OrcAttackType.DashAttack);
                }
                else
                {
                    SetState(OrcBossState.Chase);
                    return;
                }
            }

            // GDD: "จะไม่สุ่มท่าเดิมซ้ำกันเกิน 2 ครั้งติดกัน"
            if (_consecutiveAttackCount >= 2 && candidateAttacks.Contains(_lastAttack))
            {
                candidateAttacks.Remove(_lastAttack);
            }

            // ถ้า Candidate ว่าง (กรณีพิเศษ) ให้วิ่งหา
            if (candidateAttacks.Count == 0)
            {
                SetState(OrcBossState.Chase);
                return;
            }

            // สุ่มท่าที่เลือก
            OrcAttackType chosen = candidateAttacks[UnityEngine.Random.Range(0, candidateAttacks.Count)];

            // บันทึกสถิติเพื่อป้องกันท่าซ้ำ
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

            // สั่งเล่น Animation ทันที (Telegraph Charge + Hitbox จะถูกควบคุมผ่าน Animation Events)
            SetState(OrcBossState.Attacking);
        }

        private void ExecuteAttackAnimation(OrcAttackType attackType)
        {
            if (_anim != null)
            {
                _anim.StartTelegraphFlash(); // เริ่มกะพริบชาร์จเตือนทันที

                switch (attackType)
                {
                    case OrcAttackType.DashAttack:
                        _anim.PlayAttack01_Dash();
                        break;
                    case OrcAttackType.FrontCleave:
                        _anim.PlayAttack02_Front();
                        break;
                    case OrcAttackType.SpinAttack:
                        _anim.PlayAttack03_Spin();
                        break;
                }
            }

            // Safety Timeout: กันบอสค้างกรณีที่ Animation Clip ยังไม่ได้ใส่ OnAttackComplete
            if (_safetyTimeoutCoroutine != null) StopCoroutine(_safetyTimeoutCoroutine);
            _safetyTimeoutCoroutine = StartCoroutine(AttackSafetyTimeoutRoutine(3.5f));
        }

        private IEnumerator AttackSafetyTimeoutRoutine(float maxDuration)
        {
            yield return new WaitForSeconds(maxDuration);
            if (_currentState == OrcBossState.Attacking)
            {
                UnityEngine.Debug.LogWarning("[OrcRiderBossController] Attack animation timed out! Did you forget to add 'OnAttackComplete' event?");
                AnimEvent_OnAttackComplete();
            }
        }

        // ─── Methods called by OrcRiderAnimationEventReceiver (Animation Events) ───────

        /// <summary>
        /// Animation Event: สิ้นสุดช่วงชาร์จเตือน หยุดกะพริบ (และเริ่มพุ่งตัวสำหรับท่า Dash)
        /// </summary>
        public void AnimEvent_OnTelegraphEnd()
        {
            if (_currentState != OrcBossState.Attacking) return;
            if (_anim != null) _anim.StopTelegraph();

            // สำหรับท่า Dash Attack: ปลดล็อค X และเริ่มพุ่งตัวไปข้างหน้า
            if (_pendingAttack == OrcAttackType.DashAttack)
            {
                SetHorizontalLock(false);
                _isDashing = true;
                _rb.linearVelocity = new Vector2(_facingDirection * _dashAttackSpeed, _rb.linearVelocity.y);
            }
            else if (_pendingAttack == OrcAttackType.FrontCleave)
            {
                SetHorizontalLock(false);
                // ขยับก้าวไปข้างหน้านิดหน่อยตอนฟัน
                _rb.linearVelocity = new Vector2(_facingDirection * 2f, _rb.linearVelocity.y);
            }
        }

        /// <summary>
        /// Animation Event: เปิดใช้งาน Hitbox ในช่วงที่ฟันโดน
        /// </summary>
        public void AnimEvent_OpenHitbox()
        {
            if (_currentState != OrcBossState.Attacking) return;

            if (_pendingAttack == OrcAttackType.SpinAttack)
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
        /// Animation Event: ปิดใช้งาน Hitbox เมื่อฟันเสร็จ
        /// </summary>
        public void AnimEvent_CloseHitbox()
        {
            CloseAllHitboxes();
            _isDashing = false;
            SetHorizontalLock(true);
        }

        /// <summary>
        /// Animation Event: จบท่าโจมตี เข้าสู่จังหวะ Recovery
        /// </summary>
        public void AnimEvent_OnAttackComplete()
        {
            if (_currentState != OrcBossState.Attacking) return;

            if (_safetyTimeoutCoroutine != null)
            {
                StopCoroutine(_safetyTimeoutCoroutine);
                _safetyTimeoutCoroutine = null;
            }

            _isDashing = false;
            SetHorizontalLock(true);
            CloseAllHitboxes();
            if (_anim != null) _anim.StopTelegraph();

            SetState(OrcBossState.Recovery);
        }

        private IEnumerator RecoveryRoutine()
        {
            yield return new WaitForSeconds(_attackRecoveryTime);

            if (_currentState == OrcBossState.Recovery)
            {
                SetState(OrcBossState.Idle);
            }
        }

        /// <summary>
        /// ปิดเฉพาะ Attack Hitbox (ActiveAttack) เท่านั้น
        /// ห้ามปิด BodyContact เพราะมันต้องทำ contact damage ตลอดเวลา
        /// </summary>
        private void CloseAllHitboxes()
        {
            if (_frontAttackHitbox != null) _frontAttackHitbox.CloseHitbox();
            if (_spinAttackHitbox != null) _spinAttackHitbox.CloseHitbox();
        }

        private void HandleDefeated()
        {
            SetState(OrcBossState.Defeated);
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

            // รอให้อนิเมชั่นตายเล่นจบตามระยะเวลาที่กำหนด (Fallback กรณีไม่ได้ใส่ Animation Event)
            yield return new WaitForSeconds(_deathAnimationDuration);

            TriggerPhaseAdvance();
        }

        /// <summary>
        /// Animation Event: เล่นอนิเมชั่นตายจบแล้ว สั่งเปลี่ยนเฟสทันที
        /// </summary>
        public void AnimEvent_OnDeathComplete()
        {
            if (_deathTransitionCoroutine != null)
            {
                StopCoroutine(_deathTransitionCoroutine);
                _deathTransitionCoroutine = null;
            }

            TriggerPhaseAdvance();
        }

        private void TriggerPhaseAdvance()
        {
            UnityEngine.Debug.Log("[OrcRiderBossController] Phase 1 Death completed! Transitioning to Phase 2...");

            if (BossManager.Instance != null)
            {
                BossManager.Instance.AdvanceToNextPhase();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = transform.position;

            // วาดเส้นและวงกลม Melee Range
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(center, _meleeRange);

            // วาดขอบเขตระยะประชิดแนวนอน (ซ้าย-ขวา)
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.9f);
            Gizmos.DrawLine(center + Vector3.left * _meleeRange + Vector3.up * 2f, center + Vector3.left * _meleeRange + Vector3.down * 0.5f);
            Gizmos.DrawLine(center + Vector3.right * _meleeRange + Vector3.up * 2f, center + Vector3.right * _meleeRange + Vector3.down * 0.5f);
            Gizmos.DrawLine(center + Vector3.left * _meleeRange, center + Vector3.right * _meleeRange);

            #if UNITY_EDITOR
            // วาดพื้นที่แรเงาสีแดงโปร่งแสงใน Scene view
            UnityEditor.Handles.color = new Color(1f, 0f, 0f, 0.08f);
            UnityEditor.Handles.DrawSolidDisc(center, Vector3.forward, _meleeRange);

            UnityEditor.Handles.color = new Color(1f, 0.2f, 0.2f, 0.8f);
            UnityEditor.Handles.DrawWireDisc(center, Vector3.forward, _meleeRange);

            GUIStyle labelStyle = new GUIStyle
            {
                normal = { textColor = Color.yellow },
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            UnityEditor.Handles.Label(center + Vector3.up * (_meleeRange + 0.4f), $"Melee Range: {_meleeRange:F1}m", labelStyle);
            #endif
        }
    }
}
