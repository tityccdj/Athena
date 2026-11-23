using System.Collections;
using UnityEngine;

namespace Athena.Boss.Phase3
{
    /// <summary>
    /// ควบคุม lifecycle ของ Telegraph Prefab:
    /// 1. เล่น Animation ของตัวเองจนจบ
    /// 2. เสก Explosion Prefab ที่ตำแหน่งเดียวกัน
    /// 3. ทำลายตัวเอง
    /// 
    /// ทำงานอิสระจาก Boss Controller — boss แค่ spawn แล้วปล่อยไป
    /// </summary>
    public class ExplosionTelegraphEffect : MonoBehaviour
    {
        [Header("Explosion Prefab (เสกหลัง telegraph เล่นจบ)")]
        [SerializeField] private GameObject _explosionPrefab;

        [Header("Timing")]
        [Tooltip("ระยะเวลารอก่อนเสก Explosion (ถ้า 0 จะคำนวณจากความยาว Animation อัตโนมัติ)")]
        [SerializeField] private float _delayOverride = 0f;

        [Tooltip("Fallback ถ้าไม่มี Animator หรือคลิป")]
        [SerializeField] private float _fallbackDelay = 0.5f;

        private void Start()
        {
            float delay = _delayOverride;

            // ถ้าไม่ได้กำหนด delay เอง → อ่านจากความยาวคลิป Animation
            if (delay <= 0f)
            {
                Animator anim = GetComponentInChildren<Animator>();
                if (anim != null && anim.runtimeAnimatorController != null)
                {
                    AnimatorClipInfo[] clipInfos = anim.GetCurrentAnimatorClipInfo(0);
                    if (clipInfos != null && clipInfos.Length > 0 && clipInfos[0].clip != null)
                    {
                        delay = clipInfos[0].clip.length;
                    }
                }

                if (delay <= 0f)
                {
                    delay = _fallbackDelay;
                }
            }

            StartCoroutine(TelegraphSequence(delay));
        }

        private IEnumerator TelegraphSequence(float waitTime)
        {
            // รอให้ animation เล่นจบ
            yield return new WaitForSeconds(waitTime);

            // เสก Explosion ที่ตำแหน่งเดียวกัน
            if (_explosionPrefab != null)
            {
                Instantiate(_explosionPrefab, transform.position, Quaternion.identity);
            }

            // ทำลายตัวเอง
            Destroy(gameObject);
        }
    }
}
