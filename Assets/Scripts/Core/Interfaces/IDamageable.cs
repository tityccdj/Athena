using Athena.Core;

namespace Athena.Core.Interfaces
{
    public interface IDamageable
    {
        void TakeDamage(DamageInfo info);
        bool IsInvulnerable { get; }
    }
}
