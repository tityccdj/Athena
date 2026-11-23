using UnityEngine;

namespace Athena.Boss.Phase3
{
    /// <summary>
    /// รับ Event จาก Animation Clip ของบอส Phase 3 (Necromancer) และส่งต่อไปยัง Controller
    /// </summary>
    public class NecromancerAnimationEventReceiver : MonoBehaviour
    {
        private NecromancerBossController _controller;
        private NecromancerAnimation _animation;

        public void Initialize(NecromancerBossController controller, NecromancerAnimation animation)
        {
            _controller = controller;
            _animation = animation;
        }

        public void OnTelegraphStart()
        {
            // เริ่มต้นกะพริบชาร์จเตือน
            if (_animation != null) _animation.StartTelegraphFlash();
            if (_controller != null) _controller.AnimEvent_OnSpawnTelegraph();
        }

        /// <summary>
        /// Animation Event: สั่งเสก Prefab วงเตือนระเบิด (Explosion Telegraph)
        /// </summary>
        public void OnSpawnTelegraph()
        {
            if (_controller != null) _controller.AnimEvent_OnSpawnTelegraph();
        }

        public void OnTelegraphEnd()
        {
            if (_controller != null) _controller.AnimEvent_OnTelegraphEnd();
        }

        public void OnHitboxOpen()
        {
            if (_controller != null) _controller.AnimEvent_OpenHitbox();
        }

        public void OnHitboxClose()
        {
            if (_controller != null) _controller.AnimEvent_CloseHitbox();
        }

        /// <summary>
        /// Animation Event: เรียก Prefab Explosion มาระเบิดตรงจุดที่เตือนไว้
        /// </summary>
        public void OnFireExplosion()
        {
            if (_controller != null) _controller.AnimEvent_OnFireExplosion();
        }

        public void OnExplosion()
        {
            if (_controller != null) _controller.AnimEvent_OnFireExplosion();
        }

        // รองรับชื่อเดิม OnFireMeteor เผื่อคลิปอนิเมชันเดิมเรียกใช้
        public void OnFireMeteor()
        {
            if (_controller != null) _controller.AnimEvent_OnFireExplosion();
        }

        public void OnAttackComplete()
        {
            if (_controller != null) _controller.AnimEvent_OnAttackComplete();
        }

        public void OnIntroComplete()
        {
            if (_controller != null) _controller.AnimEvent_OnIntroComplete();
        }

        public void OnDeathComplete()
        {
            if (_controller != null) _controller.AnimEvent_OnDeathComplete();
        }
    }
}
