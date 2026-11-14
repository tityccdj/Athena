using Athena.Core;
using Athena.Core.Interfaces;
using UnityEngine;

namespace Athena.Player.Combat
{
    [RequireComponent(typeof(Collider2D))]
    public class PlayerProjectile : MonoBehaviour
    {
        [Header("Projectile Settings (GDD: 2 Damage)")]
        [SerializeField] private int _damage = 2;
        [SerializeField] private float _speed = 16f;
        [SerializeField] private float _lifetime = 3f;
        [SerializeField] private float _knockbackForce = 5f;
        [SerializeField] private LayerMask _hitLayers;

        private Vector2 _direction = Vector2.right;
        private Rigidbody2D _rb;

        public void Launch(Vector2 direction, GameObject owner = null)
        {
            _direction = direction.normalized;
            _rb = GetComponent<Rigidbody2D>();
            if (_rb != null)
            {
                _rb.linearVelocity = _direction * _speed;
            }

            // Flip visual if moving left
            if (_direction.x < 0)
            {
                Vector3 scale = transform.localScale;
                scale.x = -Mathf.Abs(scale.x);
                transform.localScale = scale;
            }

            Destroy(gameObject, _lifetime);
        }

        private void Update()
        {
            if (_rb == null)
            {
                transform.Translate(_direction * (_speed * Time.deltaTime), Space.World);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Check if matches layer mask
            if ((_hitLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                if (!damageable.IsInvulnerable)
                {
                    DamageInfo info = new DamageInfo(
                        amount: _damage,
                        hitPoint: transform.position,
                        knockbackDirection: _direction,
                        knockbackForce: _knockbackForce,
                        source: gameObject
                    );
                    damageable.TakeDamage(info);
                    Destroy(gameObject);
                    return;
                }
            }

            // If it hits an obstacle or wall
            Destroy(gameObject);
        }
    }
}

