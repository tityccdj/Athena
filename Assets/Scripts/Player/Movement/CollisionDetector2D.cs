using UnityEngine;

namespace Athena.Player.Movement
{
    public class CollisionDetector2D : MonoBehaviour
    {
        [Header("Ground Check")]
        [SerializeField] private Transform _groundCheckPoint;
        [SerializeField] private Vector2 _groundCheckSize = new Vector2(0.6f, 0.1f);
        [SerializeField] private LayerMask _groundLayer;

        [Header("Wall Check")]
        [SerializeField] private Transform _wallCheckPoint;
        [SerializeField] private Vector2 _wallCheckSize = new Vector2(0.1f, 0.8f);
        [SerializeField] private LayerMask _wallLayer;
        [SerializeField] private float _wallCheckDistance = 0.4f;

        public bool IsGrounded { get; private set; }
        public bool IsTouchingWall { get; private set; }
        public int WallDirection { get; private set; } // -1 = Left, 1 = Right, 0 = None

        private void FixedUpdate()
        {
            CheckCollisions();
        }

        private void CheckCollisions()
        {
            Vector2 origin = _groundCheckPoint != null ? _groundCheckPoint.position : transform.position;
            IsGrounded = Physics2D.OverlapBox(origin, _groundCheckSize, 0f, _groundLayer);

            Vector2 wallOrigin = _wallCheckPoint != null ? _wallCheckPoint.position : transform.position;
            
            // Check Right Wall
            bool rightWall = Physics2D.Raycast(wallOrigin, Vector2.right, _wallCheckDistance, _wallLayer);
            // Check Left Wall
            bool leftWall = Physics2D.Raycast(wallOrigin, Vector2.left, _wallCheckDistance, _wallLayer);

            if (rightWall)
            {
                IsTouchingWall = true;
                WallDirection = 1;
            }
            else if (leftWall)
            {
                IsTouchingWall = true;
                WallDirection = -1;
            }
            else
            {
                IsTouchingWall = false;
                WallDirection = 0;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Vector2 groundOrigin = _groundCheckPoint != null ? _groundCheckPoint.position : transform.position;
            Gizmos.DrawWireCube(groundOrigin, _groundCheckSize);

            Gizmos.color = IsTouchingWall ? Color.cyan : Color.yellow;
            Vector2 wallOrigin = _wallCheckPoint != null ? _wallCheckPoint.position : transform.position;
            Gizmos.DrawLine(wallOrigin, wallOrigin + Vector2.right * _wallCheckDistance);
            Gizmos.DrawLine(wallOrigin, wallOrigin + Vector2.left * _wallCheckDistance);
        }
    }
}

