using System;
using System.Collections;
using Athena.Player.Health;
using Athena.Player.Input;
using UnityEngine;

namespace Athena.Player.Movement
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerDash : MonoBehaviour
    {
        [Header("Dash Settings")]
        [SerializeField] private float _dashSpeed = 22f;
        [SerializeField] private float _dashDuration = 0.2f;
        [SerializeField] private float _dashCooldown = 0.8f;

        [Header("Unity Events (สำหรับเชื่อมต่อ ParticleManager หรือเสียง)")]
        [SerializeField] private UnityEngine.Events.UnityEvent _onDashStartedEvent;
        [SerializeField] private UnityEngine.Events.UnityEvent _onDashEndedEvent;

        [Header("Phase Through Enemies / Boss")]
        [SerializeField] private LayerMask _enemyLayer;

        private Rigidbody2D _rb;
        private Collider2D _playerCollider;
        private IPlayerInput _input;
        private PlayerMovement _movement;
        private PlayerHealth _health;

        private bool _isDashing;
        private float _lastDashTime = -999f;
        private int _dashDirection = 1;

        public bool IsDashing => _isDashing;
        public int DashDirection => _dashDirection;
        public float CooldownRemaining => Mathf.Max(0f, (_lastDashTime + _dashCooldown) - Time.time);

        public event Action<int> OnDashStarted; // Direction
        public event Action OnDashEnded;

        public void Initialize(IPlayerInput input, PlayerMovement movement, PlayerHealth health)
        {
            _input = input;
            _movement = movement;
            _health = health;
        }

        public void ResetDash()
        {
            StopAllCoroutines();
            _isDashing = false;
            _lastDashTime = -999f;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _playerCollider = GetComponent<Collider2D>();
        }

        private void Update()
        {
            if (_input == null || _movement == null) return;

            // ห้าม Dash ถ้าตายแล้ว
            if (_health != null && _health.CurrentHealth <= 0) return;

            if (!_isDashing && Time.time >= _lastDashTime + _dashCooldown)
            {
                if (_input.ConsumeDashBuffer())
                {
                    StartCoroutine(PerformDash());
                }
            }
        }

        private IEnumerator PerformDash()
        {
            _isDashing = true;
            _lastDashTime = Time.time;

            // Determine direction: horizontal input if pressed, otherwise current facing direction
            float moveX = _input.MoveInput.x;
            _dashDirection = Mathf.Abs(moveX) > 0.05f ? (moveX > 0 ? 1 : -1) : _movement.FacingDirection;

            // Notify dash started
            OnDashStarted?.Invoke(_dashDirection);
            _onDashStartedEvent?.Invoke();

            // GDD: I-Frame (อมตะ) during Dash
            if (_health != null)
            {
                _health.SetInvulnerable(true);
            }

            // GDD: สามารถพุ่งทะลุตัว Boss ได้ (Ignore collision with Enemy Layer)
            SetEnemyCollisionIgnored(true);

            // Lock regular movement and freeze gravity for horizontal dash
            _movement.SetMovementLocked(true);
            float originalGravity = _rb.gravityScale;
            _rb.gravityScale = 0f;
            _rb.linearVelocity = new Vector2(_dashDirection * _dashSpeed, 0f);

            yield return new WaitForSeconds(_dashDuration);

            // Restore state
            _rb.gravityScale = originalGravity;
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            _movement.SetMovementLocked(false);

            SetEnemyCollisionIgnored(false);

            if (_health != null)
            {
                _health.SetInvulnerable(false);
            }

            _isDashing = false;
            OnDashEnded?.Invoke();
            _onDashEndedEvent?.Invoke();
        }

        private void SetEnemyCollisionIgnored(bool ignore)
        {
            if (_playerCollider == null) return;

            // Find all colliders on enemy layer overlapping or ignore layer collision
            int playerLayer = gameObject.layer;
            for (int i = 0; i < 32; i++)
            {
                if ((_enemyLayer.value & (1 << i)) != 0)
                {
                    Physics2D.IgnoreLayerCollision(playerLayer, i, ignore);
                }
            }
        }

        private void OnDisable()
        {
            if (_isDashing)
            {
                SetEnemyCollisionIgnored(false);
                if (_health != null) _health.SetInvulnerable(false);
                _isDashing = false;
            }
        }
    }
}

