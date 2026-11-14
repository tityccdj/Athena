using UnityEngine;

namespace Athena.Core.Interfaces
{
    public interface IKnockbackable
    {
        void ApplyKnockback(Vector2 direction, float force);
    }
}

