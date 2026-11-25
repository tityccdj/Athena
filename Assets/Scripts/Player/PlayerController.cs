using Athena.Player.Combat;
using Athena.Player.Health;
using Athena.Player.Input;
using Athena.Player.Movement;
using Athena.Player.Visual;
using UnityEngine;

namespace Athena.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(CollisionDetector2D))]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerDash))]
    [RequireComponent(typeof(PlayerCombat))]
    [RequireComponent(typeof(PlayerHealth))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Modular Subsystems (SOLID)")]
        [SerializeField] private PlayerInputReader _inputReader;
        [SerializeField] private CollisionDetector2D _collisionDetector;
        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private PlayerDash _dash;
        [SerializeField] private PlayerCombat _combat;
        [SerializeField] private PlayerHealth _health;

        [Header("Visual & Animation")]
        [SerializeField] private PlayerAnimation _animation;
        [SerializeField] private PlayerAnimationEventReceiver _animEventReceiver;

        public IPlayerInput Input => _inputReader;
        public CollisionDetector2D Collisions => _collisionDetector;
        public PlayerMovement Movement => _movement;
        public PlayerDash Dash => _dash;
        public PlayerCombat Combat => _combat;
        public PlayerHealth Health => _health;
        public PlayerAnimation Animation => _animation;

        private void Awake()
        {
            // Gather references if not assigned in Inspector
            if (_inputReader == null) _inputReader = GetComponent<PlayerInputReader>();
            if (_collisionDetector == null) _collisionDetector = GetComponent<CollisionDetector2D>();
            if (_movement == null) _movement = GetComponent<PlayerMovement>();
            if (_dash == null) _dash = GetComponent<PlayerDash>();
            if (_combat == null) _combat = GetComponent<PlayerCombat>();
            if (_health == null) _health = GetComponent<PlayerHealth>();
            if (_animation == null) _animation = GetComponentInChildren<PlayerAnimation>();
            if (_animEventReceiver == null) _animEventReceiver = GetComponentInChildren<PlayerAnimationEventReceiver>();

            var rb = GetComponent<Rigidbody2D>();

            // Wire up dependencies cleanly
            _movement.Initialize(_inputReader, _collisionDetector);
            _dash.Initialize(_inputReader, _movement, _health);
            _combat.Initialize(_inputReader, _movement, _dash, _health);
            _health.Initialize(_inputReader, _movement, rb);

            if (_animation != null)
            {
                _animation.Initialize(_movement, _dash, _combat, _health, rb);
            }

            // Animation Event Receiver must be initialized after Combat
            if (_animEventReceiver != null)
            {
                _animEventReceiver.Initialize(_combat);
            }

            // Wire death/respawn to movement lock
            _health.OnDeath += _movement.OnPlayerDied;
        }

        private void OnDestroy()
        {
            if (_health != null) _health.OnDeath -= _movement.OnPlayerDied;
        }

        /// <summary>
        /// ชุบชีวิตและย้าย Player ไปยังตำแหน่ง Respawn
        /// รีเซ็ตเลือด, คูลดาวน์ยา, การเคลื่อนที่, อนิเมชัน, และคอมโบ
        /// </summary>
        public void Respawn(Vector3 targetPosition)
        {
            transform.position = targetPosition;

            if (_movement != null)
            {
                _movement.StopVelocity();
                _movement.OnPlayerRespawned();
            }

            if (_health != null)
            {
                _health.ResetHealth();
            }

            if (_combat != null)
            {
                _combat.ResetCombat();
            }

            if (_dash != null)
            {
                _dash.ResetDash();
            }

            if (_animation != null)
            {
                _animation.ResetAnimation();
            }
        }
    }
}


