using Athena.Core;
using Athena.Core.Interfaces;
using UnityEngine;

namespace Athena.Debug
{
    /// <summary>
    /// Dummy สำหรับทดสอบระบบ Damage และ Knockback
    /// ใส่ Script นี้บน GameObject ที่มี Collider2D + Rigidbody2D
    /// Layer ต้องตรงกับ Layer ที่ PlayerAttackHitbox._targetLayers กำหนดไว้
    /// </summary>
    public class TestDummy : MonoBehaviour, IDamageable, IKnockbackable
    {
        [Header("Dummy Stats")]
        [SerializeField] private int _maxHP = 10;

        [Header("Read-Only (Inspector Monitor)")]
        [SerializeField, ReadOnly] private int _currentHP;
        [SerializeField, ReadOnly] private int _totalDamageTaken;
        [SerializeField, ReadOnly] private int _hitCount;
        [SerializeField, ReadOnly] private string _lastHitInfo = "-";

        private Rigidbody2D _rb;
        private SpriteRenderer _spriteRenderer;
        private Color _originalColor;

        public bool IsInvulnerable => false; // Dummy รับ damage ตลอดเวลา

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer != null) _originalColor = _spriteRenderer.color;
            ResetDummy();
        }

        public void TakeDamage(DamageInfo info)
        {
            _currentHP -= info.Amount;
            _totalDamageTaken += info.Amount;
            _hitCount++;
            _lastHitInfo = $"DMG:{info.Amount}  Knockback:{info.KnockbackForce:F1}  From:{info.Source?.name ?? "?"}";

            UnityEngine.Debug.Log($"[TestDummy] Hit #{_hitCount} | -{info.Amount} HP | HP: {_currentHP}/{_maxHP} | Knockback: {info.KnockbackForce:F1} | Dir: {info.KnockbackDirection}");

            // Visual flash
            if (_spriteRenderer != null)
            {
                StopAllCoroutines();
                StartCoroutine(FlashRed());
            }

            // Apply knockback
            ApplyKnockback(info.KnockbackDirection, info.KnockbackForce);

            if (_currentHP <= 0)
            {
                UnityEngine.Debug.Log($"[TestDummy] DEAD after {_hitCount} hits / {_totalDamageTaken} total damage — Resetting in 2s");
                Invoke(nameof(ResetDummy), 2f);
            }
        }

        public void ApplyKnockback(Vector2 direction, float force)
        {
            if (_rb != null && force > 0f)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.AddForce(direction * force, ForceMode2D.Impulse);
            }
        }

        [ContextMenu("Reset Dummy")]
        public void ResetDummy()
        {
            _currentHP = _maxHP;
            _totalDamageTaken = 0;
            _hitCount = 0;
            _lastHitInfo = "-";
            if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
            UnityEngine.Debug.Log("[TestDummy] Reset!");
        }

        private System.Collections.IEnumerator FlashRed()
        {
            if (_spriteRenderer != null) _spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;
        }

        private void OnDrawGizmos()
        {
            // แสดง HP bar เหนือ Dummy ใน Scene view
            #if UNITY_EDITOR
            if (!Application.isPlaying) return;
            Vector3 pos = transform.position + Vector3.up * 1.2f;
            float ratio = (float)_currentHP / _maxHP;
            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.DrawLine(pos - Vector3.right * 0.5f, pos + Vector3.right * (ratio - 0.5f));
            UnityEditor.Handles.color = Color.gray;
            UnityEditor.Handles.DrawLine(pos + Vector3.right * (ratio - 0.5f), pos + Vector3.right * 0.5f);
            #endif
        }
    }

    // Attribute สำหรับแสดงค่าแบบ Read-Only ใน Inspector
    public class ReadOnlyAttribute : PropertyAttribute { }

    #if UNITY_EDITOR
    [UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            UnityEditor.EditorGUI.PropertyField(position, property, label);
            GUI.enabled = true;
        }
    }
    #endif
}
