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
        private float _originalGravity = 1f;
        private RigidbodyConstraints2D _originalConstraints;
        private Coroutine _dashCoroutine;

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
            if (_dashCoroutine != null)
            {
                StopCoroutine(_dashCoroutine);
                _dashCoroutine = null;
            }

            if (_isDashing)
            {
                EndDash();
            }

            _isDashing = false;
            _lastDashTime = -999f;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _playerCollider = GetComponent<Collider2D>();
            if (_rb != null)
            {
                _originalConstraints = _rb.constraints;
                _originalGravity = _rb.gravityScale;
            }
        }

        private void FixedUpdate()
        {
            if (_isDashing && _rb != null)
            {
                // บังคับความเร็วในแนวนอนอย่างต่อเนื่อง และล็อคแกน Y ให้เป็น 0 ตลอดช่วง Dash
                // ป้องกันไม่ให้แรงสะท้อนจากรอยต่อ Tilemap หรือ Collider ผืนดินดันตัวละครลอยกระดอนขึ้น
                _rb.linearVelocity = new Vector2(_dashDirection * _dashSpeed, 0f);
            }
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
                    _dashCoroutine = StartCoroutine(PerformDash());
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

            // Lock regular movement
            _movement.SetMovementLocked(true);

            // บันทึกและล็อคค่าฟิสิกส์สำหรับ Dash
            if (_rb != null)
            {
                _originalGravity = _rb.gravityScale;
                _originalConstraints = _rb.constraints;

                _rb.gravityScale = 0f;
                // ล็อคตำแหน่งแกน Y ชั่วคราว กันการกระดอนจากรอยต่อพื้น (Tilemap Seam / Edge Deflection)
                _rb.constraints = _originalConstraints | RigidbodyConstraints2D.FreezePositionY;
                _rb.linearVelocity = new Vector2(_dashDirection * _dashSpeed, 0f);
            }

            yield return new WaitForSeconds(_dashDuration);

            EndDash();
        }

        private void EndDash()
        {
            if (!_isDashing) return;

            // คืนค่าฟิสิกส์ทั้งหมด
            if (_rb != null)
            {
                _rb.constraints = _originalConstraints;
                _rb.gravityScale = _originalGravity;
                // รีเซ็ตความเร็วเป็น 0 ทันที ป้องกันไม่ให้มีแรงสะท้อนแนวตั้ง (Upward velocity) หลงเหลือมากระดอนตัวละคร
                _rb.linearVelocity = Vector2.zero;
            }

            if (_movement != null)
            {
                _movement.SetMovementLocked(false);
            }

            SetEnemyCollisionIgnored(false);

            if (_health != null)
            {
                _health.SetInvulnerable(false);
            }

            _isDashing = false;
            _dashCoroutine = null;

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
            if (_dashCoroutine != null)
            {
                StopCoroutine(_dashCoroutine);
                _dashCoroutine = null;
            }

            if (_isDashing)
            {
                EndDash();
            }
        }
    }
}
