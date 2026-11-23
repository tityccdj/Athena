using System.Collections;
using UnityEngine;

namespace Athena.Boss.Phase3
{
    /// <summary>
    /// ควบคุม Animation และ Telegraph Flash ของบอส Phase 3 (Necromancer)
    /// </summary>
    public class NecromancerAnimation : MonoBehaviour
    {
        [Header("Animator State Names")]
        [SerializeField] private string _startState = "Necromancer_Start";
        [SerializeField] private string _idleState = "Necromancer_Idle";
        [SerializeField] private string _walkState = "Necromancer_Walk";
        [SerializeField] private string _attack01MeleeState = "Necromancer_1AttackMelee";
        [SerializeField] private string _attack02MeteorState = "Necromancer_2AttackMeteor";
        [SerializeField] private string _hurtState = "Necromancer_Hurt";
        [SerializeField] private string _deathState = "Necromancer_DEATH";

        [Header("Telegraph Flash Settings (GDD: ตัวกะพริบเพื่อเตือน)")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Color _telegraphColor = new Color(0.9f, 0.3f, 1f, 1f); // สีม่วงสว่างเตือนเวทมนตร์
        [SerializeField] private float _flashInterval = 0.1f;

        private Animator _animator;
        private Color _originalColor;
        private Coroutine _telegraphCoroutine;

        private int _startHash;
        private int _idleHash;
        private int _walkHash;
        private int _attack01MeleeHash;
        private int _attack02MeteorHash;
        private int _hurtHash;
        private int _deathHash;

        private void Awake()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (_animator != null) return;

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
            _startHash = Animator.StringToHash(_startState);
            _idleHash = Animator.StringToHash(_idleState);
            _walkHash = Animator.StringToHash(_walkState);
            _attack01MeleeHash = Animator.StringToHash(_attack01MeleeState);
            _attack02MeteorHash = Animator.StringToHash(_attack02MeteorState);
            _hurtHash = Animator.StringToHash(_hurtState);
            _deathHash = Animator.StringToHash(_deathState);
        }

        public void PlayStart()
        {
            StopTelegraph();
            PlayState(_startHash, force: true);
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

        public void PlayAttack01_Melee()
        {
            PlayState(_attack01MeleeHash, force: true);
        }

        public void PlayAttack02_Explosion()
        {
            PlayState(_attack02MeteorHash, force: true);
        }

        public void PlayAttack02_Meteor()
        {
            PlayAttack02_Explosion();
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
            EnsureInitialized();
            if (_animator == null) return;
            if (_animator.HasState(0, stateHash))
            {
                if (force)
                {
                    _animator.Play(stateHash, 0, 0f);
                }
                else
                {
                    _animator.CrossFade(stateHash, 0.05f, 0, 0f);
                }
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
