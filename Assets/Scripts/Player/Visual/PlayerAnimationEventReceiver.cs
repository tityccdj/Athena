using Athena.Player.Combat;
using UnityEngine;

namespace Athena.Player.Visual
{
    /// <summary>
    /// รับ Animation Events จาก Animator แล้วส่งต่อคำสั่งไปยัง PlayerCombat
    ///
    /// วิธีใช้ใน Unity Animation Window:
    ///   1. เลือก frame ที่ต้องการใน Attack1.anim / Attack2.anim / Dash-Attack.anim
    ///   2. Add Event → เลือก function:
    ///      - "OnHitboxOpen"  → เปิด hitbox (ตอนที่ sword sweep เริ่ม)
    ///      - "OnHitboxClose" → ปิด hitbox (ตอนที่ sword sweep จบ)
    ///      - "OnFireProjectile" → ยิง projectile (เฉพาะ Dash-Attack)
    ///
    /// Component นี้ต้องอยู่บน GameObject เดียวกับ Animator (Visual child)
    /// </summary>
    public class PlayerAnimationEventReceiver : MonoBehaviour
    {
        private PlayerCombat _combat;

        /// <summary>
        /// เรียกจาก PlayerController หลัง Initialize ทุกอย่างแล้ว
        /// </summary>
        public void Initialize(PlayerCombat combat)
        {
            _combat = combat;
        }

        // ─── Animation Event Callbacks ────────────────────────────────────────────

        /// <summary>
        /// Animation Event: เรียกในช่วงที่ hitbox ควรเปิด (ดาบแกว่งถูก)
        /// </summary>
        public void OnHitboxOpen()
        {
            _combat?.AnimEvent_OpenHitbox();
        }

        /// <summary>
        /// Animation Event: เรียกเมื่อ hitbox ควรปิด (ดาบแกว่งผ่านแล้ว)
        /// </summary>
        public void OnHitboxClose()
        {
            _combat?.AnimEvent_CloseHitbox();
        }

        /// <summary>
        /// Animation Event: เรียกในช่วงที่ควรยิง projectile (Dash Attack เท่านั้น)
        /// </summary>
        public void OnFireProjectile()
        {
            _combat?.AnimEvent_FireProjectile();
        }
    }
}
