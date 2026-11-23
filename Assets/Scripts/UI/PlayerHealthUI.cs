using System.Collections.Generic;
using Athena.Player;
using Athena.Player.Health;
using UnityEngine;
using UnityEngine.UI;

namespace Athena.UI
{
    /// <summary>
    /// ควบคุมการแสดงผลหลอดเลือดของ Player (เป็นไอคอนหัวใจ/ขีดเลือดตามจำนวน HP ที่เหลือ)
    /// ตาม GDD: Max HP = 5 หน่วย (โดนโจมตีลดทีละ 1)
    /// ทำงานร่วมกับ HorizontalLayoutGroup ใน Unity UI
    /// </summary>
    public class PlayerHealthUI : MonoBehaviour
    {
        [Header("Player Health Source")]
        [Tooltip("ถ้าไม่ใส่ จะค้นหา PlayerHealth ในฉากให้อัตโนมัติ")]
        [SerializeField] private PlayerHealth _playerHealth;

        [Header("Hearts Container")]
        [Tooltip("Transform ที่มี HorizontalLayoutGroup (ถ้าไม่ใส่จะใช้ transform ของ GameObject นี้)")]
        [SerializeField] private Transform _heartsContainer;

        [Header("Heart Icons (กำหนดล่วงหน้า)")]
        [Tooltip("ลาก Image หัวใจแต่ละดวง (เช่น Heart_1 .. Heart_5) มาใส่ที่นี่ หรือปล่อยว่างเพื่อให้ค้นหา Child อัตโนมัติ")]
        [SerializeField] private List<Image> _heartImages = new List<Image>();

        [Header("Optional Sprites (ถ้าต้องการสลับภาพแทนการซ่อน)")]
        [Tooltip("ภาพหัวใจเต็ม")]
        [SerializeField] private Sprite _fullHeartSprite;
        [Tooltip("ภาพหัวใจว่าง/หัวใจแตก (ถ้าใส่ จะเปลี่ยน Sprite แทนการ SetActive false)")]
        [SerializeField] private Sprite _emptyHeartSprite;

        [Header("Optional Heart Prefab (สำหรับ Instantiate อัตโนมัติ)")]
        [Tooltip("ถ้ามี Prefab หัวใจ สามารถใส่ที่นี่เพื่อให้สร้างตาม Max HP อัตโนมัติได้")]
        [SerializeField] private GameObject _heartPrefab;

        private void Awake()
        {
            if (_heartsContainer == null)
            {
                _heartsContainer = transform;
            }

            // ถ้าไม่ได้ลาก Image มาใส่ ให้ดึง Image จากลูกๆ ใน Container มาเก็บไว้
            if (_heartImages.Count == 0 && _heartPrefab == null)
            {
                Image[] children = _heartsContainer.GetComponentsInChildren<Image>(true);
                foreach (var img in children)
                {
                    // ไม่นับ Image ที่อยู่บนตัว Container เอง
                    if (img.gameObject != _heartsContainer.gameObject)
                    {
                        _heartImages.Add(img);
                    }
                }
            }
        }

        private void Start()
        {
            FindPlayerHealthIfNeeded();

            if (_playerHealth != null)
            {
                _playerHealth.OnHealthChanged += UpdateHearts;
                UpdateHearts(_playerHealth.CurrentHealth, _playerHealth.MaxHealth);
            }
        }

        private void OnEnable()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnHealthChanged -= UpdateHearts;
                _playerHealth.OnHealthChanged += UpdateHearts;
                UpdateHearts(_playerHealth.CurrentHealth, _playerHealth.MaxHealth);
            }
        }

        private void OnDisable()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnHealthChanged -= UpdateHearts;
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

        /// <summary>
        /// อัปเดตการแสดงผลหัวใจตาม HP ปัจจุบัน
        /// </summary>
        public void UpdateHearts(int currentHealth, int maxHealth)
        {
            // กรณีใช้ Prefab ในการสร้างหัวใจแบบ Dynamic
            if (_heartPrefab != null)
            {
                EnsurePrefabHeartsCount(maxHealth);
            }

            // อัปเดตสถานะของแต่ละไอคอน
            for (int i = 0; i < _heartImages.Count; i++)
            {
                if (_heartImages[i] == null) continue;

                bool isAlive = i < currentHealth;

                if (_fullHeartSprite != null && _emptyHeartSprite != null)
                {
                    _heartImages[i].gameObject.SetActive(i < maxHealth);
                    _heartImages[i].sprite = isAlive ? _fullHeartSprite : _emptyHeartSprite;
                }
                else
                {
                    // โหมดเปิด-ปิด GameObject ตามจำนวนเลือดที่เหลือ
                    _heartImages[i].gameObject.SetActive(isAlive);
                }
            }
        }

        private void EnsurePrefabHeartsCount(int targetCount)
        {
            while (_heartImages.Count < targetCount)
            {
                GameObject newHeart = Instantiate(_heartPrefab, _heartsContainer);
                Image img = newHeart.GetComponent<Image>();
                if (img == null) img = newHeart.GetComponentInChildren<Image>();
                if (img != null)
                {
                    _heartImages.Add(img);
                }
            }
        }
    }
}
