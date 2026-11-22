using System.Collections;
using UnityEngine;

namespace Athena.Boss.Phase2
{
    /// <summary>
    /// ควบคุม Animation และ Telegraph Flash ของบอส Phase 2 (Elite Orc)
    /// </summary>
    public class EliteOrcAnimation : MonoBehaviour
    {
        [Header("Animator State Names")]
        [SerializeField] private string _idleState = "Idle";
        [SerializeField] private string _walkState = "Walk";
        [SerializeField] private string _attack01State = "Attack01";
        [SerializeField] private string _attack02State = "Attack02";
        [SerializeField] private string _attack03State = "Attack03";
        [SerializeField] private string _hurtState = "Hurt";
        [SerializeField] private string _deathState = "Death";

        [Header("Telegraph Flash Settings (GDD: ตัวกะพริบเพื่อเตือน)")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Color _telegraphColor = new Color(1f, 0.9f, 0.2f, 1f); // สีเหลืองเตือน
        [SerializeField] private float _flashInterval = 0.1f;

        private Animator _animator;
        private Color _originalColor;
        private Coroutine _telegraphCoroutine;

        private int _idleHash;
        private int _walkHash;
        private int _attack01Hash;
        private int _attack02Hash;
        private int _attack03Hash;
        private int _hurtHash;
        private int _deathHash;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (_spriteRenderer != null)
            {
                _originalColor = _spriteRenderer.color;
            }

            InitializeHashes();
        }

        private void InitializeHashes()
        {
            _idleHash = Animator.StringToHash(_idleState);
            _walkHash = Animator.StringToHash(_walkState);
            _attack01Hash = Animator.StringToHash(_attack01State);
            _attack02Hash = Animator.StringToHash(_attack02State);
            _attack03Hash = Animator.StringToHash(_attack03State);
            _hurtHash = Animator.StringToHash(_hurtState);
            _deathHash = Animator.StringToHash(_deathState);
        }

        public void PlayIdle()
        {
            StopTelegraph();
            PlayState(_idleHash);
        }

        public void PlayWalk()
        {
            StopTelegraph();
            PlayState(_walkHash);
        }

        public void StartTelegraphFlash()
        {
            StopTelegraph();
            if (_spriteRenderer != null)
            {
                _telegraphCoroutine = StartCoroutine(ContinuousTelegraphFlashRoutine());
            }
        }

        public void PlayAttack01_Slash()
        {
            PlayState(_attack01Hash, force: true);
        }

        public void PlayAttack02_Spin()
        {
            PlayState(_attack02Hash, force: true);
        }

        public void PlayAttack03_SlashAndWave()
        {
            PlayState(_attack03Hash, force: true);
        }

        public void PlayHurt()
        {
            StopTelegraph();
            PlayState(_hurtHash, force: true);
        }

        public void PlayDeath()
        {
            StopTelegraph();
            PlayState(_deathHash, force: true);
        }

        private void PlayState(int stateHash, bool force = false)
        {
            if (_animator == null) return;
            if (_animator.HasState(0, stateHash))
            {
                _animator.CrossFade(stateHash, 0.05f, 0, 0f);
            }
        }

        public void StopTelegraph()
        {
            if (_telegraphCoroutine != null)
            {
                StopCoroutine(_telegraphCoroutine);
                _telegraphCoroutine = null;
            }

            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = _originalColor;
            }
        }

        private IEnumerator ContinuousTelegraphFlashRoutine()
        {
            bool isToggled = false;
            while (true)
            {
                _spriteRenderer.color = isToggled ? _telegraphColor : _originalColor;
                isToggled = !isToggled;
                yield return new WaitForSeconds(_flashInterval);
            }
        }
    }
}
