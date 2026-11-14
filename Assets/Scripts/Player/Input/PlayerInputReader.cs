using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Athena.Player.Input
{
    [DefaultExecutionOrder(-100)]
    public class PlayerInputReader : MonoBehaviour, IPlayerInput
    {
        [Header("Buffer Window (GDD: 0.1 - 0.2s)")]
        [Range(0.1f, 0.2f)]
        [SerializeField] private float _bufferDuration = 0.15f;

        private InputBuffer _jumpBuffer;
        private InputBuffer _dashBuffer;
        private InputBuffer _attackBuffer;
        private InputBuffer _healBuffer;

        private Vector2 _moveInput;
        private bool _isJumpHeld;

        public Vector2 MoveInput => _moveInput;
        public bool IsJumpHeld => _isJumpHeld;

        public bool HasBufferedJump => _jumpBuffer.IsBuffered;
        public bool HasBufferedDash => _dashBuffer.IsBuffered;
        public bool HasBufferedAttack => _attackBuffer.IsBuffered;
        public bool HasBufferedHeal => _healBuffer.IsBuffered;

        public event Action OnJumpPressed;
        public event Action OnJumpReleased;
        public event Action OnDashPressed;
        public event Action OnAttackPressed;
        public event Action OnHealPressed;

        private void Awake()
        {
            _jumpBuffer = new InputBuffer(_bufferDuration);
            _dashBuffer = new InputBuffer(_bufferDuration);
            _attackBuffer = new InputBuffer(_bufferDuration);
            _healBuffer = new InputBuffer(_bufferDuration);
        }

        private void Update()
        {
            ReadInputs();
        }

        private void ReadInputs()
        {
            Vector2 move = Vector2.zero;
            bool jumpDown = false;
            bool jumpUp = false;
            bool dashDown = false;
            bool attackDown = false;
            bool healDown = false;

            // Keyboard support
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;

                if (keyboard.spaceKey.wasPressedThisFrame) jumpDown = true;
                if (keyboard.spaceKey.wasReleasedThisFrame) jumpUp = true;
                _isJumpHeld = keyboard.spaceKey.isPressed;

                if (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.cKey.wasPressedThisFrame) dashDown = true;
                if (keyboard.jKey.wasPressedThisFrame) attackDown = true;
                if (keyboard.hKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame) healDown = true;
            }

            // Mouse support for attack
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) attackDown = true;
            }

            // Gamepad support
            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                Vector2 stick = gamepad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.04f)
                {
                    move = stick;
                }
                else
                {
                    Vector2 dpad = gamepad.dpad.ReadValue();
                    if (dpad.sqrMagnitude > 0.04f) move = dpad;
                }

                if (gamepad.buttonSouth.wasPressedThisFrame) jumpDown = true;
                if (gamepad.buttonSouth.wasReleasedThisFrame) jumpUp = true;
                if (gamepad.buttonSouth.isPressed) _isJumpHeld = true;

                if (gamepad.rightTrigger.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame) dashDown = true;
                if (gamepad.buttonWest.wasPressedThisFrame) attackDown = true;
                if (gamepad.buttonNorth.wasPressedThisFrame || gamepad.leftShoulder.wasPressedThisFrame) healDown = true;
            }

            _moveInput = Vector2.ClampMagnitude(move, 1f);

            // Buffer processing
            if (jumpDown)
            {
                _jumpBuffer.Trigger();
                OnJumpPressed?.Invoke();
            }

            if (jumpUp)
            {
                OnJumpReleased?.Invoke();
            }

            if (dashDown)
            {
                _dashBuffer.Trigger();
                OnDashPressed?.Invoke();
            }

            if (attackDown)
            {
                _attackBuffer.Trigger();
                OnAttackPressed?.Invoke();
            }

            if (healDown)
            {
                _healBuffer.Trigger();
                OnHealPressed?.Invoke();
            }
        }

        public bool ConsumeJumpBuffer() => _jumpBuffer.Consume();
        public bool ConsumeDashBuffer() => _dashBuffer.Consume();
        public bool ConsumeAttackBuffer() => _attackBuffer.Consume();
        public bool ConsumeHealBuffer() => _healBuffer.Consume();
    }
}

