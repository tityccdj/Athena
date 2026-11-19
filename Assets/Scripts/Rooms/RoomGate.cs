using System;
using UnityEngine;

namespace Athena.Rooms
{
    /// <summary>
    /// ประตูกั้นห้อง (เช่น WallWhenboss)
    /// ใช้กั้นขังตอนสู้บอส และเปิดออกเมื่อเปลี่ยนเฟสหรือบอสตาย
    /// </summary>
    public class RoomGate : MonoBehaviour
    {
        [Header("Gate Settings")]
        [SerializeField] private bool _startClosed = false;

        [Header("Wall GameObjects (สลับระหว่าง มีบอส / ไม่มีบอส)")]
        [Tooltip("กำแพงที่เปิดใช้งานตอนมีบอส (กั้นไม่ให้ Player ผ่าน)")]
        [SerializeField] private GameObject _wallWhenBoss;
        [Tooltip("กำแพงหรือทางผ่านที่เปิดใช้งานตอนไม่มีบอส (เปิดทางให้ Player ผ่านได้)")]
        [SerializeField] private GameObject _wallWhenNoBoss;

        [Header("Components (Optional)")]
        [Tooltip("Collider ที่ใช้กั้นไม่ให้ Player เดินผ่าน (ถ้าไม่ใส่จะหาจาก _wallWhenBoss หรือ GameObject นี้)")]
        [SerializeField] private Collider2D _gateCollider;

        public bool IsClosed { get; private set; }

        public event Action<bool> OnGateStateChanged; // true = closed, false = open

        private void Awake()
        {
            if (_gateCollider == null)
            {
                if (_wallWhenBoss != null)
                {
                    _gateCollider = _wallWhenBoss.GetComponent<Collider2D>();
                }
                if (_gateCollider == null)
                {
                    _gateCollider = GetComponent<Collider2D>();
                }
            }

            if (_startClosed)
            {
                CloseGate();
            }
            else
            {
                OpenGate();
            }
        }

        [ContextMenu("Open Gate (No Boss)")]
        public void OpenGate()
        {
            IsClosed = false;

            // สลับสถานะ: ปิด WallWhenBoss, เปิด WallWhenNoBoss
            if (_wallWhenBoss != null)
            {
                _wallWhenBoss.SetActive(false);
            }
            if (_wallWhenNoBoss != null)
            {
                _wallWhenNoBoss.SetActive(true);
            }

            if (_gateCollider != null)
            {
                _gateCollider.enabled = false;
            }

            OnGateStateChanged?.Invoke(false);
            UnityEngine.Debug.Log($"[RoomGate] {gameObject.name}: OPENED (No Boss)");
        }

        [ContextMenu("Close Gate (When Boss)")]
        public void CloseGate()
        {
            IsClosed = true;

            // สลับสถานะ: เปิด WallWhenBoss, ปิด WallWhenNoBoss
            if (_wallWhenBoss != null)
            {
                _wallWhenBoss.SetActive(true);
            }
            if (_wallWhenNoBoss != null)
            {
                _wallWhenNoBoss.SetActive(false);
            }

            if (_gateCollider != null)
            {
                _gateCollider.enabled = true;
            }

            OnGateStateChanged?.Invoke(true);
            UnityEngine.Debug.Log($"[RoomGate] {gameObject.name}: CLOSED (When Boss)");
        }

        private void OnDrawGizmos()
        {
            Collider2D col = _gateCollider != null ? _gateCollider : GetComponent<Collider2D>();
            if (col == null) return;

            Gizmos.color = IsClosed ? new Color(1f, 0f, 0f, 0.5f) : new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        }
    }

    #if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(RoomGate))]
    public class RoomGateEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            RoomGate gate = (RoomGate)target;

            GUILayout.Space(10);
            GUILayout.Label("Testing Controls (Editor / PlayMode)", UnityEditor.EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Open Gate\n(No Boss)", GUILayout.Height(40)))
            {
                gate.OpenGate();
            }

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Close Gate\n(When Boss)", GUILayout.Height(40)))
            {
                gate.CloseGate();
            }
            GUILayout.EndHorizontal();

            GUI.backgroundColor = Color.white;
        }
    }
    #endif
}
