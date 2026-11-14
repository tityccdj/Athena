using UnityEngine;

namespace Athena.Player.Input
{
    [System.Serializable]
    public class InputBuffer
    {
        [SerializeField] private float _bufferWindow = 0.15f;
        private float _lastInputTime = -999f;

        public InputBuffer(float bufferWindow = 0.15f)
        {
            _bufferWindow = Mathf.Clamp(bufferWindow, 0.05f, 0.5f);
        }

        public void SetBufferWindow(float window)
        {
            _bufferWindow = window;
        }

        public void Trigger()
        {
            _lastInputTime = Time.time;
        }

        public bool IsBuffered => (Time.time - _lastInputTime) <= _bufferWindow;

        public bool Consume()
        {
            if (IsBuffered)
            {
                _lastInputTime = -999f;
                return true;
            }
            return false;
        }

        public void Clear()
        {
            _lastInputTime = -999f;
        }
    }
}

