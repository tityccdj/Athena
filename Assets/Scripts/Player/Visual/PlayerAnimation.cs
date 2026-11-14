using System.Collections;
using Athena.Player.Combat;
using Athena.Player.Health;
using Athena.Player.Movement;
using UnityEngine;

namespace Athena.Player.Visual
{
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimation : MonoBehaviour
    {
        [Header("Animator State Names (Matches Warrior.controller)")]
        [SerializeField] private string _idleState = "Idle";
        [SerializeField] private string _runState = "Run";
        [SerializeField] private string _jumpState = "jump";
        [SerializeField] private string _fallState = "Fall";
        [SerializeField] private string _wallSlideState = "Wall-Slide";
        [SerializeField] private string _dashState = "Dash";
        [SerializeField] private string _dashAttackState = "Dash-Attack";
        [SerializeField] private string _attackState = "Attack";
        [SerializeField] private string _attack1State = "Attack1";
        [SerializeField] private string _attack2State = "Attack2";
        [SerializeField] private string _hurtState = "Hurt";
        [SerializeField] private string _deathState = "Death";

        [Header("Animation Durations")]
        [SerializeField] private float _attack1Duration = 0.35f;
        [SerializeField] private float _attack2Duration = 0.4f;
        [SerializeField] private float _dashAttackDuration = 0.35f;
        [SerializeField] private float _hurtDuration = 0.25f;
        [SerializeField] private float _crossFadeDuration = 0.03f;

        private Animator _animator;
        private Rigidbody2D _rb;
        private PlayerMovement _movement;
        private PlayerDash _dash;
        private PlayerCombat _combat;
        private PlayerHealth _health;

        private int _idleHash;
        private int _runHash;
        private int _jumpHash;
        private int _fallHash;
        private int _wallSlideHash;
        private int _dashHash;
        private int _dashAttackHash;
        private int _attackHash;
        private int _attack1Hash;
        private int _attack2Hash;
        private int _hurtHash;
        private int _deathHash;

        private int _currentStateHash;
        private float _actionLockEndTime;
        private bool _isDead;
        private bool _isActionLocked;

        public void Initialize(PlayerMovement movement, PlayerDash dash, PlayerCombat combat, PlayerHealth health, Rigidbody2D rb)
        {
            _movement = movement;
            _dash = dash;
            _combat = combat;
            _health = health;
            _rb = rb;

            SubscribeEvents();
        }

        private void Awake()
        {
            EnsureAnimator();
            if (_rb == null) _rb = GetComponentInParent<Rigidbody2D>();

            InitializeHashes();
        }

        private void EnsureAnimator()
        {
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
                if (_animator == null) _animator = GetComponentInChildren<Animator>();
            }
        }

        private void InitializeHashes()
        {
            _idleHash = Animator.StringToHash(_idleState);
            _runHash = Animator.StringToHash(_runState);
            _jumpHash = Animator.StringToHash(_jumpState);
            _fallHash = Animator.StringToHash(_fallState);
            _wallSlideHash = Animator.StringToHash(_wallSlideState);
            _dashHash = Animator.StringToHash(_dashState);
            _dashAttackHash = Animator.StringToHash(_dashAttackState);
            _attackHash = Animator.StringToHash(_attackState);
            _attack1Hash = Animator.StringToHash(_attack1State);
            _attack2Hash = Animator.StringToHash(_attack2State);
            _hurtHash = Animator.StringToHash(_hurtState);
            _deathHash = Animator.StringToHash(_deathState);
        }

        private void SubscribeEvents()
        {
            if (_combat != null)
            {
                _combat.OnMeleeAttackStarted += HandleMeleeAttack;
                _combat.OnDashAttackTriggered += HandleDashAttack;
            }

            if (_health != null)
            {
                _health.OnDamaged += HandleDamaged;
                _health.OnDeath += HandleDeath;
            }
        }

        private void OnDestroy()
        {
            if (_combat != null)
            {
                _combat.OnMeleeAttackStarted -= HandleMeleeAttack;
                _combat.OnDashAttackTriggered -= HandleDashAttack;
            }

            if (_health != null)
            {
                _health.OnDamaged -= HandleDamaged;
                _health.OnDeath -= HandleDeath;
            }
        }

        private void Update()
        {
            if (_animator == null || _movement == null || _isDead) return;

            // Check if one-shot animation lock expired
            if (_isActionLocked && Time.time >= _actionLockEndTime)
            {
                _isActionLocked = false;
            }

            // Determine appropriate state
            int targetState = DetermineTargetState();

            if (targetState != _currentStateHash)
            {
                PlayState(targetState);
            }
        }

        private int DetermineTargetState()
        {
            // If in one-shot action (Attack/Hurt) and not dashing, let it play
            if (_isActionLocked)
            {
                return _currentStateHash;
            }

            // 1. Dash State
            if (_dash != null && _dash.IsDashing)
            {
                return _dashHash;
            }

            // 2. Wall Slide State
            if (_movement.IsWallSliding)
            {
                return _wallSlideHash;
            }

            // 3. Air States (Jump / Fall)
            if (!_movement.IsGrounded)
            {
                float verticalVelocity = _rb != null ? _rb.linearVelocity.y : 0f;
                return (verticalVelocity > 0.1f) ? _jumpHash : _fallHash;
            }

            // 4. Ground States (Run / Idle)
            float horizontalVelocity = _rb != null ? Mathf.Abs(_rb.linearVelocity.x) : 0f;
            if (horizontalVelocity > 0.2f)
            {
                return _runHash;
            }

            return _idleHash;
        }

        private void PlayState(int stateHash, bool force = false)
        {
            EnsureAnimator();
            if (_animator == null) return;
            if (!force && stateHash == _currentStateHash) return;

            _currentStateHash = stateHash;
            _animator.CrossFade(stateHash, _crossFadeDuration, 0, 0f);
        }

        private void HandleMeleeAttack(int comboStep)
        {
            if (_isDead) return;
            EnsureAnimator();
            if (_animator == null) return;

            _isActionLocked = true;

            if (comboStep == 1)
            {
                _actionLockEndTime = Time.time + _attack1Duration;
                int target = _animator.HasState(0, _attack1Hash) ? _attack1Hash : _attackHash;
                PlayState(target, force: true);
            }
            else // comboStep == 2
            {
                _actionLockEndTime = Time.time + _attack2Duration;
                int target = _animator.HasState(0, _attack2Hash) ? _attack2Hash : _attackHash;
                PlayState(target, force: true);
            }
        }

        private void HandleDashAttack()
        {
            if (_isDead) return;

            _isActionLocked = true;
            _actionLockEndTime = Time.time + _dashAttackDuration;
            PlayState(_dashAttackHash, force: true);
        }

        private void HandleDamaged(int amount)
        {
            if (_isDead) return;

            _isActionLocked = true;
            _actionLockEndTime = Time.time + _hurtDuration;
            PlayState(_hurtHash, force: true);
        }

        private void HandleDeath()
        {
            _isDead = true;
            _isActionLocked = false;
            PlayState(_deathHash, force: true);
        }

        public void ResetAnimation()
        {
            _isDead = false;
            _isActionLocked = false;
            _currentStateHash = 0;
            PlayState(_idleHash, force: true);
        }
    }
}
