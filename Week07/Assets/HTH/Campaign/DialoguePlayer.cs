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
    /// ─── 글리치 모드 상세 ────────────────────────────────────────────────
    ///   이름 글리치 (GlitchName):
    ///     true이면 미수집 이름을 글리치 문자로 표시합니다.
    ///     CharacterRecordBook에서 이름을 수집했으면 실제 이름을 표시합니다.
    ///     수집 전: "█▓▒" / 수집 후: "새턴"
    ///
    ///   대사 글리치 (GlitchDialogue):
    ///     true이면 대사 텍스트 전체를 글리치 문자로 표시합니다.
    ///     1회차 연출용으로 현재 Phase2에서는 false로 유지합니다.
    ///
    /// ─── 클릭 차단 ───────────────────────────────────────────────────────
    ///   이전 화면(검은 화면, 확인 패널)의 클릭이 첫 줄 스킵으로 이어지는 것을 방지합니다.
    ///   1. 마우스 버튼이 완전히 떼어질 때까지 대기 (WaitUntil)
    ///   2. 추가로 _clickBlockDuration 동안 클릭 차단 (WaitForSeconds)
    ///
    /// ─── 씬 배치 ─────────────────────────────────────────────────────────
    ///   _CampaignSystem 하위 GameObject에 컴포넌트로 추가합니다.
    ///   Canvas 안에 배치하지 않습니다 (UI 패널을 참조하지만 컴포넌트 자체는 Canvas 밖).
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Dialogue Panel       → Canvas/DialoguePanel (전체 패널 GameObject)
    ///   Character Image      → DialoguePanel 하위 Image 컴포넌트 (화자 이미지)
    ///   Name Text            → DialoguePanel 하위 TMP_Text (화자 이름)
    ///   Dialogue Text        → DialoguePanel 하위 TMP_Text (대사 내용)
    ///   Character Record Book → _CampaignSystem/CharacterRecordBook
    ///   Character Sprites    → 캐릭터별 스프라이트 배열 (인덱스 = CharacterId)
    ///                          [0] 비워두기, [1]=#1, [2]=#2, ... [7]=#7
    ///   Glitch Name          → 미수집 이름 글리치 여부 (기본값 true)
    ///   Glitch Dialogue      → 대사 텍스트 글리치 여부 (기본값 false)
    ///   Glitch Chars         → 글리치 대체 문자 목록
    ///   Click To Advance     → true (클릭으로 다음 줄 진행)
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

        [Header("캐릭터 정보")]
        [Tooltip("수집된 캐릭터 이름을 가져오는 인물 기록장입니다.\n" +
                 "_CampaignSystem/CharacterRecordBook을 연결합니다.\n" +
                 "연결하지 않으면 '#1' 형식으로 표시됩니다.")]
        [SerializeField] private CharacterRecordBook _characterRecordBook;

        [Header("캐릭터 스프라이트 (인덱스 = CharacterId)")]
        [Tooltip("캐릭터 ID를 인덱스로 사용해 스프라이트를 가져옵니다.\n" +
                 "[0]은 비워두고 [1]부터 캐릭터 스프라이트를 넣습니다.\n" +
                 "예: [1]=#1 스프라이트, [2]=#2 스프라이트, ..., [7]=#7 스프라이트")]
        [SerializeField] private List<Sprite> _characterSprites = new();

        [Header("글리치 설정")]
        [Tooltip("true이면 수집되지 않은 캐릭터 이름을 글리치 문자로 표시합니다.\n" +
                 "CharacterRecordBook에서 이름이 수집된 경우 실제 이름을 표시합니다.\n" +
                 "수집 전: '█▓▒' / 수집 후: '새턴'\n" +
                 "Phase2에서는 true로 설정하는 것을 권장합니다.")]
        [SerializeField] private bool _glitchName = true;

        [Tooltip("true이면 대사 텍스트 전체를 글리치 문자로 표시합니다.\n" +
                 "1회차 연출용으로 현재 Phase2에서는 false로 유지합니다.")]
        [SerializeField] private bool _glitchDialogue = false;

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

        private bool _isPlaying;
        private bool _waitingForClick;
        private bool _clickBlocked;
        private Coroutine _playCoroutine;
        private Action _onComplete;

        /// <summary>현재 대사를 재생 중인지 여부입니다.</summary>
        public bool IsPlaying => _isPlaying;

        /// <summary>
        /// 이름 글리치 모드 활성화 여부입니다.
        /// 외부에서 동적으로 변경할 수 있습니다.
        /// </summary>
        public bool GlitchName
        {
            get => _glitchName;
            set => _glitchName = value;
        }

        /// <summary>
        /// 대사 텍스트 글리치 모드 활성화 여부입니다.
        /// 외부에서 동적으로 변경할 수 있습니다.
        /// </summary>
        public bool GlitchDialogue
        {
            get => _glitchDialogue;
            set => _glitchDialogue = value;
        }

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(false);
        }

        private void Update()
        {
            if (_clickBlocked) return;

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

        /// <summary>현재 재생 중인 대사를 즉시 종료합니다.</summary>
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
        /// <summary>
        /// 획득 알림 텍스트를 순서대로 표시합니다.
        /// DialogueTriggerManager에서 대사 완료 후 호출합니다.
        /// 화자 이미지 없이 텍스트만 표시합니다.
        /// </summary>
        public void PlayNotification(List<string> messages, Action onComplete = null)
        {
            if (messages == null || messages.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            StartCoroutine(NotificationCoroutine(messages, onComplete));
        }

        private IEnumerator NotificationCoroutine(List<string> messages, Action onComplete)
        {
            // 패널이 닫혀있으면 열기
            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(true);

            // 화자 이미지 숨김
            if (_characterImage != null)
                _characterImage.enabled = false;

            // 이름 텍스트 비움
            if (_nameText != null)
                _nameText.text = "";

            // 클릭 차단 (이전 클릭 잔여 방지)
            _clickBlocked = true;
            yield return new WaitForSeconds(0.3f);
            _clickBlocked = false;

            foreach (var message in messages)
            {
                if (string.IsNullOrEmpty(message)) continue;

                if (_dialogueText != null)
                    _dialogueText.text = message;

                _waitingForClick = true;
                yield return new WaitUntil(() => !_waitingForClick);
            }

            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(false);

            onComplete?.Invoke();
        }

        // ── Private — 재생 코루틴 ─────────────────────────────────────────

        private IEnumerator PlayCoroutine(List<DialogueLine> lines)
        {
            // 패널 활성화 전 첫 번째 줄을 미리 세팅합니다.
            var firstLine = lines[0];
            if (firstLine != null)
            {
                UpdateCharacterImage(firstLine.SpeakerId);
                if (_nameText != null) _nameText.text = BuildNameText(firstLine.SpeakerId);
                if (_dialogueText != null) _dialogueText.text = BuildDialogueText(firstLine.Text);
            }

            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(true);

            yield return new WaitUntil(() => !Input.GetMouseButton(0));
            _clickBlocked = true;
            yield return new WaitForSeconds(_clickBlockDuration);
            _clickBlocked = false;

            // 첫 번째 줄은 이미 세팅됐으므로 클릭 대기만 합니다.
            // 이후 줄부터 정상 출력합니다.
            bool isFirst = true;
            foreach (var line in lines)
            {
                if (line == null) continue;

                if (isFirst)
                {
                    // 첫 줄: 텍스트는 이미 세팅됨, 클릭 대기만
                    isFirst = false;
                }
                else
                {
                    // 두 번째 줄부터 정상 처리
                    UpdateCharacterImage(line.SpeakerId);
                    if (_nameText != null) _nameText.text = BuildNameText(line.SpeakerId);
                    if (_dialogueText != null) _dialogueText.text = BuildDialogueText(line.Text);
                }

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
            _clickBlocked = false;
            _playCoroutine = null;

            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(false);

            var callback = _onComplete;
            _onComplete = null;
            callback?.Invoke();
        }

        // ── Private — 텍스트 빌드 ─────────────────────────────────────────

        /// <summary>
        /// 화자 이름 텍스트를 생성합니다.
        ///
        /// GlitchName = true인 경우:
        ///   이름 수집됨 → 실제 이름 표시 ("새턴")
        ///   이름 미수집 → 글리치 문자 표시 ("█▓▒")
        ///
        /// GlitchName = false인 경우:
        ///   이름 수집됨 → 실제 이름 표시 ("새턴")
        ///   이름 미수집 → "#번호" 표시 ("#2")
        /// </summary>
        private string BuildNameText(int speakerId)
        {
            string collectedName = _characterRecordBook?.GetCollectedName(speakerId);
            bool hasName = !string.IsNullOrEmpty(collectedName);

            if (hasName)
            {
                // 이름 수집됨 → 항상 실제 이름 표시 (글리치 없음)
                return collectedName;
            }

            if (_glitchName)
            {
                // 이름 미수집 + 글리치 모드 → 글리치 문자 표시
                // "#번호"를 글리치 처리해 번호도 숨깁니다.
                return ApplyGlitch($"#{speakerId}");
            }

            // 이름 미수집 + 글리치 없음 → "#번호" 표시
            return $"#{speakerId}";
        }

        /// <summary>
        /// 대사 텍스트를 생성합니다.
        ///
        /// GlitchDialogue = true  → 대사 전체 글리치 처리
        /// GlitchDialogue = false → 원본 대사 그대로 표시
        /// </summary>
        private string BuildDialogueText(string text)
        {
            if (_glitchDialogue)
                return ApplyGlitch(text);

            return text ?? "";
        }

        // ── Private — 글리치 처리 ─────────────────────────────────────────

        /// <summary>
        /// 텍스트에 글리치 문자를 혼합합니다.
        /// 공백은 유지하고 나머지는 70% 확률로 대체합니다.
        /// </summary>
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