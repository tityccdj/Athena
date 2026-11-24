using System.Collections.Generic;
using Athena.Core;
using Athena.Core.Interfaces;
using UnityEngine;

namespace Athena.Player.Combat
{
    /// <summary>
    /// Hitbox ที่เปิด/ปิดได้จาก Animation Event
    /// Open() → เริ่ม check ทุก FixedUpdate, Close() → หยุด check
    /// </summary>
    public class PlayerAttackHitbox : MonoBehaviour
    {
        [Header("Hitbox Area")]
        [SerializeField] private Vector2 _offset = new Vector2(0.8f, 0f);
        [SerializeField] private float _radius = 0.6f;
        [SerializeField] private LayerMask _targetLayers;

        private bool _isActive;
        private int _damage;
        private int _facingDirection;
        private float _knockbackForce;
        private GameObject _attacker;

        // Track hit entities within single open window to avoid multi-hit
        private readonly HashSet<IDamageable> _damagedThisSwing = new HashSet<IDamageable>();

        public bool IsActive => _isActive;

        /// <summary>
        /// เรียกจาก Animation Event "HitboxOpen" — เปิด hitbox พร้อมตั้งค่า damage
        /// </summary>
        public void Open(int damage, int facingDirection, float knockbackForce, GameObject attacker)
        {
            _damage = damage;
            _facingDirection = facingDirection;
            _knockbackForce = knockbackForce;
            _attacker = attacker;
            _damagedThisSwing.Clear();
            _isActive = true;
        }

        /// <summary>
        /// เรียกจาก Animation Event "HitboxClose" — ปิด hitbox
        /// </summary>
        public void Close()
        {
            _isActive = false;
            _damagedThisSwing.Clear();
        }

        /// <summary>
        /// Alias สำหรับ Close() เพื่อความเข้ากันได้
        /// </summary>
        public void CloseHitbox() => Close();

        private void FixedUpdate()
        {
            if (!_isActive) return;
            CheckOverlap();
        }

        private void CheckOverlap()
        {
            Vector2 origin = (Vector2)transform.position + new Vector2(_offset.x * _facingDirection, _offset.y);
            Collider2D[] hits = Physics2D.OverlapCircleAll(origin, _radius, _targetLayers);

            foreach (var hit in hits)
            {
                if (hit.gameObject == _attacker) continue;

                if (hit.TryGetComponent<IDamageable>(out var damageable))
                {
                    if (!_damagedThisSwing.Contains(damageable) && !damageable.IsInvulnerable)
                    {
                        _damagedThisSwing.Add(damageable);
                        Vector2 knockbackDir = new Vector2(_facingDirection, 0.2f).normalized;
                        DamageInfo info = new DamageInfo(_damage, hit.transform.position, knockbackDir, _knockbackForce, _attacker);
                        damageable.TakeDamage(info);
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Show hitbox in red when active, gray when inactive
            Gizmos.color = _isActive ? Color.red : new Color(1f, 0f, 0f, 0.3f);
            Vector2 facing = Application.isPlaying ? new Vector2(_facingDirection, 0f) : Vector2.right;
            Vector2 origin = (Vector2)transform.position + new Vector2(_offset.x * facing.x, _offset.y);
            Gizmos.DrawWireSphere(origin, _radius);
        }
    }
}
