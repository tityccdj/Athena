using System.Collections.Generic;
using Athena.Core;
using Athena.Core.Interfaces;
using Athena.Particles;
using UnityEngine;

namespace Athena.Player.Combat
{
    /// <summary>
    /// Hitbox ที่เปิด/ปิดได้จาก Animation Event
    /// Open() → เริ่ม check ทุก FixedUpdate, Close() → หยุด check
    /// รองรับการเล่น Particle Effect ณ จุดปะทะที่ฟันโดนศัตรู
    /// </summary>
    public class PlayerAttackHitbox : MonoBehaviour
    {
        [Header("Hitbox Area")]
        [SerializeField] private Vector2 _offset = new Vector2(0.8f, 0f);
        [SerializeField] private float _radius = 0.6f;
        [SerializeField] private LayerMask _targetLayers;

        [Header("Hit Particle Settings (เล่น Particle ณ จุดที่ฟันโดน)")]
        [Tooltip("ชื่อ Particle ใน ParticleManager เช่น 'HitImpact' หรือ 'BloodSpark'")]
        [SerializeField] private string _hitParticleName;

        [Tooltip("Prefab สำรองกรณีต้องการลาก Prefab ใส่โดยตรงโดยไม่ผ่าน ParticleManager")]
        [SerializeField] private GameObject _hitParticlePrefab;

        [Tooltip("ระยะ Offset เพิ่มเติมจากจุดปะทะ")]
        [SerializeField] private Vector3 _particleOffset = Vector3.zero;

        [Header("Hit Stop / Freeze Frame Settings")]
        [Tooltip("ระยะเวลา Freeze Frame เมื่อฟัน Hit ที่สองโดนศัตรู (วินาที เช่น 0.08 - 0.12) 0 = ปิด")]
        [SerializeField] private float _hit2FreezeDuration = 0.08f;

        [Header("Unity Events (สำหรับเชื่อมต่อ Event อื่นๆ เพิ่มเติม)")]
        [SerializeField] private UnityEngine.Events.UnityEvent _onHitTargetEvent;
        [SerializeField] private UnityEngine.Events.UnityEvent<Vector3> _onHitPositionEvent;

        private bool _isActive;
        private int _damage;
        private int _facingDirection;
        private float _knockbackForce;
        private GameObject _attacker;
        private bool _isComboHit2 = false;

        // Track hit entities within single open window to avoid multi-hit
        private readonly HashSet<IDamageable> _damagedThisSwing = new HashSet<IDamageable>();

        public bool IsActive => _isActive;

        /// <summary>
        /// เรียกจาก Animation Event "HitboxOpen" — เปิด hitbox พร้อมตั้งค่า damage
        /// </summary>
        public void Open(int damage, int facingDirection, float knockbackForce, GameObject attacker, bool isComboHit2 = false, float overrideFreezeDuration = -1f)
        {
            _damage = damage;
            _facingDirection = facingDirection;
            _knockbackForce = knockbackForce;
            _attacker = attacker;
            _isComboHit2 = isComboHit2;
            if (overrideFreezeDuration >= 0f)
            {
                _hit2FreezeDuration = overrideFreezeDuration;
            }
            _damagedThisSwing.Clear();
            _isActive = true;
        }

        /// <summary>
        /// เรียกจาก Animation Event "HitboxClose" — ปิด hitbox
        /// </summary>
        public void Close()
        {
            _isActive = false;
            _isComboHit2 = false;
            _damagedThisSwing.Clear();
        }

        /// <summary>
        /// Alias สำหรับ Close() เพื่อความเข้ากันได้
        /// </summary>
        public void CloseHitbox() => Close();

        private void SpawnHitParticle(Vector2 hitPoint)
        {
            Vector3 spawnPos = new Vector3(hitPoint.x, hitPoint.y, 0f) + _particleOffset;

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

                        // คำนวณจุดปะทะที่ผิว Collider ของศัตรู (Closest Point)
                        Vector2 hitPoint = hit.ClosestPoint(origin);

                        Vector2 knockbackDir = new Vector2(_facingDirection, 0.2f).normalized;
                        DamageInfo info = new DamageInfo(_damage, hitPoint, knockbackDir, _knockbackForce, _attacker);
                        damageable.TakeDamage(info);

                        // Freeze Frame เมื่อฟัน Hit ที่สองโดนศัตรู
                        if (_isComboHit2 && _hit2FreezeDuration > 0f)
                        {
                            HitStopManager.Instance.Freeze(_hit2FreezeDuration);
                        }

                        // เล่น Particle Effect ณ ตำแหน่งที่ฟันโดนทันที
                        SpawnHitParticle(hitPoint);

                        _onHitTargetEvent?.Invoke();
                        _onHitPositionEvent?.Invoke(hitPoint);
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
