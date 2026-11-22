using UnityEngine;

namespace Athena.Boss.Phase2
{
    /// <summary>
    /// รับ Animation Events จาก Animator ของ Elite Orc (Visual child) แล้วส่งต่อให้ Controller และ Animation
    /// 
    /// วาง Component นี้ไว้ที่ GameObject เดียวกับ Animator (Visual child)
    /// 
    /// ฟังก์ชัน Animation Events:
    /// - "OnTelegraphStart" : เริ่มกะพริบชาร์จเตือน
    /// - "OnTelegraphEnd"   : สิ้นสุดช่วงชาร์จเตือน (เริ่มพุ่งตัวสำหรับท่า Spin Tornado)
    /// - "OnHitboxOpen"     : เปิด Hitbox ฟัน
    /// - "OnHitboxClose"    : ปิด Hitbox
    /// - "OnFireShockwave"  : ปล่อยคลื่น Shockwave คลื่นดินพุ่งไปข้างหน้า (สำหรับ Attack03)
    /// - "OnAttackComplete" : จบท่าโจมตี เข้าสู่ Recovery
    /// - "OnDeathComplete"  : เล่นท่าตายจบแล้ว สั่งเปลี่ยนเฟสเข้าสู่ Phase 3
    /// </summary>
    public class EliteOrcAnimationEventReceiver : MonoBehaviour
    {
        private EliteOrcBossController _controller;
        private EliteOrcAnimation _anim;

        public void Initialize(EliteOrcBossController controller, EliteOrcAnimation anim)
        {
            _controller = controller;
            _anim = anim;
        }

        private void Awake()
        {
            if (_anim == null) _anim = GetComponent<EliteOrcAnimation>();
            if (_controller == null) _controller = GetComponentInParent<EliteOrcBossController>();
        }

        public void OnTelegraphStart()
        {
            _anim?.StartTelegraphFlash();
        }

        public void OnTelegraphEnd()
        {
            _anim?.StopTelegraph();
            _controller?.AnimEvent_OnTelegraphEnd();
        }

        public void OnHitboxOpen()
        {
            _controller?.AnimEvent_OpenHitbox();
        }

        public void OnHitboxClose()
        {
            _controller?.AnimEvent_CloseHitbox();
        }

        public void OnFireShockwave()
        {
            _controller?.AnimEvent_OnFireShockwave();
        }

        public void OnAttackComplete()
        {
            _anim?.StopTelegraph();
            _controller?.AnimEvent_OnAttackComplete();
        }

        public void OnDeathComplete()
        {
            _controller?.AnimEvent_OnDeathComplete();
        }
    }
}
