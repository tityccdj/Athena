using Athena.Core;
using Athena.Core.Interfaces;
using UnityEngine;

namespace Athena.Debug
{
    /// <summary>
    /// วางบน GameObject ที่ต้องการให้ทำ Damage เมื่อ Player ชน
    /// ใช้ทดสอบระบบ PlayerHealth, Knockback, I-Frame
    /// ตรงกับ GDD: "Obstacles: หาก Player สัมผัสโดนจะรับ 1 Damage"
    /// </summary>
    public class DamageSource : MonoBehaviour
    {
        [Header("Damage Settings")]
        [SerializeField] private int _damage = 1;
        [SerializeField] private float _knockbackForce = 5f;
        [SerializeField] private float _contactCooldown = 0.5f; // กัน multi-hit ถี่เกินไป

        [Header("Target Layer")]
        [SerializeField] private LayerMask _targetLayer; // เลือก Layer ของ Player

        private float _lastHitTime = -999f;

        private void OnCollisionEnter2D(Collision2D col)
        {
            TryDamage(col.gameObject, col.contacts[0].point);
        }

        private void OnCollisionStay2D(Collision2D col)
        {
            // ทำ damage ซ้ำถ้ายืนค้างบน obstacle (cooldown กัน spam)
            TryDamage(col.gameObject, col.contacts[0].point);
        }

        private void OnTriggerEnter2D(Collider2D col)
        {
            TryDamage(col.gameObject, col.transform.position);
        }

        private void TryDamage(GameObject target, Vector2 hitPoint)
        {
            // เช็ค cooldown
            if (Time.time < _lastHitTime + _contactCooldown) return;

            // เช็ค Layer
            if ((_targetLayer.value & (1 << target.layer)) == 0) return;

            // เช็คว่า target implement IDamageable
            if (!target.TryGetComponent<IDamageable>(out var damageable)) return;
            if (damageable.IsInvulnerable) return;

            _lastHitTime = Time.time;

            // คำนวณ knockback direction: ผลักออกจาก damage source
            Vector2 knockbackDir = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            knockbackDir.y = Mathf.Max(knockbackDir.y, 0.2f); // เพิ่ม vertical นิดหน่อย

            DamageInfo info = new DamageInfo(_damage, hitPoint, knockbackDir, _knockbackForce, gameObject);
            damageable.TakeDamage(info);

            UnityEngine.Debug.Log($"[DamageSource] {gameObject.name} hit {target.name} for {_damage} DMG");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.4f);
            Gizmos.DrawCube(transform.position, transform.localScale);
        }
    }
}
