using System.Collections.Generic;
using Athena.Core;
using Athena.Core.Interfaces;
using UnityEngine;

namespace Athena.Boss.Core
{
    public enum BossHitboxType
    {
        BodyContact, // เดินชนแล้วทำดาเมจ (1 DMG) มี Cooldown ป้องกันโดนรัวทุกเฟรม
        ActiveAttack  // เปิด-ปิดตามจังหวะท่าโจมตี (Animation Event หรือ Code)
    }

    /// <summary>
    /// Hitbox ของบอส (แยก Body Hitbox และ Attack Hitbox ออกจากกันตาม GDD)
    /// ทำ 1 Damage (หรือตามที่กำหนด) ให้กับ Player เมื่อสัมผัสโดน
    /// </summary>
    public class BossDamageHitbox : MonoBehaviour
    {
        [Header("Hitbox Configuration")]
        [SerializeField] private BossHitboxType _hitboxType = BossHitboxType.BodyContact;
        [SerializeField] private int _damage = 1;
        [SerializeField] private float _knockbackForce = 6f;
        [SerializeField] private LayerMask _targetLayer; // Player Layer

        [Header("Body Contact Settings")]
        [SerializeField] private float _contactCooldown = 0.6f;

        private Collider2D _collider;
        private float _lastContactTime = -999f;
        private bool _isActive = false;
        private readonly HashSet<IDamageable> _hitEntitiesThisAttack = new HashSet<IDamageable>();

        public bool IsActive => _isActive;

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();

            if (_hitboxType == BossHitboxType.BodyContact)
            {
                _isActive = true;
            }
            else
            {
                // ActiveAttack เริ่มต้นปิดไว้
                _isActive = false;
                if (_collider != null) _collider.enabled = false;
            }
        }

        /// <summary>
        /// เปิดใช้งาน Hitbox ท่าโจมตี (เรียกจาก Animation Event หรือ Boss Controller)
        /// </summary>
        public void OpenHitbox(int customDamage = -1)
        {
            _isActive = true;
            _hitEntitiesThisAttack.Clear();
            if (customDamage > 0) _damage = customDamage;

            if (_collider != null) _collider.enabled = true;
        }

        /// <summary>
        /// ปิดใช้งาน Hitbox ท่าโจมตี
        /// </summary>
        public void CloseHitbox()
        {
            _isActive = false;
            _hitEntitiesThisAttack.Clear();

            if (_collider != null && _hitboxType == BossHitboxType.ActiveAttack)
            {
                _collider.enabled = false;
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryDealDamage(other.gameObject, other.transform.position);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            TryDealDamage(collision.gameObject, collision.contacts[0].point);
        }

        private void TryDealDamage(GameObject target, Vector2 hitPoint)
        {
            if (!_isActive) return;

            // ตรวจสอบ Layer
            if (_targetLayer != 0 && (_targetLayer.value & (1 << target.layer)) == 0) return;

            // ตรวจสอบว่า target มี IDamageable หรือไม่
            if (!target.TryGetComponent<IDamageable>(out var damageable)) return;
            if (damageable.IsInvulnerable) return;

            // จัดการเงื่อนไขตามประเภท Hitbox
            if (_hitboxType == BossHitboxType.BodyContact)
            {
                if (Time.time < _lastContactTime + _contactCooldown) return;
                _lastContactTime = Time.time;
            }
            else if (_hitboxType == BossHitboxType.ActiveAttack)
            {
                // โจมตี 1 ครั้ง โดน Entity ละ 1 ทีต่อการฟัน 1 รอบ
                if (_hitEntitiesThisAttack.Contains(damageable)) return;
                _hitEntitiesThisAttack.Add(damageable);
            }

            // คำนวณ Knockback ทิศทางผลักออกจากบอส
            Vector2 knockbackDir = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            knockbackDir.y = Mathf.Max(knockbackDir.y, 0.25f); // ผลักให้ลอยขึ้นเล็กน้อย

            DamageInfo info = new DamageInfo(_damage, hitPoint, knockbackDir, _knockbackForce, gameObject);
            damageable.TakeDamage(info);

            UnityEngine.Debug.Log($"[BossDamageHitbox] Hit {target.name} for {_damage} DMG! (Type: {_hitboxType})");
        }

        private void OnDrawGizmosSelected()
        {
            Collider2D col = _collider != null ? _collider : GetComponent<Collider2D>();
            if (col == null) return;

            // สีแดง = ActiveAttack, สีส้ม = BodyContact
            Gizmos.color = (_hitboxType == BossHitboxType.ActiveAttack)
                ? new Color(1f, 0f, 0f, 0.5f)
                : new Color(1f, 0.5f, 0f, 0.35f);

            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}
