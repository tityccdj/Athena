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
        [SerializeField] private float _fallGravityMultiplier = 2.5f;
        [SerializeField] private float _jumpCutMultiplier = 0.5f;

        [Header("Wall Slide Settings")]
        [SerializeField] private float _wallSlideSpeed = 2f;

        [Header("Visual")]
        [SerializeField] private Transform _visualTransform;

        private Rigidbody2D _rb;
        private CollisionDetector2D _collisionDetector;
        private IPlayerInput _input;

        private float _defaultGravityScale;
        private bool _canJump;
        private bool _isWallSliding;
        private bool _isMovementLocked;

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
            _defaultGravityScale = _rb.gravityScale;
            if (_visualTransform == null) _visualTransform = transform;
        }

        private void Update()
        {
            if (_input == null || _collisionDetector == null) return;

            UpdateFacingDirection();
            CheckJump();
        }

        private void FixedUpdate()
        {
            if (_input == null || _collisionDetector == null) return;

            if (_isMovementLocked) return;

            HandleWallSlide();
            HandleHorizontalMovement();
            ApplyBetterJumpGravity();
        }

        private void UpdateFacingDirection()
        {
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
            if (_collisionDetector.IsGrounded)
            {
                _canJump = true;
            }

            // GDD: Single Jump
            if (_canJump && _input.ConsumeJumpBuffer())
            {
                ExecuteJump();
            }

            // Variable jump height: releasing jump key cuts vertical momentum
            if (!_input.IsJumpHeld && _rb.linearVelocity.y > 0)
            {
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.y * _jumpCutMultiplier);
            }
        }

        private void ExecuteJump()
        {
            _canJump = false;
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _jumpForce);
        }

        private void HandleWallSlide()
        {
            // Wall slide condition: In air, touching wall, falling down
            if (!_collisionDetector.IsGrounded && _collisionDetector.IsTouchingWall && _rb.linearVelocity.y < 0)
            {
                // Must be pressing towards the wall or falling against it
                int wallDir = _collisionDetector.WallDirection;
                if (_input.MoveInput.x == 0 || Mathf.Sign(_input.MoveInput.x) == wallDir)
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
            float targetSpeed = _input.MoveInput.x * _moveSpeed;
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
            }
            else
            {
                _rb.gravityScale = _defaultGravityScale;
            }
        }

        public void SetMovementLocked(bool locked)
        {
            _isMovementLocked = locked;
            if (locked)
            {
                _rb.gravityScale = _defaultGravityScale;
            }
        }

        public void ApplyDirectVelocity(Vector2 velocity)
        {
            _rb.linearVelocity = velocity;
        }

        public void StopVelocity()
        {
            _rb.linearVelocity = Vector2.zero;
        }
    }
}

