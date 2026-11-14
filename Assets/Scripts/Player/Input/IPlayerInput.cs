using System;
using UnityEngine;

namespace Athena.Player.Input
{
    public interface IPlayerInput
    {
        Vector2 MoveInput { get; }
        bool IsJumpHeld { get; }

        bool HasBufferedJump { get; }
        bool HasBufferedDash { get; }
        bool HasBufferedAttack { get; }
        bool HasBufferedHeal { get; }

        bool ConsumeJumpBuffer();
        bool ConsumeDashBuffer();
        bool ConsumeAttackBuffer();
        bool ConsumeHealBuffer();

        event Action OnJumpPressed;
        event Action OnJumpReleased;
        event Action OnDashPressed;
        event Action OnAttackPressed;
        event Action OnHealPressed;
    }
}

