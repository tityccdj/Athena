using UnityEngine;

namespace Athena.Boss.Phase1
{
    /// <summary>
    /// รับ Animation Events จาก Animator ของ Orc Rider (Visual child) แล้วส่งต่อให้ Controller และ Animation
    /// 
    /// วาง Component นี้ไว้ที่ GameObject เดียวกับ Animator (Visual child)
    /// 
    /// ฟังก์ชัน Animation Events ที่เลือกใช้ได้ใน Animation Window:
    /// - "OnTelegraphStart" : เริ่มแสดงเอฟเฟกต์กะพริบชาร์จเตือน (เริ่มที่เฟรมเริ่มต้นของท่า)
    /// - "OnTelegraphEnd"   : สิ้นสุดช่วงชาร์จเตือน หยุดกะพริบ (และเริ่มพุ่งตัวสำหรับท่า Dash)
    /// - "OnHitboxOpen"     : เปิด Hitbox ในเฟรมที่อาวุธฟันโดน
    /// - "OnHitboxClose"    : ปิด Hitbox ในเฟรมที่อาวุธฟันเสร็จ
    /// - "OnAttackComplete" : สิ้นสุดท่าโจมตี เข้าสู่จังหวะ Recovery
    /// </summary>
    public class OrcRiderAnimationEventReceiver : MonoBehaviour
    {
        private OrcRiderBossController _controller;
        private OrcRiderAnimation _anim;

        public void Initialize(OrcRiderBossController controller, OrcRiderAnimation anim)
        {
            _controller = controller;
            _anim = anim;
        }

        private void Awake()
        {
            if (_anim == null) _anim = GetComponent<OrcRiderAnimation>();
            if (_controller == null) _controller = GetComponentInParent<OrcRiderBossController>();
        }

        /// <summary>
        /// Animation Event: เริ่มกะพริบชาร์จเตือน
        /// </summary>
        public void OnTelegraphStart()
        {
            _anim?.StartTelegraphFlash();
        }

        /// <summary>
        /// Animation Event: สิ้นสุดช่วงชาร์จเตือน (หยุดกะพริบ และพุ่งตัวสำหรับ Dash)
        /// </summary>
        public void OnTelegraphEnd()
        {
            _anim?.StopTelegraph();
            _controller?.AnimEvent_OnTelegraphEnd();
        }

        /// <summary>
        /// Animation Event: เปิด Hitbox ในเฟรมที่ฟัน
        /// </summary>
        public void OnHitboxOpen()
        {
            _controller?.AnimEvent_OpenHitbox();
        }

        /// <summary>
        /// Animation Event: ปิด Hitbox ในเฟรมที่ฟันเสร็จ
        /// </summary>
        public void OnHitboxClose()
        {
            _controller?.AnimEvent_CloseHitbox();
        }

        /// <summary>
        /// Animation Event: จบท่าโจมตี เข้าสู่ Recovery
        /// </summary>
        public void OnAttackComplete()
        {
            _anim?.StopTelegraph();
            _controller?.AnimEvent_OnAttackComplete();
        }

        /// <summary>
        /// Animation Event: เล่นท่าตายจบแล้ว สั่งเปลี่ยนเฟสทันที
        /// </summary>
        public void OnDeathComplete()
        {
            _controller?.AnimEvent_OnDeathComplete();
        }
    }
}
