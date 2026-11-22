using System.Collections.Generic;
using Athena.Core;
using Athena.Core.Interfaces;
using UnityEngine;

namespace Athena.Boss.Phase2
{
    /// <summary>
    /// คลื่น Shockwave ที่บอส Phase 2 ปล่อยออกมาจากท่า Attack03 (Slash and Wave)
    /// เคลื่อนที่ไปตามพื้น และทำ 1 Damage ให้ Player
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class BossShockwaveProjectile : MonoBehaviour
    {
        [Header("Projectile Settings")]
        [SerializeField] private float _speed = 9f;
        [SerializeField] private int _damage = 1;
        [SerializeField] private float _knockbackForce = 6f;
        [SerializeField] private float _lifeTime = 4f;
        [SerializeField] private LayerMask _targetLayer; // Player Layer
        [SerializeField] private LayerMask _obstacleLayer; // Wall / Ground Layer

        private Vector2 _direction = Vector2.right;
        private readonly HashSet<IDamageable> _damagedEntities = new HashSet<IDamageable>();

        private void Start()
        {
            Destroy(gameObject, _lifeTime);
        }

        public void Launch(int facingDirection, LayerMask targetLayer, LayerMask obstacleLayer = default)
        {
            _direction = new Vector2(facingDirection, 0f).normalized;
            _targetLayer = targetLayer;
            if (obstacleLayer != default) _obstacleLayer = obstacleLayer;

            // พลิก Sprite ให้หันตามทิศทางที่ปล่อย
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (facingDirection > 0 ? 1 : -1);
            transform.localScale = scale;
        }

        private void Update()
        {
            transform.position += (Vector3)(_direction * _speed * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // ชนกำแพงหรือสิ่งกีดขวาง ให้ทำลายตัวเอง
            if (_obstacleLayer != 0 && (_obstacleLayer.value & (1 << other.gameObject.layer)) != 0)
            {
                Destroy(gameObject);
                return;
            }

            // ตรวจสอบ Player Layer
            if (_targetLayer != 0 && (_targetLayer.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                if (!_damagedEntities.Contains(damageable) && !damageable.IsInvulnerable)
                {
                    _damagedEntities.Add(damageable);
                    Vector2 knockbackDir = new Vector2(_direction.x, 0.3f).normalized;
                    DamageInfo info = new DamageInfo(_damage, other.transform.position, knockbackDir, _knockbackForce, gameObject);
                    damageable.TakeDamage(info);

                    Destroy(gameObject);
                }
            }
        }
    }
}
