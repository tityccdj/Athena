using System.Collections.Generic;
using Athena.Core;
using Athena.Core.Interfaces;
using UnityEngine;

namespace Athena.Player.Combat
{
    public class PlayerAttackHitbox : MonoBehaviour
    {
        [Header("Hitbox Area")]
        [SerializeField] private Vector2 _offset = new Vector2(0.8f, 0f);
        [SerializeField] private float _radius = 0.6f;
        [SerializeField] private LayerMask _targetLayers;

        public int PerformAttack(int damage, int facingDirection, float knockbackForce, GameObject attacker)
        {
            Vector2 origin = (Vector2)transform.position + new Vector2(_offset.x * facingDirection, _offset.y);
            Collider2D[] hits = Physics2D.OverlapCircleAll(origin, _radius, _targetLayers);

            HashSet<IDamageable> damagedEntities = new HashSet<IDamageable>();
            int hitCount = 0;

            foreach (var hit in hits)
            {
                if (hit.gameObject == attacker) continue;

                if (hit.TryGetComponent<IDamageable>(out var damageable))
                {
                    if (!damagedEntities.Contains(damageable) && !damageable.IsInvulnerable)
                    {
                        damagedEntities.Add(damageable);
                        Vector2 knockbackDir = new Vector2(facingDirection, 0.2f).normalized;
                        DamageInfo info = new DamageInfo(damage, hit.transform.position, knockbackDir, knockbackForce, attacker);
                        damageable.TakeDamage(info);
                        hitCount++;
                    }
                }
            }

            return hitCount;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector2 origin = (Vector2)transform.position + _offset;
            Gizmos.DrawWireSphere(origin, _radius);
        }
    }
}

