using System.Collections;
using System.Collections.Generic;
using Athena.Core;
using Athena.Core.Interfaces;
using UnityEngine;

namespace Athena.Boss.Phase3
{
    /// <summary>
    /// อุกกาบาต / เวทมนตร์ลูกไฟยักษ์ ที่บอส Phase 3 (Necromancer) เรียกตกลงมาใส่ตำแหน่ง Player
    /// ตาม GDD: "Meteor: เรียกอุกกาบาตตกลงมาตรงตำแหน่งที่ Player ยืนอยู่ (Player อยู่ไกล)"
    /// - แสดงสัญลักษณ์เตือนบนพื้น (Ground Warning / Telegraph) ก่อนตก
    /// - พุ่งตกลงมาจากด้านบนลงสู่ตำแหน่งเป้าหมาย
    /// - ระเบิดสร้างความเสียหาย 1 Damage แบบ AOE ให้ Player
    /// </summary>
    public class NecromancerMeteorProjectile : MonoBehaviour
    {
        [Header("Damage Settings (GDD: 1 Damage)")]
        [SerializeField] private int _damage = 1;
        [SerializeField] private float _knockbackForce = 7f;
        [SerializeField] private float _explosionRadius = 1.8f;
        [SerializeField] private LayerMask _targetPlayerLayer;
        [SerializeField] private LayerMask _obstacleLayer;

        [Header("Meteor Flight Settings")]
        [SerializeField] private float _spawnHeight = 10f;
        [SerializeField] private float _fallSpeed = 22f;
        [SerializeField] private float _warningDuration = 0.75f;

        [Header("Visual Components")]
        [SerializeField] private SpriteRenderer _meteorSprite;
        [SerializeField] private SpriteRenderer _telegraphWarningSprite;
        [SerializeField] private Color _warningColor = new Color(1f, 0.2f, 0.2f, 0.6f);
        [SerializeField] private Color _warningFlashColor = new Color(1f, 0.9f, 0.2f, 0.9f);

        [Header("Impact Effect (Optional)")]
        [SerializeField] private GameObject _impactEffectPrefab;

        private Vector2 _targetPosition;
        private bool _isFalling = false;
        private bool _hasImpacted = false;
        private readonly HashSet<IDamageable> _hitEntities = new HashSet<IDamageable>();

        /// <summary>
        /// สั่งยิงอุกกาบาตลงมาที่ตำแหน่ง targetPosition
        /// </summary>
        public void Launch(Vector2 targetPosition, LayerMask playerLayer, LayerMask obstacleLayer = default)
        {
            _targetPosition = targetPosition;
            _targetPlayerLayer = playerLayer;
            if (obstacleLayer != default) _obstacleLayer = obstacleLayer;

            StartCoroutine(MeteorLifecycleRoutine());
        }

        private IEnumerator MeteorLifecycleRoutine()
        {
            // 1. ตั้งตำแหน่ง Warning Indicator บนพื้น
            if (_telegraphWarningSprite != null)
            {
                _telegraphWarningSprite.transform.position = _targetPosition;
                _telegraphWarningSprite.gameObject.SetActive(true);
            }

            // ซ่อนตัวอุกกาบาตไว้ก่อนระหว่างชาร์จเตือน
            if (_meteorSprite != null)
            {
                _meteorSprite.gameObject.SetActive(false);
            }

            // 2. ช่วงชาร์จเตือน (Warning Telegraph Flash)
            float elapsed = 0f;
            bool isToggled = false;
            float flashInterval = 0.1f;

            while (elapsed < _warningDuration)
            {
                if (_telegraphWarningSprite != null)
                {
                    _telegraphWarningSprite.color = isToggled ? _warningFlashColor : _warningColor;
                    // ขยายวงเตือนขึ้นทีละน้อย
                    float scaleProgress = Mathf.Lerp(0.6f, 1.2f, elapsed / _warningDuration);
                    _telegraphWarningSprite.transform.localScale = new Vector3(scaleProgress * _explosionRadius * 2f, scaleProgress * 0.6f, 1f);
                }
                isToggled = !isToggled;
                yield return new WaitForSeconds(flashInterval);
                elapsed += flashInterval;
            }

            // 3. เริ่มปล่อยอุกกาบาตพุ่งลงมาจากฟ้า
            Vector3 startPos = new Vector3(_targetPosition.x, _targetPosition.y + _spawnHeight, 0f);
            transform.position = startPos;

            if (_meteorSprite != null)
            {
                _meteorSprite.gameObject.SetActive(true);
            }

            _isFalling = true;

            // คำนวณการตก
            while (_isFalling && !_hasImpacted)
            {
                transform.position = Vector3.MoveTowards(transform.position, _targetPosition, _fallSpeed * Time.deltaTime);

                if (Vector2.Distance(transform.position, _targetPosition) <= 0.2f)
                {
                    Explode();
                    break;
                }

                yield return null;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_isFalling || _hasImpacted) return;

            // ถ้าโดน Player ระหว่างตก ให้ระเบิดทันที
            if (_targetPlayerLayer != 0 && ((1 << other.gameObject.layer) & _targetPlayerLayer.value) != 0)
            {
                Explode();
            }
            // ถ้าชนพื้นดินหรือสิ่งกีดขวาง ให้ระเบิด
            else if (_obstacleLayer != 0 && ((1 << other.gameObject.layer) & _obstacleLayer.value) != 0)
            {
                Explode();
            }
        }

        private void Explode()
        {
            if (_hasImpacted) return;
            _hasImpacted = true;
            _isFalling = false;

            // ซ่อนหรือปิดเตือน
            if (_telegraphWarningSprite != null)
            {
                _telegraphWarningSprite.gameObject.SetActive(false);
            }

            if (_meteorSprite != null)
            {
                _meteorSprite.gameObject.SetActive(false);
            }

            // สร้าง Impact Effect ถ้ามี
            if (_impactEffectPrefab != null)
            {
                Instantiate(_impactEffectPrefab, _targetPosition, Quaternion.identity);
            }

            // คำนวณ AOE Damage แก่ Player ที่อยู่ในรัศมีระเบิด
            Collider2D[] hits = Physics2D.OverlapCircleAll(_targetPosition, _explosionRadius, _targetPlayerLayer);
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent<IDamageable>(out var damageable))
                {
                    if (!_hitEntities.Contains(damageable) && !damageable.IsInvulnerable)
                    {
                        _hitEntities.Add(damageable);
                        Vector2 knockbackDir = ((Vector2)hit.transform.position - _targetPosition).normalized;
                        knockbackDir.y = Mathf.Max(knockbackDir.y, 0.4f); // ผลักขึ้นเล็กน้อย

                        DamageInfo info = new DamageInfo(_damage, hit.transform.position, knockbackDir, _knockbackForce, gameObject);
                        damageable.TakeDamage(info);
                    }
                }
            }

            // ทำลายตัวเองหลังระเบิดเสร็จ
            Destroy(gameObject, 0.5f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(Application.isPlaying ? (Vector3)_targetPosition : transform.position, _explosionRadius);
        }
    }
}
