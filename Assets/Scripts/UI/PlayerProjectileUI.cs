using Athena.Player;
using Athena.Player.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Athena.UI
{
    /// <summary>
    /// ควบคุมการแสดงผลคูลดาวน์ของ Projectile Attack (Dash Attack) บนหน้าต่าง UI:
    /// - รองรับทั้ง Slider (Unity UI) และ Image Filled (Radial หรือ Bar)
    /// - รองรับการแสดงผลตัวเลขนับถอยหลัง (TextMeshProUGUI หรือ Legacy Text)
    /// - ปรับเปลี่ยนสีไอคอนอัตโนมัติ (มืดลงขณะติดคูลดาวน์ สว่างขึ้นเมื่อพร้อมใช้งาน)
    /// </summary>
    public class PlayerProjectileUI : MonoBehaviour
    {
        [Header("Player Combat Reference")]
        [Tooltip("ถ้าไม่ใส่ จะค้นหา PlayerCombat ในฉากให้อัตโนมัติ")]
        [SerializeField] private PlayerCombat _playerCombat;

        [Header("UI Slider / Fill Display")]
        [Tooltip("Slider สำหรับแสดงหลอดคูลดาวน์")]
        [SerializeField] private Slider _cooldownSlider;

        [Tooltip("Image แบบ Filled (Radial หรือ Horizontal/Vertical) สำหรับแสดงคูลดาวน์")]
        [SerializeField] private Image _cooldownFillImage;

        [Tooltip("สลับทิศทาง (ติ๊กถูก: เต็ม 1.0 แล้วค่อยๆ ลดลงจนเหลือ 0 / ไม่ติ๊ก: เริ่มจาก 0 แล้วค่อยๆ เพิ่มขึ้นจนเต็ม 1.0)")]
        [SerializeField] private bool _invertFill = false;

        [Header("Projectile Icon Visual Tint")]
        [Tooltip("ไอคอนรูปกระสุน/ท่าโจมตี (เปลี่ยนสีมืดลงขณะติดคูลดาวน์)")]
        [SerializeField] private Image _projectileIcon;
        [SerializeField] private Color _readyColor = Color.white;
        [SerializeField] private Color _cooldownColor = new Color(0.35f, 0.35f, 0.35f, 0.85f);

        [Header("Optional Cooldown Text (นับเวลาถอยหลัง)")]
        [Tooltip("TextMeshProUGUI สำหรับแสดงตัวเลขวินาที เช่น 2.5s")]
        [SerializeField] private TextMeshProUGUI _cooldownTextTMP;
        [Tooltip("Legacy Text สำรองสำหรับแสดงตัวเลขวินาที")]
        [SerializeField] private Text _cooldownTextLegacy;
        [SerializeField] private bool _showDecimal = true;

        [Header("Optional Unity Events")]
        [SerializeField] private UnityEngine.Events.UnityEvent _onCooldownReadyEvent;

        private bool _wasOnCooldownLastFrame = false;

        private void Start()
        {
            FindPlayerCombatIfNeeded();
        }

        private void Update()
        {
            if (_playerCombat == null)
            {
                FindPlayerCombatIfNeeded();
                if (_playerCombat == null) return;
            }

            float remaining = _playerCombat.ProjectileCooldownRemaining;
            float duration = _playerCombat.ProjectileCooldownDuration;

            if (remaining > 0.05f)
            {
                _wasOnCooldownLastFrame = true;

                // กำลังติดคูลดาวน์
                float ratio = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
                float displayRatio = _invertFill ? ratio : (1f - ratio);

                if (_cooldownSlider != null)
                {
                    _cooldownSlider.value = displayRatio;
                }

                if (_cooldownFillImage != null)
                {
                    _cooldownFillImage.gameObject.SetActive(true);
                    _cooldownFillImage.fillAmount = displayRatio;
                }

                if (_projectileIcon != null)
                {
                    _projectileIcon.color = _cooldownColor;
                }

                // แสดงข้อความนับถอยหลัง
                string timeStr = _showDecimal ? remaining.ToString("F1") + "s" : Mathf.CeilToInt(remaining).ToString() + "s";

                if (_cooldownTextTMP != null)
                {
                    _cooldownTextTMP.gameObject.SetActive(true);
                    _cooldownTextTMP.text = timeStr;
                }

                if (_cooldownTextLegacy != null)
                {
                    _cooldownTextLegacy.gameObject.SetActive(true);
                    _cooldownTextLegacy.text = timeStr;
                }
            }
            else
            {
                // พร้อมใช้งานแล้ว (Ready)
                if (_wasOnCooldownLastFrame)
                {
                    _wasOnCooldownLastFrame = false;
                    _onCooldownReadyEvent?.Invoke();
                }

                float readyValue = _invertFill ? 0f : 1f;

                if (_cooldownSlider != null)
                {
                    _cooldownSlider.value = readyValue;
                }

                if (_cooldownFillImage != null)
                {
                    _cooldownFillImage.fillAmount = readyValue;
                    if (_invertFill) _cooldownFillImage.gameObject.SetActive(false);
                }

                if (_projectileIcon != null)
                {
                    _projectileIcon.color = _readyColor;
                }

                if (_cooldownTextTMP != null)
                {
                    _cooldownTextTMP.gameObject.SetActive(false);
                }

                if (_cooldownTextLegacy != null)
                {
                    _cooldownTextLegacy.gameObject.SetActive(false);
                }
            }
        }

        private void FindPlayerCombatIfNeeded()
        {
            if (_playerCombat == null)
            {
                var player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    _playerCombat = player.Combat;
                }
                else
                {
                    _playerCombat = FindFirstObjectByType<PlayerCombat>();
                }
            }
        }
    }
}
