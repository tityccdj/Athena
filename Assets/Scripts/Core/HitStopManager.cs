using System.Collections;
using Athena.UI;
using UnityEngine;

namespace Athena.Core
{
    /// <summary>
    /// จัดการระบบ Hit Stop / Freeze Frame ชั่วคราวเพื่อเพิ่ม Game Feel & Impact
    /// ทำงานโดยการหยุด Time.timeScale = 0f ชั่วขณะตาม Realtime (วินาทีจริง)
    /// </summary>
    public class HitStopManager : MonoBehaviour
    {
        private static HitStopManager _instance;

        public static HitStopManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<HitStopManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("[HitStopManager]");
                        _instance = go.AddComponent<HitStopManager>();
                    }
                }
                return _instance;
            }
        }

        private Coroutine _freezeRoutine;
        private bool _isFreezing = false;

        public bool IsFreezing => _isFreezing;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        /// <summary>
        /// ทำการ Freeze Frame เป็นระยะเวลาสั้นๆ (หน่วยเป็นวินาทีจริง Realtime)
        /// </summary>
        /// <param name="duration">ระยะเวลาหยุดเวลา เช่น 0.08s</param>
        /// <param name="freezeTimeScale">ค่า Time.timeScale ขณะหยุด (ปกติคือ 0f)</param>
        public void Freeze(float duration, float freezeTimeScale = 0f)
        {
            if (duration <= 0.001f) return;

            // ถ้าเกมกำลัง Pause หรือ Player ตายอยู่ ไม่ต้องทำ Freeze Frame
            if (PauseRespawnUI.Instance != null && (PauseRespawnUI.Instance.IsPaused || PauseRespawnUI.Instance.IsPlayerDead))
            {
                return;
            }

            if (_freezeRoutine != null)
            {
                StopCoroutine(_freezeRoutine);
            }

            _freezeRoutine = StartCoroutine(FreezeRoutine(duration, freezeTimeScale));
        }

        private IEnumerator FreezeRoutine(float duration, float freezeTimeScale)
        {
            _isFreezing = true;
            Time.timeScale = freezeTimeScale;

            yield return new WaitForSecondsRealtime(duration);

            // คืนค่า timeScale กลับเป็น 1.0f เฉพาะเมื่อไม่ได้อยู่ในสถานะ Pause หรือ Player ตาย
            if (PauseRespawnUI.Instance == null || (!PauseRespawnUI.Instance.IsPaused && !PauseRespawnUI.Instance.IsPlayerDead))
            {
                Time.timeScale = 1.0f;
            }

            _isFreezing = false;
            _freezeRoutine = null;
        }
    }
}
