using System;
using Athena.Player.Input;
using Athena.Player.Movement;
using UnityEngine;

namespace Athena.Player.Combat
{
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Melee Combo Settings (GDD: 2 Hits, Hit 2 stronger)")]
        [SerializeField] private int _hit1Damage = 1;
        [SerializeField] private int _hit2Damage = 2;
        [SerializeField] private float _attackCooldown = 0.25f;
        [SerializeField] private float _comboEndCooldown = 0.45f;
        [SerializeField] private float _comboResetTime = 0.8f;
        [SerializeField] private float _hit1Knockback = 4f;
        [SerializeField] private float _hit2Knockback = 7f;

        [Header("Dash Attack Settings (GDD: Projectile, 2 Damage)")]
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private Transform _projectileSpawnPoint;

        [Header("Components")]
        [SerializeField] private PlayerAttackHitbox _hitbox;

        private IPlayerInput _input;
        private PlayerMovement _movement;
        private PlayerDash _dash;

        private int _currentComboStep = 0; // 0 = ready for Hit 1, 1 = ready for Hit 2
        private float _lastAttackTime = -999f;
        private bool _hasFiredDashAttackInCurrentDash = false;

        public event Action<int> OnMeleeAttackStarted; // comboStep: 1 or 2
        public event Action OnDashAttackTriggered;

        public void Initialize(IPlayerInput input, PlayerMovement movement, PlayerDash dash)
        {
            _input = input;
            _movement = movement;
            _dash = dash;

            if (_dash != null)
            {
                _dash.OnDashStarted += HandleDashStarted;
            }
        }

        private void Awake()
        {
            if (_hitbox == null) _hitbox = GetComponentInChildren<PlayerAttackHitbox>();
            if (_projectileSpawnPoint == null) _projectileSpawnPoint = transform;
        }

        private void OnDestroy()
        {
            if (_dash != null)
            {
                _dash.OnDashStarted -= HandleDashStarted;
            }
        }

        private void Update()
        {
            if (_input == null || _movement == null) return;

            // Reset combo step if player waited too long
            if (_currentComboStep > 0 && Time.time > _lastAttackTime + _comboResetTime)
            {
                _currentComboStep = 0;
            }

            if (_input.HasBufferedAttack)
            {
                TryAttack();
            }
        }

        private void TryAttack()
        {
            // Case 1: Dash Attack (GDD: Attack during Dash fires projectile)
            if (_dash != null && _dash.IsDashing)
            {
                if (!_hasFiredDashAttackInCurrentDash)
                {
                    _input.ConsumeAttackBuffer();
                    ExecuteDashAttack();
                }
                return;
            }

            // Case 2: Melee Combo (2 steps)
            float requiredCooldown = (_currentComboStep == 1) ? _attackCooldown : _comboEndCooldown;
            if (Time.time >= _lastAttackTime + requiredCooldown)
            {
                _input.ConsumeAttackBuffer();
                ExecuteMeleeAttack();
            }
        }

        private void ExecuteMeleeAttack()
        {
            _lastAttackTime = Time.time;
            _currentComboStep++;

            int damage = (_currentComboStep == 1) ? _hit1Damage : _hit2Damage;
            float knockback = (_currentComboStep == 1) ? _hit1Knockback : _hit2Knockback;

            if (_hitbox != null)
            {
                _hitbox.PerformAttack(damage, _movement.FacingDirection, knockback, gameObject);
            }

            OnMeleeAttackStarted?.Invoke(_currentComboStep);

            // GDD: Combo has 2 hits max, then resets to 0
            if (_currentComboStep >= 2)
            {
                _currentComboStep = 0;
            }
        }

        private void ExecuteDashAttack()
        {
            _hasFiredDashAttackInCurrentDash = true;

            if (_projectilePrefab != null)
            {
                Vector3 spawnPos = _projectileSpawnPoint != null ? _projectileSpawnPoint.position : transform.position;
                GameObject projObj = Instantiate(_projectilePrefab, spawnPos, Quaternion.identity);
                
                if (projObj.TryGetComponent<PlayerProjectile>(out var projectile))
                {
                    Vector2 dir = new Vector2(_movement.FacingDirection, 0f);
                    projectile.Launch(dir, gameObject);
                }
            }

            OnDashAttackTriggered?.Invoke();
        }

        private void HandleDashStarted(int direction)
        {
            _hasFiredDashAttackInCurrentDash = false;
        }
    }
}

