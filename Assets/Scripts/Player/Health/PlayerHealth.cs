using System;
using System.Collections;
using Athena.CameraSystem;
using Athena.Core;
using Athena.Core.Interfaces;
using Athena.Player.Input;
using Athena.Player.Movement;
using UnityEngine;

namespace Athena.Player.Health
{
    public class PlayerHealth : MonoBehaviour, IDamageable, IKnockbackable, IHealable
    {
        [Header("Health Settings (GDD: Max HP = 5)")]
        [SerializeField] private int _maxHealth = 5;
        [SerializeField] private float _damageIFrameDuration = 1.0f;
        [SerializeField] private float _knockbackStunDuration = 0.2f;

        [Header("Heal Settings (GDD: +2 HP, Cooldown 30s)")]
        [SerializeField] private int _healAmount = 2;
        [SerializeField] private float _healCooldown = 30.0f;

        [Header("Unity Events (สำหรับเชื่อมต่อ ParticleManager หรือเสียง)")]
        [SerializeField] private UnityEngine.Events.UnityEvent _onDamagedEvent;
        [SerializeField] private UnityEngine.Events.UnityEvent _onHealedEvent;
        [SerializeField] private UnityEngine.Events.UnityEvent _onDeathEvent;

        [Header("Camera Shake on Damage")]
        [Tooltip("เปิดใช้งาน Camera Shake เมื่อ Player ได้รับความเสียหายหรือไม่")]
        [SerializeField] private bool _enableCameraShake = true;
        [Tooltip("ระยะเวลาการสั่นของกล้องเมื่อโดนดาเมจ (วินาที)")]
        [SerializeField] private float _damageShakeDuration = 0.2f;
        [Tooltip("ความแรงการสั่นของกล้องเมื่อโดนดาเมจ")]
        [SerializeField] private float _damageShakeIntensity = 0.35f;

        [Header("Visual Feedback")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private float _flashInterval = 0.1f;

        private int _currentHealth;
        private bool _isInvulnerable;
        private float _lastHealTime = -999f;
        private Coroutine _iFrameCoroutine;

        private Rigidbody2D _rb;
        private PlayerMovement _movement;
        private IPlayerInput _input;

        public int CurrentHealth => _currentHealth;
        public int MaxHealth => _maxHealth;
        public bool IsInvulnerable => _isInvulnerable;
        public float HealCooldownRemaining => Mathf.Max(0f, (_lastHealTime + _healCooldown) - Time.time);
        public float HealCooldownMax => _healCooldown;

        public event Action<int, int> OnHealthChanged; // (current, max)
        public event Action<int> OnDamaged;
        public event Action<int> OnHealed;
        public event Action OnDeath;

        public void Initialize(IPlayerInput input, PlayerMovement movement, Rigidbody2D rb)
        {
            _input = input;
            _movement = movement;
            _rb = rb;
        }

        private void Awake()
        {
            _currentHealth = _maxHealth;
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_spriteRenderer == null) _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void Start()
        {
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        private void Update()
        {
            if (_input != null && _input.HasBufferedHeal)
            {
                if (CanHeal)
                {
                    _input.ConsumeHealBuffer();
                    TryHeal();
                }
            }
        }

        public bool CanHeal => Time.time >= _lastHealTime + _healCooldown && _currentHealth < _maxHealth && _currentHealth > 0;

        public void TryHeal()
        {
            if (!CanHeal) return;

            _lastHealTime = Time.time;
            Heal(_healAmount);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (_isInvulnerable || _currentHealth <= 0) return;

            int damageToTake = Mathf.Max(1, info.Amount);
            _currentHealth = Mathf.Max(0, _currentHealth - damageToTake);

            OnDamaged?.Invoke(damageToTake);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            _onDamagedEvent?.Invoke();

            // Camera Shake เมื่อได้รับดาเมจ
            if (_enableCameraShake && CameraController2D.Instance != null)
            {
                CameraController2D.Instance.TriggerShake(_damageShakeDuration, _damageShakeIntensity);
            }

            // Apply Knockback
            if (info.KnockbackForce > 0f)
            {
                ApplyKnockback(info.KnockbackDirection, info.KnockbackForce);
            }

            if (_currentHealth <= 0)
            {
                Die();
                return;
            }

            // GDD: Hit Reaction I-Frame 1 second with flashing
            TriggerDamageIFrame();
        }

        public void ApplyKnockback(Vector2 direction, float force)
        {
            if (_rb == null) return;
            StartCoroutine(PerformKnockback(direction.normalized, force));
        }

        private IEnumerator PerformKnockback(Vector2 direction, float force)
        {
            if (_movement != null) _movement.SetMovementLocked(true);
            _rb.linearVelocity = direction * force;

            yield return new WaitForSeconds(_knockbackStunDuration);

            if (_movement != null) _movement.SetMovementLocked(false);
        }

        public void Heal(int amount)
        {
            if (_currentHealth <= 0) return;

            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
            OnHealed?.Invoke(amount);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            _onHealedEvent?.Invoke();
        }

        public void SetInvulnerable(bool invulnerable)
        {
            _isInvulnerable = invulnerable;
        }

        private void TriggerDamageIFrame()
        {
            if (_iFrameCoroutine != null) StopCoroutine(_iFrameCoroutine);
            _iFrameCoroutine = StartCoroutine(DamageIFrameRoutine());
        }

        private IEnumerator DamageIFrameRoutine()
        {
            _isInvulnerable = true;
            float timer = 0f;

            while (timer < _damageIFrameDuration)
            {
                if (_spriteRenderer != null)
                {
                    _spriteRenderer.enabled = !_spriteRenderer.enabled;
                }
                yield return new WaitForSeconds(_flashInterval);
                timer += _flashInterval;
            }

            if (_spriteRenderer != null)
            {
                _spriteRenderer.enabled = true;
            }

            _isInvulnerable = false;
            _iFrameCoroutine = null;
        }

        private void Die()
        {
            if (_spriteRenderer != null) _spriteRenderer.enabled = true;
            _isInvulnerable = true;
            OnDeath?.Invoke();
            _onDeathEvent?.Invoke();
        }

        public void ResetHealth()
        {
            _currentHealth = _maxHealth;
            _isInvulnerable = false;
            _lastHealTime = -999f;
            if (_spriteRenderer != null) _spriteRenderer.enabled = true;
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }
    }
}

