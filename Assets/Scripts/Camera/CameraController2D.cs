using Athena.Player;
using UnityEngine;

namespace Athena.CameraSystem
{
    /// <summary>
    /// ควบคุมการเคลื่อนที่ของกล้อง 2D รองรับทั้ง Free Follow และ Confined (ล็อคกรอบห้อง)
    /// ตาม GDD:
    /// - Room 1 & Transition: กล้องเดินตามปกติ (Free Follow)
    /// - Room 2 & Room 3: กล้อง Lock กรอบในห้อง (Confined)
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController2D : MonoBehaviour
    {
        public static CameraController2D Instance { get; private set; }

        [Header("Target Follow")]
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _offset = new Vector3(0f, 1.2f, -10f);
        [SerializeField] private float _smoothTime = 0.2f;

        [Header("Room Confiner State (Read-Only)")]
        [SerializeField] private bool _isConfined = false;
        [SerializeField] private Bounds _currentRoomBounds;

        private Camera _cam;
        private Vector3 _currentVelocity = Vector3.zero;
        private float _defaultOrthographicSize = 5f;
        private Coroutine _winZoomCoroutine;

        public bool IsConfined => _isConfined;
        public Bounds CurrentRoomBounds => _currentRoomBounds;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _cam = GetComponent<Camera>();
            if (_cam != null)
            {
                _defaultOrthographicSize = _cam.orthographicSize;
            }

            // Auto-find player if not assigned
            if (_target == null)
            {
                var player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    _target = player.transform;
                }
            }
        }

        private void LateUpdate()
        {
            if (_target == null || _cam == null) return;

            // 1. คำนวณตำแหน่งปลายทางที่ต้องการตาม Player
            Vector3 desiredPos = _target.position + _offset;

            // 2. ถ้าอยู่ในโหมด Confined (ล็อคกรอบห้อง) ให้ clamp ตำแหน่งกล้องไม่ให้ล้นขอบห้อง
            if (_isConfined && _currentRoomBounds.size != Vector3.zero)
            {
                desiredPos = ClampPositionToBounds(desiredPos);
            }

            // 3. เคลื่อนที่กล้องแบบ Smooth
            Vector3 smoothedPos = Vector3.SmoothDamp(transform.position, desiredPos, ref _currentVelocity, _smoothTime);
            transform.position = new Vector3(smoothedPos.x, smoothedPos.y, _offset.z);
        }

        /// <summary>
        /// คำนวณ Clamp ขอบเขตกล้องตามขนาด Viewport (Orthographic Size & Aspect Ratio)
        /// </summary>
        private Vector3 ClampPositionToBounds(Vector3 targetPos)
        {
            float camHalfHeight = _cam.orthographicSize;
            float camHalfWidth = camHalfHeight * _cam.aspect;

            float minX = _currentRoomBounds.min.x + camHalfWidth;
            float maxX = _currentRoomBounds.max.x - camHalfWidth;
            float minY = _currentRoomBounds.min.y + camHalfHeight;
            float maxY = _currentRoomBounds.max.y - camHalfHeight;

            // หากห้องมีขนาดเล็กกว่าหน้าจอกล้อง ให้จัดตำแหน่งไว้กึ่งกลางห้อง
            float clampedX = (minX > maxX) ? _currentRoomBounds.center.x : Mathf.Clamp(targetPos.x, minX, maxX);
            float clampedY = (minY > maxY) ? _currentRoomBounds.center.y : Mathf.Clamp(targetPos.y, minY, maxY);

            return new Vector3(clampedX, clampedY, targetPos.z);
        }

        /// <summary>
        /// สลับเข้าสู่โหมดล็อคกรอบห้อง (ใช้สำหรับ Room 2 และ Room 3)
        /// </summary>
        public void SetConfinedRoom(Bounds roomBounds)
        {
            _currentRoomBounds = roomBounds;
            _isConfined = true;
        }

        /// <summary>
        /// สลับเข้าสู่โหมด Free Follow เดินตามปกติ (ใช้สำหรับ Room 1 และทางเชื่อมระหว่างห้อง)
        /// </summary>
        public void SetFreeFollow()
        {
            _isConfined = false;
        }

        /// <summary>
        /// กำหนดเป้าหมายใหม่ให้กล้องติดตาม
        /// </summary>
        public void SetTarget(Transform target)
        {
            _target = target;
        }

        /// <summary>
        /// วาร์ปกล้องไปยังตำแหน่งเป้าหมายทันทีโดยไม่ต้องรอ Smooth (เช่น ตอนเริ่มเกมหรือ Respawn)
        /// </summary>
        public void SnapToTarget()
        {
            if (_target == null) return;
            Vector3 desiredPos = _target.position + _offset;
            if (_isConfined && _currentRoomBounds.size != Vector3.zero)
            {
                desiredPos = ClampPositionToBounds(desiredPos);
            }
            transform.position = new Vector3(desiredPos.x, desiredPos.y, _offset.z);
            _currentVelocity = Vector3.zero;
        }

        /// <summary>
        /// GDD Win State: ซูมกล้องเข้าไปหาร่างบอสพร้อม Slow-motion และค่อยๆ ซูมออกกลับสู่ปกติ
        /// </summary>
        public void TriggerWinZoom(Transform focusTarget, float targetZoomSize = 3.2f, float holdDuration = 1.5f)
        {
            if (_winZoomCoroutine != null) StopCoroutine(_winZoomCoroutine);
            _winZoomCoroutine = StartCoroutine(WinZoomRoutine(focusTarget, targetZoomSize, holdDuration));
        }

        private System.Collections.IEnumerator WinZoomRoutine(Transform focusTarget, float targetZoomSize, float holdDuration)
        {
            Transform originalTarget = _target;
            if (focusTarget != null) _target = focusTarget;

            // 1. Zoom-In อย่างนุ่มนวล
            float zoomSpeed = 0.5f;
            float elapsed = 0f;
            float startSize = _cam.orthographicSize;

            while (elapsed < zoomSpeed)
            {
                elapsed += Time.unscaledDeltaTime;
                _cam.orthographicSize = Mathf.Lerp(startSize, targetZoomSize, elapsed / zoomSpeed);
                yield return null;
            }
            _cam.orthographicSize = targetZoomSize;

            // 2. แช่ภาพตามระยะเวลา holdDuration
            yield return new WaitForSecondsRealtime(holdDuration);

            // 3. Zoom-Out ค่อยๆ ซูมกลับมาเป็นขนาดเดิม
            elapsed = 0f;
            float returnSpeed = 1.2f;
            while (elapsed < returnSpeed)
            {
                elapsed += Time.unscaledDeltaTime;
                _cam.orthographicSize = Mathf.Lerp(targetZoomSize, _defaultOrthographicSize, elapsed / returnSpeed);
                yield return null;
            }
            _cam.orthographicSize = _defaultOrthographicSize;

            if (originalTarget != null) _target = originalTarget;
            _winZoomCoroutine = null;
        }
    }
}
