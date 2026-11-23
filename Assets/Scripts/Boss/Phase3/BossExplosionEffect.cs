using Athena.Boss.Core;
using UnityEngine;

namespace Athena.Boss.Phase3
{
    /// <summary>
    /// ควบคุมการทำงานของ Prefab Explosion:
    /// - เปิดใช้งาน Hitbox ทันทีที่ถูกเสกขึ้นมา
    /// - ทำลายตัวเองอัตโนมัติหลังจากเล่นแอนิเมชันระเบิดเสร็จ
    /// </summary>
    public class BossExplosionEffect : MonoBehaviour
    {
        [Header("Lifetime Settings")]
        [Tooltip("ระยะเวลาแสดงผลระเบิดก่อนทำลายตัวเอง (วินาที)")]
        [SerializeField] private float _lifetime = 0.6f;

        [Header("Hitbox Settings")]
        [Tooltip("สั่งเปิด BossDamageHitbox ทันทีเมื่อเกิด")]
        [SerializeField] private bool _autoOpenHitbox = true;

        private void Start()
        {
            if (_autoOpenHitbox)
            {
                // ตรวจหา BossDamageHitbox ทั้งบน Root และ Children
                BossDamageHitbox[] hitboxes = GetComponentsInChildren<BossDamageHitbox>();
                foreach (var hb in hitboxes)
                {
                    hb.OpenHitbox();
                }
            }

            // ถ้ามี Animator สามารถคำนวณระยะเวลาจากความยาวของคลิปแอนิเมชันได้
            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                AnimatorClipInfo[] clipInfos = anim.GetCurrentAnimatorClipInfo(0);
                if (clipInfos != null && clipInfos.Length > 0 && clipInfos[0].clip != null)
                {
                    float clipLength = clipInfos[0].clip.length;
                    if (clipLength > 0.1f)
                    {
                        _lifetime = clipLength;
                    }
                }
            }

            Destroy(gameObject, _lifetime);
        }
    }
}
