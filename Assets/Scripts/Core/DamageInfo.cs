using UnityEngine;

namespace Athena.Core
{
    public struct DamageInfo
    {
        public int Amount { get; }
        public Vector2 HitPoint { get; }
        public Vector2 KnockbackDirection { get; }
        public float KnockbackForce { get; }
        public GameObject Source { get; }

        public DamageInfo(int amount, Vector2 hitPoint, Vector2 knockbackDirection, float knockbackForce = 0f, GameObject source = null)
        {
            Amount = amount;
            HitPoint = hitPoint;
            KnockbackDirection = knockbackDirection;
            KnockbackForce = knockbackForce;
            Source = source;
        }
    }
}

