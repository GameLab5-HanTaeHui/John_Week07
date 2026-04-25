using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HTH.Campaign
{
    /// <summary>
    /// 다이얼로그 UI를 순서대로 출력합니다.
    ///
    /// ─── 동작 흐름 ───────────────────────────────────────────────────────
    ///   Play(lines, onComplete) 호출
    ///   → 패널 활성화
    ///   → 한 줄씩 순서대로 표시
    ///     → 화자 이미지 교체 (항상 보임)
    ///     → 이름/텍스트 표시 (글리치 모드 시 처리)
    ///   → 클릭으로 다음 줄 진행
    ///   → 모든 줄 완료 시 onComplete 콜백
    ///
    /// ─── 글리치 모드 ─────────────────────────────────────────────────────
    ///   IsGlitchMode = true 시 이름/텍스트에 글리치 문자 혼합 출력
    ///   2회차(Phase2)에서는 일반 텍스트 출력
    ///   현재는 IsGlitchMode = false 고정 (Phase2에서 사용)
    ///
    /// ─── Canvas 구조 ─────────────────────────────────────────────────────
    ///   DialoguePanel
    ///   ├── CharacterImage (Image)   ← 항상 보임
    ///   ├── NameText (TMP_Text)      ← 글리치 처리 대상
    ///   └── DialogueText (TMP_Text)  ← 글리치 처리 대상
    ///
    /// ─── Inspector 설정 ──────────────────────────────────────────────────
    ///   DialoguePanel   → 전체 패널 루트
    ///   CharacterImage  → 화자 이미지
    ///   NameText        → 화자 이름 텍스트
    ///   DialogueText    → 대사 텍스트
    ///   CharacterSprites → ID별 캐릭터 스프라이트 배열 (인덱스 = CharacterId)
    ///   IsGlitchMode    → 글리치/모자이크 처리 여부
    ///   GlitchChars     → 글리치 대체 문자 목록
    ///   ClickToAdvance  → true: 클릭으로 다음 줄 / false: 자동 진행
    ///   AutoAdvanceDelay → 자동 진행 시 딜레이(초)
    /// </summary>
    [DisallowMultipleComponent]
    public class DialoguePlayer : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("UI 참조")]
        [SerializeField] private GameObject _dialoguePanel;
        [SerializeField] private Image _characterImage;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _dialogueText;

        [Header("캐릭터 스프라이트 (인덱스 = CharacterId)")]
        [Tooltip("CharacterId를 인덱스로 사용합니다. 0번 인덱스는 미사용.")]
        [SerializeField] private List<Sprite> _characterSprites = new();

        [Header("글리치 설정")]
        [Tooltip("true = 텍스트 글리치/모자이크 처리. 1회차 연출용.")]
        [SerializeField] private bool _isGlitchMode = false;
        [Tooltip("글리치 대체 문자 목록")]
        [SerializeField] private string _glitchChars = "█▓▒░▄▀?#@&*";

        [Header("진행 설정")]
        [Tooltip("true = 클릭으로 다음 줄 진행 / false = 자동 진행")]
        [SerializeField] private bool _clickToAdvance = true;
        [Tooltip("자동 진행 시 줄당 대기 시간(초). ClickToAdvance = false 시 사용.")]
        [SerializeField] private float _autoAdvanceDelay = 2.0f;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private bool _isPlaying;
        private bool _waitingForClick;
        private Coroutine _playCoroutine;
        private Action _onComplete;

        /// <summary>현재 다이얼로그 재생 중인지 여부입니다.</summary>
        public bool IsPlaying => _isPlaying;

        /// <summary>글리치 모드 활성화 여부입니다. 외부에서 동적으로 설정 가능합니다.</summary>
        public bool IsGlitchMode
        {
            get => _isGlitchMode;
            set => _isGlitchMode = value;
        }

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(false);
        }

        private void Update()
        {
            // 클릭으로 다음 줄 진행
            if (_waitingForClick && _clickToAdvance)
            {
                if (Input.GetMouseButtonDown(0))
                    _waitingForClick = false;
            }
        }

        private void OnDestroy()
        {
            if (_playCoroutine != null)
                StopCoroutine(_playCoroutine);
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 대사 목록을 순서대로 재생합니다.
        /// 재생 중이면 무시됩니다.
        /// </summary>
        /// <param name="lines">재생할 대사 목록</param>
        /// <param name="onComplete">모든 대사 재생 완료 후 호출되는 콜백</param>
        public void Play(List<DialogueLine> lines, Action onComplete = null)
        {
            if (_isPlaying)
            {
                Debug.LogWarning("[DialoguePlayer] 이미 재생 중 — 무시됩니다.");
                return;
            }
            if (lines == null || lines.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            _onComplete = onComplete;
            _isPlaying = true;
            _playCoroutine = StartCoroutine(PlayCoroutine(lines));
        }

        /// <summary>
        /// 현재 재생 중인 다이얼로그를 즉시 종료합니다.
        /// onComplete 콜백이 호출됩니다.
        /// </summary>
        public void Skip()
        {
            if (!_isPlaying) return;

            if (_playCoroutine != null)
            {
                StopCoroutine(_playCoroutine);
                _playCoroutine = null;
            }

            FinishPlay();
        }

        // ── Private — 재생 코루틴 ─────────────────────────────────────────

        private IEnumerator PlayCoroutine(List<DialogueLine> lines)
        {
            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(true);

            foreach (var line in lines)
            {
                if (line == null) continue;

                // 캐릭터 이미지 교체
                UpdateCharacterImage(line.SpeakerId);

                // 이름/텍스트 설정
                string displayName = $"#{line.SpeakerId}";
                string displayText = line.Text;

                if (_isGlitchMode)
                {
                    displayName = ApplyGlitch(displayName);
                    displayText = ApplyGlitch(displayText);
                }

                if (_nameText != null) _nameText.text = displayName;
                if (_dialogueText != null) _dialogueText.text = displayText;

                // 진행 대기
                if (_clickToAdvance)
                {
                    _waitingForClick = true;
                    yield return new WaitUntil(() => !_waitingForClick);
                }
                else
                {
                    yield return new WaitForSeconds(_autoAdvanceDelay);
                }
            }

            FinishPlay();
        }

        private void FinishPlay()
        {
            _isPlaying = false;
            _waitingForClick = false;
            _playCoroutine = null;

            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(false);

            var callback = _onComplete;
            _onComplete = null;
            callback?.Invoke();
        }

        // ── Private — 글리치 처리 ─────────────────────────────────────────

        /// <summary>
        /// 텍스트에 글리치 문자를 혼합합니다.
        /// 각 문자를 일정 확률로 글리치 문자로 대체합니다.
        /// </summary>
        private string ApplyGlitch(string text)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(_glitchChars))
                return text;

            var sb = new System.Text.StringBuilder(text.Length);
            foreach (char c in text)
            {
                // 공백은 유지, 나머지는 70% 확률로 글리치 처리
                if (c == ' ' || UnityEngine.Random.value > 0.7f)
                    sb.Append(c);
                else
                    sb.Append(_glitchChars[UnityEngine.Random.Range(0, _glitchChars.Length)]);
            }
            return sb.ToString();
        }

        // ── Private — 이미지 ─────────────────────────────────────────────

        private void UpdateCharacterImage(int characterId)
        {
            if (_characterImage == null) return;

            Sprite sprite = GetSpriteById(characterId);
            if (sprite != null)
            {
                _characterImage.sprite = sprite;
                _characterImage.enabled = true;
            }
            else
            {
                _characterImage.enabled = false;
                Debug.LogWarning($"[DialoguePlayer] CharacterId={characterId}의 스프라이트가 없습니다.");
            }
        }

        private Sprite GetSpriteById(int characterId)
        {
            if (_characterSprites == null) return null;
            if (characterId < 0 || characterId >= _characterSprites.Count) return null;
            return _characterSprites[characterId];
        }
    }
}