using System;
using System.Collections;
using System.Collections.Generic;
using Athena.Rooms;
using UnityEngine;

namespace Athena.Boss.Core
{
    /// <summary>
    /// ควบคุมการบินของวิญญาณบอส (Ghost) จาก Room 2 ไป Room 3 เมื่อ Phase 2 ตาย
    /// ตาม GDD:
    /// - "เมื่อตาย ร่างจะกลายเป็นผีและบินไป Room 3. บอสจะติด I-Frame ตลอดทาง
    ///    จนกว่า Player จะวิ่งตามไปถึง Room 3 และแสดง Animation เปิดตัวเสร็จ"
    /// - ประตูกั้นห้อง RoomGate จะเปิดออกเพื่อให้ Player วิ่งตามไป
    /// </summary>
    public class BossGhostTransition : MonoBehaviour
    {
        [Header("Ghost Visual")]
        [SerializeField] private SpriteRenderer _ghostSprite;
        [SerializeField] private Color _ghostColor = new Color(0.4f, 0.85f, 1f, 0.75f);
        [SerializeField] private float _pulseSpeed = 4f;

        [Header("Flight Movement")]
        [SerializeField] private float _flightSpeed = 9f;
        [SerializeField] private float _floatBobAmplitude = 0.5f;
        [SerializeField] private float _floatBobFrequency = 3f;

        [Header("Target Room")]
        [SerializeField] private string _room3Name = "Room3_Boss";

        private Vector3 _destination;
        private bool _isFlying = false;
        private bool _hasArrivedAtRoom3 = false;
        private List<Vector3> _waypoints = new List<Vector3>();
        private int _currentWaypointIndex = 0;

        public bool HasArrivedAtRoom3 => _hasArrivedAtRoom3;

        private void Awake()
        {
            if (_ghostSprite == null)
            {
                _ghostSprite = GetComponentInChildren<SpriteRenderer>();
            }

            if (_ghostSprite != null)
            {
                _ghostSprite.color = _ghostColor;
            }
        }

        private void OnEnable()
        {
            RoomArea.OnRoomEntered += HandleRoomEntered;
        }

        private void OnDisable()
        {
            RoomArea.OnRoomEntered -= HandleRoomEntered;
        }

        /// <summary>
        /// เริ่มต้นการบินของวิญญาณจากจุดที่บอส Phase 2 ตาย ไปยัง Room 3
        /// </summary>
        public void StartFlight(Vector3 startPos, Vector3 targetPos, List<Vector3> corridorWaypoints = null)
        {
            transform.position = startPos;
            _destination = targetPos;
            _waypoints.Clear();

            if (corridorWaypoints != null && corridorWaypoints.Count > 0)
            {
                _waypoints.AddRange(corridorWaypoints);
            }
            _waypoints.Add(targetPos);

            _currentWaypointIndex = 0;
            _isFlying = true;
            _hasArrivedAtRoom3 = false;

            gameObject.SetActive(true);
            StartCoroutine(FlightRoutine());
        }

        private IEnumerator FlightRoutine()
        {
            while (_currentWaypointIndex < _waypoints.Count)
            {
                Vector3 currentTarget = _waypoints[_currentWaypointIndex];

                while (Vector2.Distance(transform.position, currentTarget) > 0.3f)
                {
                    // เคลื่อนที่ไปตาม Waypoint พร้อม Sine Wave ลอยขึ้น-ลงแบบวิญญาณ
                    float bob = Mathf.Sin(Time.time * _floatBobFrequency) * _floatBobAmplitude;
                    Vector3 moveTarget = new Vector3(currentTarget.x, currentTarget.y + bob, currentTarget.z);

                    transform.position = Vector3.MoveTowards(transform.position, moveTarget, _flightSpeed * Time.deltaTime);

                    // Pulse ค่าความโปร่งแสงของผี
                    if (_ghostSprite != null)
                    {
                        float alpha = Mathf.Lerp(0.5f, 0.9f, (Mathf.Sin(Time.time * _pulseSpeed) + 1f) * 0.5f);
                        Color c = _ghostColor;
                        c.a = alpha;
                        _ghostSprite.color = c;
                    }

                    yield return null;
                }

                _currentWaypointIndex++;
            }

            _isFlying = false;
            _hasArrivedAtRoom3 = true;
            UnityEngine.Debug.Log("[BossGhostTransition] Ghost arrived in Room 3! Waiting for Player...");

            // ลอยนิ่งรอใน Room 3 จนกว่า Player จะเข้ามา
            while (!_isFlying && gameObject.activeInHierarchy)
            {
                float bob = Mathf.Sin(Time.time * _floatBobFrequency) * _floatBobAmplitude;
                transform.position = new Vector3(_destination.x, _destination.y + bob, _destination.z);
                yield return null;
            }
        }

        private void HandleRoomEntered(RoomArea room)
        {
            // เมื่อ Player ก้าวเข้าสู่ Room 3
            if (room != null && (room.RoomName.Equals(_room3Name, StringComparison.OrdinalIgnoreCase) || room.RoomName.Contains("Room3")))
            {
                UnityEngine.Debug.Log("[BossGhostTransition] Player entered Room 3! Manifesting Phase 3 Necromancer...");
                ManifestPhase3();
            }
        }

        /// <summary>
        /// แปลงร่างจากวิญญาณเป็น Necromancer (Phase 3)
        /// </summary>
        public void ManifestPhase3()
        {
            gameObject.SetActive(false);

            if (BossManager.Instance != null)
            {
                BossManager.Instance.StartPhase3FromGhost();
            }
        }
    }
}
