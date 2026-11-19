using System;
using System.Collections;
using System.Collections.Generic;
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

        private OrcBossState _currentState = OrcBossState.Idle;
        private OrcAttackType _pendingAttack = OrcAttackType.None;
        private OrcAttackType _lastAttack = OrcAttackType.None;
        private int _consecutiveAttackCount = 0;

        private float _stateTimer = 0f;
        private int _facingDirection = 1; // 1 = Right, -1 = Left

        public OrcBossState CurrentState => _currentState;
        public int FacingDirection => _facingDirection;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _health = GetComponent<BossHealth>();
            _anim = GetComponentInChildren<OrcRiderAnimation>();

            if (_visualTransform == null)
            {
                _visualTransform = transform;
            }

            _health.OnDefeated += HandleDefeated;
        }

        private void Start()
        {
            _player = FindFirstObjectByType<PlayerController>();
            SetState(OrcBossState.Idle);
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
            }
        }

        private void SetState(OrcBossState newState)
        {
            _currentState = newState;
            _stateTimer = Time.time;

            switch (newState)
            {
                case OrcBossState.Idle:
                    _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                    if (_anim != null) _anim.PlayIdle();
                    break;

                case OrcBossState.Chase:
                    if (_anim != null) _anim.PlayWalk();
                    break;

                case OrcBossState.TelegraphCharge:
                    _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                    if (_anim != null) _anim.StartTelegraphCharge(_chargeDelay);
                    StartCoroutine(TelegraphChargeRoutine());
                    break;

                case OrcBossState.Attacking:
                    StartCoroutine(ExecuteAttackRoutine(_pendingAttack));
                    break;

                case OrcBossState.Recovery:
                    _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                    if (_anim != null) _anim.PlayIdle();
                    StartCoroutine(RecoveryRoutine());
                    break;

                case OrcBossState.Defeated:
                    _rb.linearVelocity = Vector2.zero;
                    StopAllCoroutines();
                    CloseAllHitboxes();
                    if (_anim != null) _anim.PlayDeath();
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

            // GDD: ทุกท่าจะต้องมี Delay Charge 1 วินาทีก่อนเสมอ (Telegraphing)
            SetState(OrcBossState.TelegraphCharge);
        }

        private IEnumerator TelegraphChargeRoutine()
        {
            yield return new WaitForSeconds(_chargeDelay);

            if (_currentState == OrcBossState.TelegraphCharge)
            {
                SetState(OrcBossState.Attacking);
            }
        }

        private IEnumerator ExecuteAttackRoutine(OrcAttackType attackType)
        {
            int attackDir = _facingDirection;

            switch (attackType)
            {
                case OrcAttackType.DashAttack:
                    // ท่า 1: Dash พุ่งเข้าใส่ด้วยความเร็วสูง
                    if (_anim != null) _anim.PlayAttack01_Dash();
                    if (_frontAttackHitbox != null) _frontAttackHitbox.OpenHitbox();

                    float dashTimer = 0f;
                    float dashDuration = 0.45f;
                    while (dashTimer < dashDuration)
                    {
                        _rb.linearVelocity = new Vector2(attackDir * _dashAttackSpeed, _rb.linearVelocity.y);
                        dashTimer += Time.deltaTime;
                        yield return null;
                    }

                    if (_frontAttackHitbox != null) _frontAttackHitbox.CloseHitbox();
                    _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                    break;

                case OrcAttackType.FrontCleave:
                    // ท่า 2: ฟันกวาดด้านหน้า
                    if (_anim != null) _anim.PlayAttack02_Front();

                    // เลื่อนไปข้างหน้านิดหน่อยตอนฟัน
                    _rb.linearVelocity = new Vector2(attackDir * 2f, _rb.linearVelocity.y);
                    yield return new WaitForSeconds(0.15f);

                    if (_frontAttackHitbox != null) _frontAttackHitbox.OpenHitbox();
                    yield return new WaitForSeconds(0.35f);
                    if (_frontAttackHitbox != null) _frontAttackHitbox.CloseHitbox();

                    _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                    break;

                case OrcAttackType.SpinAttack:
                    // ท่า 3: หมุนฟันรอบตัว
                    if (_anim != null) _anim.PlayAttack03_Spin();

                    yield return new WaitForSeconds(0.15f);
                    if (_spinAttackHitbox != null) _spinAttackHitbox.OpenHitbox();
                    yield return new WaitForSeconds(0.4f);
                    if (_spinAttackHitbox != null) _spinAttackHitbox.CloseHitbox();

                    _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                    break;
            }

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

        private void CloseAllHitboxes()
        {
            if (_frontAttackHitbox != null) _frontAttackHitbox.CloseHitbox();
            if (_spinAttackHitbox != null) _spinAttackHitbox.CloseHitbox();
        }

        private void HandleDefeated()
        {
            SetState(OrcBossState.Defeated);

            // แจ้งเตือน BossManager เพื่อเตรียมเปลี่ยนเฟส
            if (BossManager.Instance != null)
            {
                BossManager.Instance.AdvanceToNextPhase();
            }
        }
    }
}
