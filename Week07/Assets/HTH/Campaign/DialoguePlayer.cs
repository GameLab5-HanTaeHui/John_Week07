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
    /// ─── 이 스크립트의 역할 ──────────────────────────────────────────────
    ///   DialogueTriggerManager로부터 대사 목록을 받아 화면에 출력합니다.
    ///   화자의 이미지를 표시하고, 텍스트를 한 줄씩 보여주며,
    ///   클릭으로 다음 줄로 넘어갑니다.
    ///   모든 대사가 끝나면 onComplete 콜백을 호출해 DialogueTriggerManager에 알립니다.
    ///
    /// ─── 동작 흐름 ───────────────────────────────────────────────────────
    ///   DialogueTriggerManager.PlayGroupDialogue()에서 Play() 호출
    ///   → _isPlaying = true, DialoguePanel 활성화
    ///   → 마우스 버튼이 완전히 떼어질 때까지 대기
    ///   → _clickBlockDuration 동안 클릭 차단
    ///   → 첫 번째 줄의 화자 이미지 교체
    ///   → 이름 텍스트와 대사 텍스트 표시
    ///   → 클릭 대기 (_waitingForClick = true)
    ///   → 클릭하면 다음 줄로 이동
    ///   → 모든 줄 완료 → DialoguePanel 비활성화 → onComplete 호출
    ///
    /// ─── 클릭 차단 ───────────────────────────────────────────────────────
    ///   이전 화면(검은 화면, 확인 패널)의 클릭이 첫 줄 스킵으로 이어지는 것을 방지합니다.
    ///   1. 마우스 버튼이 완전히 떼어질 때까지 대기 (WaitUntil)
    ///   2. 추가로 _clickBlockDuration 동안 클릭 차단 (WaitForSeconds)
    ///
    /// ─── 글리치 모드 ─────────────────────────────────────────────────────
    ///   IsGlitchMode = true이면 이름과 대사 텍스트에 글리치 문자를 섞어 표시합니다.
    ///   1회차 연출용으로 설계됐으나 현재 Phase2에서는 false로 사용합니다.
    ///
    /// ─── 씬 배치 ─────────────────────────────────────────────────────────
    ///   _CampaignSystem 하위 GameObject에 컴포넌트로 추가합니다.
    ///   Canvas 안에 배치하지 않습니다 (UI 패널을 참조하지만 컴포넌트 자체는 Canvas 밖).
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Dialogue Panel      → Canvas/DialoguePanel (전체 패널 GameObject)
    ///   Character Image     → DialoguePanel 하위 Image 컴포넌트 (화자 이미지)
    ///   Name Text           → DialoguePanel 하위 TMP_Text (화자 이름)
    ///   Dialogue Text       → DialoguePanel 하위 TMP_Text (대사 내용)
    ///   Character Sprites   → 캐릭터별 스프라이트 배열 (인덱스 = CharacterId)
    ///                         [0] 비워두기, [1]=#1, [2]=#2, ... [7]=#7
    ///   Is Glitch Mode      → false (Phase2에서는 정상 텍스트)
    ///   Click To Advance    → true (클릭으로 다음 줄 진행)
    ///   Click Block Duration → 대사 시작 후 클릭 차단 시간 (기본값 1.5초)
    /// </summary>
    [DisallowMultipleComponent]
    public class DialoguePlayer : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("UI 참조")]
        [Tooltip("다이얼로그 전체 패널입니다.\nCanvas/DialoguePanel을 연결합니다.")]
        [SerializeField] private GameObject _dialoguePanel;

        [Tooltip("화자의 이미지를 표시하는 Image 컴포넌트입니다.\n항상 보입니다.")]
        [SerializeField] private Image _characterImage;

        [Tooltip("화자의 이름을 표시하는 TMP_Text입니다.\n예: '#1' 또는 '새턴'")]
        [SerializeField] private TMP_Text _nameText;

        [Tooltip("대사 내용을 표시하는 TMP_Text입니다.")]
        [SerializeField] private TMP_Text _dialogueText;

        [Header("캐릭터 스프라이트 (인덱스 = CharacterId)")]
        [Tooltip("캐릭터 ID를 인덱스로 사용해 스프라이트를 가져옵니다.\n" +
                 "[0]은 비워두고 [1]부터 캐릭터 스프라이트를 넣습니다.\n" +
                 "예: [1]=#1 스프라이트, [2]=#2 스프라이트, ..., [7]=#7 스프라이트")]
        [SerializeField] private List<Sprite> _characterSprites = new();

        [Header("글리치 설정")]
        [Tooltip("true이면 이름과 대사 텍스트에 글리치 문자를 섞어 표시합니다.\n" +
                 "1회차 연출용으로 현재 Phase2에서는 false로 유지합니다.")]
        [SerializeField] private bool _isGlitchMode = false;

        [Tooltip("글리치 처리 시 사용할 대체 문자들입니다.\n" +
                 "각 문자를 70% 확률로 원본 문자 대신 표시합니다.")]
        [SerializeField] private string _glitchChars = "█▓▒░▄▀?#@&*";

        [Header("진행 설정")]
        [Tooltip("true이면 마우스 클릭으로 다음 줄로 진행합니다.\n" +
                 "false이면 AutoAdvanceDelay 시간 후 자동으로 다음 줄로 넘어갑니다.")]
        [SerializeField] private bool _clickToAdvance = true;

        [Tooltip("자동 진행 시 각 줄마다 대기하는 시간(초)입니다.\n" +
                 "ClickToAdvance = false일 때만 사용됩니다.")]
        [SerializeField] private float _autoAdvanceDelay = 2.0f;

        [Header("클릭 차단")]
        [Tooltip("대사 시작 후 클릭을 무시할 시간(초)입니다.\n" +
                 "이전 화면(검은 화면, 확인 패널)의 클릭이 첫 줄을 스킵하는 것을 방지합니다.\n" +
                 "기본값 1.5초.")]
        [SerializeField] private float _clickBlockDuration = 1.5f;

        // ── 내부 상태 ─────────────────────────────────────────────────────

        // 현재 대사를 재생 중인지 여부입니다.
        // DialogueTriggerManager에서 이 값을 확인해 재생 중일 때 중복 재생을 방지합니다.
        private bool _isPlaying;

        // 클릭 대기 중인지 여부입니다.
        // Update()에서 마우스 클릭을 감지해 이 값을 false로 변경합니다.
        private bool _waitingForClick;

        // 클릭 차단 중인지 여부입니다.
        // 대사 시작 직후 _clickBlockDuration 동안 true로 유지됩니다.
        // Update()에서 이 값이 true이면 클릭 입력을 무시합니다.
        private bool _clickBlocked;

        private Coroutine _playCoroutine;
        private Action _onComplete;

        /// <summary>현재 대사를 재생 중인지 여부입니다.</summary>
        public bool IsPlaying => _isPlaying;

        /// <summary>
        /// 글리치 모드 활성화 여부입니다.
        /// 외부에서 동적으로 변경할 수 있습니다.
        /// 예: dialoguePlayer.IsGlitchMode = true;
        /// </summary>
        public bool IsGlitchMode
        {
            get => _isGlitchMode;
            set => _isGlitchMode = value;
        }

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            // 처음에는 패널을 숨깁니다.
            // Play()가 호출될 때 활성화됩니다.
            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(false);
        }

        private void Update()
        {
            // 클릭 차단 중이면 클릭 입력을 무시합니다.
            if (_clickBlocked) return;

            // 마우스를 뗄 때 다음 줄로 진행합니다.
            // GetMouseButtonDown 대신 GetMouseButtonUp을 사용해
            // 누른 채로 진입한 경우 손을 뗄 때만 진행하도록 합니다.
            if (_waitingForClick && _clickToAdvance)
            {
                if (Input.GetMouseButtonUp(0))
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
        /// DialogueTriggerManager.PlayGroupDialogue()에서 호출합니다.
        ///
        /// 이미 재생 중이면 무시됩니다.
        /// lines가 비어있으면 즉시 onComplete를 호출합니다.
        /// </summary>
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
        /// 현재 재생 중인 대사를 즉시 종료합니다.
        /// 코루틴을 중단하고 패널을 닫은 후 onComplete를 호출합니다.
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
            // 패널 활성화 전에 첫 번째 줄을 먼저 세팅합니다.
            // 패널이 열리는 순간 Line 0이 표시됩니다.
            var firstLine = lines[0];
            if (firstLine != null)
            {
                UpdateCharacterImage(firstLine.SpeakerId);

                string firstName = _isGlitchMode ? ApplyGlitch($"#{firstLine.SpeakerId}") : $"#{firstLine.SpeakerId}";
                string firstText = _isGlitchMode ? ApplyGlitch(firstLine.Text) : firstLine.Text;

                if (_nameText != null) _nameText.text = firstName;
                if (_dialogueText != null) _dialogueText.text = firstText;
            }

            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(true);

            // 마우스 버튼이 완전히 떼어질 때까지 대기합니다.
            // 이전 화면(검은 화면, 확인 패널)의 클릭이 남아있는 경우를 처리합니다.
            yield return new WaitUntil(() => !Input.GetMouseButton(0));

            // 추가로 _clickBlockDuration 동안 클릭을 차단합니다.
            // 마우스를 뗀 직후의 의도치 않은 클릭을 방지합니다.
            _clickBlocked = true;
            yield return new WaitForSeconds(_clickBlockDuration);
            _clickBlocked = false;

            foreach (var line in lines)
            {
                if (line == null) continue;

                UpdateCharacterImage(line.SpeakerId);

                string displayName = $"#{line.SpeakerId}";
                string displayText = line.Text;

                if (_isGlitchMode)
                {
                    displayName = ApplyGlitch(displayName);
                    displayText = ApplyGlitch(displayText);
                }

                if (_nameText != null) _nameText.text = displayName;
                if (_dialogueText != null) _dialogueText.text = displayText;

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
            _clickBlocked = false; // 비정상 종료(Skip) 시에도 차단 해제
            _playCoroutine = null;

            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(false);

            // onComplete를 로컬 변수에 저장 후 null로 초기화합니다.
            // 콜백 안에서 다시 Play()가 호출될 경우를 대비한 처리입니다.
            var callback = _onComplete;
            _onComplete = null;
            callback?.Invoke();
        }

        // ── Private — 글리치 처리 ─────────────────────────────────────────

        private string ApplyGlitch(string text)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(_glitchChars))
                return text;

            var sb = new System.Text.StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == ' ' || UnityEngine.Random.value > 0.7f)
                    sb.Append(c);
                else
                    sb.Append(_glitchChars[UnityEngine.Random.Range(0, _glitchChars.Length)]);
            }
            return sb.ToString();
        }

        // ── Private — 이미지 처리 ─────────────────────────────────────────

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