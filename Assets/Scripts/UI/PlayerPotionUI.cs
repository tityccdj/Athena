using Athena.Player;
using Athena.Player.Health;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Athena.UI
{
    /// <summary>
    /// ควบคุมการแสดงผลคูลดาวน์ของยาเพิ่มเลือด (Heal Potion)
    /// ตาม GDD: กดใช้งานเพิ่ม HP 2 หน่วย (คูลดาวน์ 30 วินาที)
    /// ใช้ Image แบบ Filled (Radial 360) แสดงคูลดาวน์หมุนวน
    /// </summary>
    public class PlayerPotionUI : MonoBehaviour
    {
        [Header("Player Health Reference")]
        [Tooltip("ถ้าไม่ใส่ จะค้นหา PlayerHealth ในฉากให้อัตโนมัติ")]
        [SerializeField] private PlayerHealth _playerHealth;

        [Header("Radial Cooldown Fill")]
        [Tooltip("Image รูปวงกลมที่ตั้งค่า Image Type = Filled, Fill Method = Radial 360")]
        [SerializeField] private Image _cooldownRadialFill;

        [Tooltip("กลับทิศทางการ Fill (ติ๊กถูก: เริ่มเต็ม 1.0 แล้วค่อยๆ หมุนลดลงจนเหลือ 0)")]
        [SerializeField] private bool _invertFill = true;

        [Header("Potion Visual Tint")]
        [Tooltip("ไอคอนรูปยา (เปลี่ยนสีมืดลงขณะติดคูลดาวน์)")]
        [SerializeField] private Image _potionIcon;
        [SerializeField] private Color _readyColor = Color.white;
        [SerializeField] private Color _cooldownColor = new Color(0.35f, 0.35f, 0.35f, 0.85f);

        [Header("Optional Cooldown Text (นับเวลาถอยหลัง)")]
        [Tooltip("TextMeshProUGUI สำหรับแสดงตัวเลขวินาที เช่น 29, 28... (ใส่หรือไม่ใส่ก็ได้)")]
        [SerializeField] private TextMeshProUGUI _cooldownTextTMP;
        [Tooltip("Legacy Text สำรองสำหรับแสดงตัวเลขวินาที")]
        [SerializeField] private Text _cooldownTextLegacy;

        private void Start()
        {
            FindPlayerHealthIfNeeded();
        }

        private void Update()
        {
            if (_playerHealth == null)
            {
                FindPlayerHealthIfNeeded();
                if (_playerHealth == null) return;
            }

            float remaining = _playerHealth.HealCooldownRemaining;
            float max = _playerHealth.HealCooldownMax;

            if (remaining > 0.05f)
            {
                // กำลังติดคูลดาวน์
                float ratio = Mathf.Clamp01(remaining / max);

                if (_cooldownRadialFill != null)
                {
                    _cooldownRadialFill.gameObject.SetActive(true);
                    _cooldownRadialFill.fillAmount = _invertFill ? ratio : (1f - ratio);
                }

                if (_potionIcon != null)
                {
                    _potionIcon.color = _cooldownColor;
                }

                // แสดงตัวเลขวินาที
                int secondsLeft = Mathf.CeilToInt(remaining);
                string textStr = secondsLeft.ToString();

                if (_cooldownTextTMP != null)
                {
                    _cooldownTextTMP.gameObject.SetActive(true);
                    _cooldownTextTMP.text = textStr;
                }
                if (_cooldownTextLegacy != null)
                {
                    _cooldownTextLegacy.gameObject.SetActive(true);
                    _cooldownTextLegacy.text = textStr;
                }
            }
            else
            {
                // คูลดาวน์พร้อมใช้งานแล้ว
                if (_cooldownRadialFill != null)
                {
                    _cooldownRadialFill.fillAmount = _invertFill ? 0f : 1f;
                    _cooldownRadialFill.gameObject.SetActive(!_invertFill);
                }

                if (_potionIcon != null)
                {
                    _potionIcon.color = _readyColor;
                }

                if (_cooldownTextTMP != null)
                {
                    _cooldownTextTMP.gameObject.SetActive(false);
                    _cooldownTextTMP.text = string.Empty;
                }
                if (_cooldownTextLegacy != null)
                {
                    _cooldownTextLegacy.gameObject.SetActive(false);
                    _cooldownTextLegacy.text = string.Empty;
                }
            }
        }

        private void FindPlayerHealthIfNeeded()
        {
            if (_playerHealth == null)
            {
                PlayerController pc = FindFirstObjectByType<PlayerController>();
                if (pc != null)
                {
                    _playerHealth = pc.Health;
                }
                else
                {
                    _playerHealth = FindFirstObjectByType<PlayerHealth>();
                }
            }
        }
    }
}
