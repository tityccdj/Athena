using Athena.Core;
using Athena.Core.Interfaces;
using Athena.Particles;
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

        [Header("Hit Particle Settings (เล่น Particle เมื่อกระสุนกระทบเป้าหมาย/กำแพง)")]
        [Tooltip("ชื่อ Particle ใน ParticleManager เช่น 'ProjectileHit' หรือ 'Explosion'")]
        [SerializeField] private string _hitParticleName;

        [Tooltip("Prefab สำรองกรณีต้องการลาก Prefab ใส่โดยตรงโดยไม่ผ่าน ParticleManager")]
        [SerializeField] private GameObject _hitParticlePrefab;

        [Tooltip("ระยะ Offset เพิ่มเติมจากจุดปะทะ")]
        [SerializeField] private Vector3 _hitParticleOffset = Vector3.zero;

        [Header("Launch Particle Settings (เล่น Particle ตอนยิงออกไป)")]
        [Tooltip("ชื่อ Particle ใน ParticleManager ตอนเริ่มยิงกระสุนออกไป (ใส่หรือไม่ใส่ก็ได้)")]
        [SerializeField] private string _launchParticleName;
        [SerializeField] private GameObject _launchParticlePrefab;

        [Header("Unity Events (สำหรับเชื่อมต่อ Event เช่น เสียง หรือ Particle)")]
        [SerializeField] private UnityEngine.Events.UnityEvent _onLaunchEvent;
        [SerializeField] private UnityEngine.Events.UnityEvent _onHitEvent;
        [SerializeField] private UnityEngine.Events.UnityEvent<Vector3> _onHitPositionEvent;

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

            // เล่น Particle ตอนเริ่มยิงกระสุน
            SpawnLaunchParticle();
            _onLaunchEvent?.Invoke();

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

            Vector2 hitPoint = other.ClosestPoint(transform.position);

            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                if (!damageable.IsInvulnerable)
                {
                    DamageInfo info = new DamageInfo(
                        amount: _damage,
                        hitPoint: hitPoint,
                        knockbackDirection: _direction,
                        knockbackForce: _knockbackForce,
                        source: gameObject
                    );
                    damageable.TakeDamage(info);
                }
            }

            // เล่น Particle เมื่อกระสุนกระทบเป้าหมายหรือสิ่งกีดขวาง
            SpawnHitParticle(hitPoint);
            _onHitEvent?.Invoke();
            _onHitPositionEvent?.Invoke(hitPoint);

            Destroy(gameObject);
        }

        private void SpawnHitParticle(Vector2 hitPoint)
        {
            Vector3 spawnPos = new Vector3(hitPoint.x, hitPoint.y, 0f) + _hitParticleOffset;

            if (ParticleManager.Instance != null)
            {
                if (!string.IsNullOrWhiteSpace(_hitParticleName))
                {
                    ParticleManager.Instance.PlayParticle(_hitParticleName, spawnPos);
                }
                else if (_hitParticlePrefab != null)
                {
                    ParticleManager.Instance.PlayPrefab(_hitParticlePrefab, spawnPos);
                }
            }
        }

        private void SpawnLaunchParticle()
        {
            if (ParticleManager.Instance != null)
            {
                if (!string.IsNullOrWhiteSpace(_launchParticleName))
                {
                    ParticleManager.Instance.PlayParticle(_launchParticleName, transform.position);
                }
                else if (_launchParticlePrefab != null)
                {
                    ParticleManager.Instance.PlayPrefab(_launchParticlePrefab, transform.position);
                }
            }
        }
    }
}

