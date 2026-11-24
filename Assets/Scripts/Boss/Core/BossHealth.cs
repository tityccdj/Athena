using System;
using System.Collections;
using Athena.Core;
using Athena.Core.Interfaces;
using UnityEngine;

namespace Athena.Boss.Core
{
    /// <summary>
    /// จัดการพลังชีวิตของบอสในแต่ละ Phase
    /// Implement IDamageable เพื่อรับดาเมจจาก Player
    /// ตาม GDD:
    /// - เลือด Phase 1 = 60% - 100% (120 HP จาก 300)
    /// - เมื่อเลือดหมดหลอดเฟส จะติด I-Frame เพื่อเตรียมเปลี่ยน Phase
    /// - เมื่อ Player ตาย เลือดจะรีเซ็ตกลับเต็ม MAX ของ Phase นั้นๆ
    /// </summary>
    public class BossHealth : MonoBehaviour, IDamageable
    {
        [Header("Phase HP Settings (GDD: Phase 1 = 120 HP)")]
        [SerializeField] private int _maxHealth = 120;

        [Header("Unity Events (สำหรับเชื่อมต่อ ParticleManager หรือเสียง)")]
        [SerializeField] private UnityEngine.Events.UnityEvent _onDamagedEvent;
        [SerializeField] private UnityEngine.Events.UnityEvent _onDefeatedEvent;

        [Header("Visual Feedback")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Color _flashColor = Color.red;
        [SerializeField] private float _flashDuration = 0.1f;

        private int _currentHealth = -1;
        private bool _isInvulnerable = false;
        private Color _originalColor;
        private Coroutine _flashCoroutine;

        public int CurrentHealth => (_currentHealth >= 0) ? _currentHealth : _maxHealth;
        public int MaxHealth => _maxHealth;
        public bool IsInvulnerable => _isInvulnerable;

        public event Action<int, int> OnHealthChanged; // (current, max)
        public event Action<int> OnDamaged;
        public event Action OnDefeated;

        private void Awake()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (_spriteRenderer != null)
            {
                _originalColor = _spriteRenderer.color;
            }

            _currentHealth = _maxHealth;
        }

        private void Start()
        {
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (_currentHealth < 0) _currentHealth = _maxHealth;
            if (_isInvulnerable || _currentHealth <= 0) return;

            int damageToTake = Mathf.Max(1, info.Amount);
            _currentHealth = Mathf.Max(0, _currentHealth - damageToTake);

            OnDamaged?.Invoke(damageToTake);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            _onDamagedEvent?.Invoke();

            TriggerHitFlash();

            UnityEngine.Debug.Log($"[BossHealth] {gameObject.name} took {damageToTake} DMG! HP: {_currentHealth}/{_maxHealth}");

            if (_currentHealth <= 0)
            {
                _isInvulnerable = true; // GDD: เมื่อ HP หมดหลอดเฟสนี้ บอสจะติด I-Frame
                OnDefeated?.Invoke();
                _onDefeatedEvent?.Invoke();
                UnityEngine.Debug.Log($"[BossHealth] {gameObject.name} DEFEATED!");
            }
        }

        public void SetInvulnerable(bool invulnerable)
        {
            _isInvulnerable = invulnerable;
        }

        /// <summary>
        /// รีเซ็ตเลือดกลับเต็ม MAX ของ Phase นี้ (ใช้เมื่อ Player ตายแล้วเริ่มใหม่)
        /// </summary>
        public void ResetHealth()
        {
            _currentHealth = _maxHealth;
            _isInvulnerable = false;

            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = _originalColor;
            }

            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            UnityEngine.Debug.Log($"[BossHealth] {gameObject.name} Health Reset to {_maxHealth}!");
        }

        private void TriggerHitFlash()
        {
            if (_spriteRenderer == null) return;
            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(HitFlashRoutine());
        }

        private IEnumerator HitFlashRoutine()
        {
            _spriteRenderer.color = _flashColor;
            yield return new WaitForSeconds(_flashDuration);
            _spriteRenderer.color = _originalColor;
            _flashCoroutine = null;
        }
    }
}
