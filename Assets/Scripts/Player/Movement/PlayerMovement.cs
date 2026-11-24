using Athena.Player.Input;
using UnityEngine;

namespace Athena.Player.Movement
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float _moveSpeed = 8f;
        [SerializeField] private float _acceleration = 50f;
        [SerializeField] private float _deceleration = 60f;

        [Header("Jump Settings")]
        [SerializeField] private float _jumpForce = 14f;
        [SerializeField] private float _baseGravityScale = 1.0f;
        [SerializeField] private float _fallGravityMultiplier = 1.5f;
        [SerializeField] private float _jumpCutMultiplier = 0.5f;
        [SerializeField] private float _maxFallSpeed = 22f; // จำกัดความเร็วตกสูงสุด ป้องกันการตกทะลุพื้น (Tunneling)

        [Header("Wall Slide Settings")]
        [SerializeField] private float _wallSlideSpeed = 2f;

        [Header("Wall Jump Settings")]
        [SerializeField] private float _wallJumpForceY = 13f;
        [SerializeField] private float _wallJumpForceX = 7f;
        [SerializeField] private float _wallJumpInputLockDuration = 0.2f; // ล็อค horizontal input ชั่วคราวหลัง wall jump

        [Header("Unity Events (สำหรับเชื่อมต่อ ParticleManager เช่น ฝุ่นตอนกระโดด/ลงพื้น)")]
        [SerializeField] private UnityEngine.Events.UnityEvent _onJumpEvent;
        [SerializeField] private UnityEngine.Events.UnityEvent _onLandEvent;

        [Header("Visual")]
        [SerializeField] private Transform _visualTransform;

        private Rigidbody2D _rb;
        private CollisionDetector2D _collisionDetector;
        private IPlayerInput _input;

        private float _defaultGravityScale;
        private bool _canJump;
        private bool _hasReleasedJumpKey = true;
        private float _lastJumpTime = -999f;
        private bool _isWallSliding;
        private bool _isMovementLocked;
        private bool _isDead;
        private bool _wasGroundedLastFrame;
        private float _wallJumpInputLockEndTime = -999f; // ล็อค horizontal input หลัง wall jump

        public int FacingDirection { get; private set; } = 1; // 1 = Right, -1 = Left
        public bool IsWallSliding => _isWallSliding;
        public bool IsGrounded => _collisionDetector != null && _collisionDetector.IsGrounded;

        public void Initialize(IPlayerInput input, CollisionDetector2D collisionDetector)
        {
            _input = input;
            _collisionDetector = collisionDetector;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();

            // ป้องกัน Physics Tunneling (ตกทะลุ Tile เมื่อตกเร็ว)
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            // Apply platformer base gravity if Rigidbody was on default 1.0
            if (_rb.gravityScale <= 1.0f)
            {
                _rb.gravityScale = _baseGravityScale;
            }
            _defaultGravityScale = _rb.gravityScale;

            if (_visualTransform == null) _visualTransform = transform;
        }

        private void Update()
        {
            if (_input == null || _collisionDetector == null) return;
            if (_isDead) return;

            UpdateFacingDirection();
            CheckJump();

            bool isGrounded = _collisionDetector != null && _collisionDetector.IsGrounded;
            if (!_wasGroundedLastFrame && isGrounded && _rb.linearVelocity.y <= 0.1f)
            {
                _onLandEvent?.Invoke();
            }
            _wasGroundedLastFrame = isGrounded;
        }

        private void FixedUpdate()
        {
            if (_input == null || _collisionDetector == null) return;
            if (_isDead) return;

            if (_isMovementLocked) return;

            HandleWallSlide();
            HandleHorizontalMovement();
            ApplyBetterJumpGravity();
        }

        private void UpdateFacingDirection()
        {
            // Don't change facing direction when movement is locked (e.g. during attack)
            if (_isMovementLocked) return;

            float horizontal = _input.MoveInput.x;
            if (Mathf.Abs(horizontal) > 0.05f)
            {
                FacingDirection = horizontal > 0 ? 1 : -1;
                Vector3 scale = _visualTransform.localScale;
                scale.x = Mathf.Abs(scale.x) * FacingDirection;
                _visualTransform.localScale = scale;
            }
        }

        private void CheckJump()
        {
            // Reset jump release state once player releases Space
            if (!_input.IsJumpHeld)
            {
                _hasReleasedJumpKey = true;
            }

            // Don't allow jumping when movement is locked (e.g. during attack)
            if (_isMovementLocked) return;

            // Refresh jump when grounded
            if (_collisionDetector.IsGrounded && Time.time > _lastJumpTime + 0.1f)
            {
                _canJump = true;
            }

            if (!_hasReleasedJumpKey) return;

            // Wall Jump: กระโดดออกจากกำแพงขณะ Wall Slide
            if (_isWallSliding && _input.ConsumeJumpBuffer())
            {
                ExecuteWallJump();
                return;
            }

            // Normal Jump
            if (_canJump && _input.ConsumeJumpBuffer())
            {
                ExecuteJump();
            }

            // Variable jump height: releasing jump key cuts upward momentum
            if (!_input.IsJumpHeld && _rb.linearVelocity.y > 0)
            {
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.y * _jumpCutMultiplier);
            }
        }

        private void ExecuteWallJump()
        {
            int wallDir = _collisionDetector.WallDirection;
            _canJump = false;
            _hasReleasedJumpKey = false;
            _lastJumpTime = Time.time;
            _isWallSliding = false;

            // กระโดดออกจากกำแพง: X ออกด้านตรงข้ามกำแพง, Y ขึ้น
            _rb.linearVelocity = new Vector2(-wallDir * _wallJumpForceX, _wallJumpForceY);

            // ล็อค horizontal input ชั่วคราว กัน player กด input ทวนทิศทาง wall jump ทันที
            _wallJumpInputLockEndTime = Time.time + _wallJumpInputLockDuration;

            // หันหน้าออกจากกำแพง
            FacingDirection = -wallDir;
            Vector3 scale = _visualTransform.localScale;
            scale.x = Mathf.Abs(scale.x) * FacingDirection;
            _visualTransform.localScale = scale;

            _onJumpEvent?.Invoke();
        }

        private void ExecuteJump()
        {
            _canJump = false;
            _hasReleasedJumpKey = false; // Player must release Space before jumping again
            _lastJumpTime = Time.time;
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _jumpForce);
            _onJumpEvent?.Invoke();
        }

        private void HandleWallSlide()
        {
            // Wall slide condition: In air, touching wall, falling down
            if (!_collisionDetector.IsGrounded && _collisionDetector.IsTouchingWall && _rb.linearVelocity.y < 0)
            {
                // Only slide if actively pushing into the wall (A towards left wall, D towards right wall)
                int wallDir = _collisionDetector.WallDirection;
                bool isPushingIntoWall = (wallDir == 1 && _input.MoveInput.x > 0.1f) || 
                                         (wallDir == -1 && _input.MoveInput.x < -0.1f);

                if (isPushingIntoWall)
                {
                    _isWallSliding = true;
                    _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, -_wallSlideSpeed);
                    return;
                }
            }

            _isWallSliding = false;
        }

        private void HandleHorizontalMovement()
        {
            // ล็อค horizontal input ชั่วคราวหลัง wall jump (กัน player กด ทวนทิศทันที)
            bool wallJumpInputLocked = Time.time < _wallJumpInputLockEndTime;
            float inputX = wallJumpInputLocked ? 0f : _input.MoveInput.x;

            float targetSpeed = inputX * _moveSpeed;
            float speedDiff = targetSpeed - _rb.linearVelocity.x;
            float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? _acceleration : _deceleration;
            float movement = speedDiff * accelRate * Time.fixedDeltaTime;

            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x + movement, _rb.linearVelocity.y);
        }

        private void ApplyBetterJumpGravity()
        {
            if (_isWallSliding)
            {
                _rb.gravityScale = 0f;
                return;
            }

            if (_rb.linearVelocity.y < 0)
            {
                _rb.gravityScale = _defaultGravityScale * _fallGravityMultiplier;

                // จำกัดความเร็วตกไม่ให้เกินขีดจำกัด (Terminal Velocity)
                if (_rb.linearVelocity.y < -_maxFallSpeed)
                {
                    _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, -_maxFallSpeed);
                }
            }
            else
            {
                _rb.gravityScale = _defaultGravityScale;
            }
        }

        public void SetMovementLocked(bool locked)
        {
            // ถ้าตายแล้ว ห้ามปลดล็อค (กัน knockback coroutine ปลดล็อคหลังตาย)
            if (_isDead && !locked) return;

            _isMovementLocked = locked;
            if (locked)
            {
                _rb.gravityScale = _defaultGravityScale;
            }
        }

        /// <summary>
        /// เรียกเมื่อ Player ตาย — ล็อคการเคลื่อนที่ถาวรจนกว่าจะ Respawn
        /// </summary>
        public void OnPlayerDied()
        {
            _isDead = true;
            _isMovementLocked = true;
            _rb.linearVelocity = Vector2.zero;
            _rb.gravityScale = _defaultGravityScale;
        }

        /// <summary>
        /// เรียกเมื่อ Respawn — ปลดล็อคการเคลื่อนที่
        /// </summary>
        public void OnPlayerRespawned()
        {
            _isDead = false;
            _isMovementLocked = false;
        }

        public void ApplyDirectVelocity(Vector2 velocity)
        {
            _rb.linearVelocity = velocity;
        }

        public void StopVelocity()
        {
            _rb.linearVelocity = Vector2.zero;
        }

        public void StopHorizontalVelocity()
        {
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
        }
    }
}
