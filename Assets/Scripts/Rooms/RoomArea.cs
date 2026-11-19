using System;
using Athena.CameraSystem;
using Athena.Player;
using UnityEngine;

namespace Athena.Rooms
{
    public enum RoomCameraMode
    {
        FreeFollow, // กล้องเดินตามปกติ (Room 1, Corridors)
        Confined    // ล็อคกรอบในห้อง (Room 2, Room 3)
    }

    /// <summary>
    /// กำหนดขอบเขตห้อง (Trigger Zone) และบอกกล้องว่าต้องปรับโหมดเป็น Free Follow หรือ Confined
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class RoomArea : MonoBehaviour
    {
        [Header("Room Info")]
        [SerializeField] private string _roomName = "Room";
        [SerializeField] private RoomCameraMode _cameraMode = RoomCameraMode.Confined;

        [Header("Confiner Settings")]
        [Tooltip("Collider ที่ใช้กำหนดขอบเขตห้อง (ถ้าไม่ใส่จะใช้ Collider บน GameObject นี้)")]
        [SerializeField] private Collider2D _roomBoundsCollider;

        [Header("Player Detection")]
        [SerializeField] private LayerMask _playerLayer;

        public string RoomName => _roomName;
        public RoomCameraMode CameraMode => _cameraMode;
        public Bounds RoomBounds => _roomBoundsCollider != null ? _roomBoundsCollider.bounds : GetComponent<Collider2D>().bounds;

        public static event Action<RoomArea> OnRoomEntered;
        public static event Action<RoomArea> OnRoomExited;

        private void Awake()
        {
            if (_roomBoundsCollider == null)
            {
                _roomBoundsCollider = GetComponent<Collider2D>();
            }

            // ต้องเป็น Trigger
            if (_roomBoundsCollider != null && !_roomBoundsCollider.isTrigger)
            {
                _roomBoundsCollider.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;

            ApplyCameraMode();
            OnRoomEntered?.Invoke(this);

            UnityEngine.Debug.Log($"[RoomArea] Player entered: {_roomName} (Mode: {_cameraMode})");
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;

            OnRoomExited?.Invoke(this);
        }

        /// <summary>
        /// สั่งให้กล้องเปลี่ยนโหมดตามการตั้งค่าของห้องนี้
        /// </summary>
        public void ApplyCameraMode()
        {
            if (CameraController2D.Instance == null) return;

            if (_cameraMode == RoomCameraMode.Confined)
            {
                CameraController2D.Instance.SetConfinedRoom(RoomBounds);
            }
            else
            {
                CameraController2D.Instance.SetFreeFollow();
            }
        }

        private bool IsPlayer(Collider2D col)
        {
            if (_playerLayer != 0 && (_playerLayer.value & (1 << col.gameObject.layer)) != 0)
            {
                return true;
            }

            return col.GetComponent<PlayerController>() != null || col.CompareTag("Player");
        }

        private void OnDrawGizmos()
        {
            Collider2D col = _roomBoundsCollider != null ? _roomBoundsCollider : GetComponent<Collider2D>();
            if (col == null) return;

            Bounds bounds = col.bounds;

            // สีเขียว = Free Follow, สีฟ้า = Confined
            Gizmos.color = (_cameraMode == RoomCameraMode.FreeFollow) 
                ? new Color(0f, 1f, 0.4f, 0.25f) 
                : new Color(0f, 0.8f, 1f, 0.25f);

            Gizmos.DrawCube(bounds.center, bounds.size);

            Gizmos.color = (_cameraMode == RoomCameraMode.FreeFollow) ? Color.green : Color.cyan;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            #if UNITY_EDITOR
            GUIStyle labelStyle = new GUIStyle
            {
                normal = { textColor = (_cameraMode == RoomCameraMode.FreeFollow) ? Color.green : Color.cyan },
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            UnityEditor.Handles.Label(bounds.center + Vector3.up * (bounds.extents.y + 0.5f), $"{_roomName} [{_cameraMode}]", labelStyle);
            #endif
        }
    }
}
