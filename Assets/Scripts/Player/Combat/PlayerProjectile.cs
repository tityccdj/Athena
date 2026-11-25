using Athena.Audio;
using Athena.CameraSystem;
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

        [Header("Hit Stop / Freeze Frame Settings")]
        [Tooltip("ระยะเวลา Freeze Frame เมื่อกระสุนยิงโดนศัตรู (วินาที เช่น 0.06 - 0.1) 0 = ปิด")]
        [SerializeField] private float _hitFreezeDuration = 0.08f;

        [Header("Launch Particle Settings (เล่น Particle ตอนยิงออกไป)")]
        [Tooltip("ชื่อ Particle ใน ParticleManager ตอนเริ่มยิงกระสุนออกไป (ใส่หรือไม่ใส่ก็ได้)")]
        [SerializeField] private string _launchParticleName;
        [SerializeField] private GameObject _launchParticlePrefab;

        [Header("SFX Settings (เสียงตอนยิง และ เสียงตอนชน)")]
        [Tooltip("ชื่อเสียงตอนยิงกระสุนใน SFXManager เช่น 'Player_Projectile_Shoot'")]
        [SerializeField] private string _launchSFXName = "Player_Projectile_Shoot";
        [Tooltip("ชื่อเสียงตอนกระสุนชนเป้าหมายใน SFXManager เช่น 'Player_Projectile_Hit'")]
        [SerializeField] private string _hitSFXName = "Player_Projectile_Hit";

        [Header("Camera Shake on Hit (เขย่ากล้องตอนกระสุนปะทะ)")]
        [Tooltip("เปิดใช้งาน Camera Shake เมื่อกระสุนพุ่งชนเป้าหมายหรือสิ่งกีดขวางหรือไม่")]
        [SerializeField] private bool _enableCameraShake = true;
        [Tooltip("ระยะเวลาการสั่นของกล้องเมื่อกระสุนชน (วินาที)")]
        [SerializeField] private float _hitShakeDuration = 0.15f;
        [Tooltip("ความแรงการสั่นของกล้องเมื่อกระสุนชน")]
        [SerializeField] private float _hitShakeIntensity = 0.25f;

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

            // เล่น Particle และ SFX ตอนเริ่มยิงกระสุน
            SpawnLaunchParticle();
            PlayLaunchSFX();
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

                    // Freeze Frame เมื่อยิงโดนศัตรู
                    if (_hitFreezeDuration > 0f)
                    {
                        HitStopManager.Instance.Freeze(_hitFreezeDuration);
                    }
                }
            }

            // Camera Shake เมื่อกระสุนพุ่งชนเป้าหมายหรือสิ่งกีดขวาง
            if (_enableCameraShake && CameraController2D.Instance != null)
            {
                CameraController2D.Instance.TriggerShake(_hitShakeDuration, _hitShakeIntensity);
            }

            // เล่น Particle และ SFX เมื่อกระสุนกระทบเป้าหมายหรือสิ่งกีดขวาง
            SpawnHitParticle(hitPoint);
            PlayHitSFX(hitPoint);
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

        private void PlayLaunchSFX()
        {
            if (!string.IsNullOrWhiteSpace(_launchSFXName) && SFXManager.Instance != null)
            {
                SFXManager.Instance.PlaySFXAtPosition(_launchSFXName, transform.position);
            }
        }

        private void PlayHitSFX(Vector2 hitPoint)
        {
            if (!string.IsNullOrWhiteSpace(_hitSFXName) && SFXManager.Instance != null)
            {
                SFXManager.Instance.PlaySFXAtPosition(_hitSFXName, hitPoint);
            }
        }
    }
}

