using UnityEngine;

namespace Athena.Audio
{
    /// <summary>
    /// Component เสริมสำหรับแปะบน GameObject ใดๆ (เช่น Player, Boss, อาวุธ, UI, หรือ ประตู)
    /// เพื่อให้สามารถผูกเรียกเล่น SFX ผ่าน UnityEvent หรือ Animation Event ได้ง่ายๆ ใน Inspector โดยไม่ต้องเขียนโค้ด
    /// </summary>
    public class SFXEventTrigger : MonoBehaviour
    {
        [Header("SFX Identifier")]
        [Tooltip("ชื่อ ID ของเสียงใน SFXManager ที่ต้องการเล่น (เช่น 'Player_Attack1', 'Player_Jump', 'Boss_Roar')")]
        [SerializeField] private string _sfxName;

        [Tooltip("AudioClip โดยตรง (กรณีต้องการระบุไฟล์เสียงในนี้โดยตรง ไม่ต้องผ่านชื่อใน SFXManager)")]
        [SerializeField] private AudioClip _directClip;

        [Header("Audio Settings Override")]
        [Range(0f, 1f)]
        [Tooltip("ตัวคูณระดับเสียง (Volume Multiplier)")]
        [SerializeField] private float _volumeMultiplier = 1.0f;

        [Tooltip("เล่นเสียงแบบ 3D ตามตำแหน่งของ GameObject นี้หรือไม่ (ถ้าไม่ติ๊กจะเป็นเสียง 2D ได้ยินชัดเจนทั่วจอ)")]
        [SerializeField] private bool _playAtTransformPosition = false;

        [Tooltip("จุดกำเนิดเสียงเฉพาะ (ถ้าปล่อยว่างจะใช้ตำแหน่งของ GameObject นี้)")]
        [SerializeField] private Transform _customAudioPoint;

        /// <summary>
        /// เล่นเสียงตาม _sfxName หรือ _directClip ที่กำหนดไว้ใน Inspector
        /// เหมาะสำหรับ Animation Event และ UnityEvent แบบไม่มี Parameter
        /// </summary>
        public void Play()
        {
            if (SFXManager.Instance == null) return;

            Vector3 pos = _customAudioPoint != null ? _customAudioPoint.position : transform.position;

            if (_directClip != null)
            {
                if (_playAtTransformPosition)
                    SFXManager.Instance.PlayClipAtPosition(_directClip, pos, _volumeMultiplier);
                else
                    SFXManager.Instance.PlayClip(_directClip, _volumeMultiplier);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_sfxName))
            {
                if (_playAtTransformPosition)
                    SFXManager.Instance.PlaySFXAtPosition(_sfxName, pos, _volumeMultiplier);
                else
                    SFXManager.Instance.PlaySFX(_sfxName, _volumeMultiplier);
            }
        }

        /// <summary>
        /// เล่นเสียงตามชื่อที่ระบุใน Parameter (เหมาะสำหรับ UnityEvent แบบรับค่า string)
        /// </summary>
        public void PlayNamed(string sfxName)
        {
            if (string.IsNullOrWhiteSpace(sfxName) || SFXManager.Instance == null) return;

            if (_playAtTransformPosition)
            {
                Vector3 pos = _customAudioPoint != null ? _customAudioPoint.position : transform.position;
                SFXManager.Instance.PlaySFXAtPosition(sfxName, pos, _volumeMultiplier);
            }
            else
            {
                SFXManager.Instance.PlaySFX(sfxName, _volumeMultiplier);
            }
        }

        /// <summary>
        /// เล่น AudioClip ที่ส่งเข้ามาผ่าน Parameter
        /// </summary>
        public void PlayClip(AudioClip clip)
        {
            if (clip == null || SFXManager.Instance == null) return;

            if (_playAtTransformPosition)
            {
                Vector3 pos = _customAudioPoint != null ? _customAudioPoint.position : transform.position;
                SFXManager.Instance.PlayClipAtPosition(clip, pos, _volumeMultiplier);
            }
            else
            {
                SFXManager.Instance.PlayClip(clip, _volumeMultiplier);
            }
        }
    }
}
