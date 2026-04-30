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
    /// ─── 역할 ────────────────────────────────────────────────────────────
    ///   DialogueTriggerManager로부터 대사 목록을 받아 화면에 출력합니다.
    ///   화자의 이미지를 표시하고, 텍스트를 한 줄씩 보여주며,
    ///   클릭으로 다음 줄로 넘어갑니다.
    ///   모든 대사가 끝나면 onComplete 콜백을 호출해 DialogueTriggerManager에 알립니다.
    ///
    /// ─── 글리치 모드 ─────────────────────────────────────────────────────
    ///   이름 글리치 (GlitchName):
    ///     미수집 이름을 글리치 문자로 표시합니다.
    ///     CharacterRecordBook에서 이름을 수집하면 실제 이름으로 전환됩니다.
    ///     수집 전: "█▓▒" / 수집 후: "새턴"
    ///
    ///   대사 글리치 (GlitchDialogue):
    ///     대사 텍스트 전체를 글리치 문자로 표시합니다.
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
    ///   Code Text            → DialoguePanel 하위 TMP_Text (대사 중요도 레이블)
    ///   Name Text            → DialoguePanel 하위 TMP_Text (화자 이름)
    ///   Dialogue Text        → DialoguePanel 하위 TMP_Text (대사 내용)
    ///   Character Sprites    → 캐릭터별 스프라이트 배열 (인덱스 = CharacterId)
    ///                          [0] 비워두기, [1]=#1, [2]=#2, ... [7]=#7
    ///   Click To Advance     → true (클릭으로 다음 줄 진행)
    ///   Click Block Duration → 대사 시작 후 클릭 차단 시간 (기본값 1.5초)
    /// </summary>
    [DisallowMultipleComponent]
    public class DialoguePlayer : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("캐릭터 데이터")]
        [Tooltip("캐릭터 이름을 가져올 ProfileDataSO 에셋입니다.")]
        [SerializeField] private ProfileDataSO _profileData;

        [Header("UI 참조")]
        [Tooltip("다이얼로그 전체 패널입니다.\nCanvas/DialoguePanel을 연결합니다.")]
        [SerializeField] private GameObject _dialoguePanel;

        [Tooltip("화자의 이미지를 표시하는 Image 컴포넌트입니다.")]
        [SerializeField] private Image _characterImage;

        [Tooltip("대사 중요도를 표시하는 TMP_Text입니다.\n예: 핵심 / 힌트 / 일반")]
        [SerializeField] private TMP_Text _codeText;

        [Tooltip("화자의 이름을 표시하는 TMP_Text입니다.\n예: '새턴'")]
        [SerializeField] private TMP_Text _nameText;

        [Tooltip("대사 내용을 표시하는 TMP_Text입니다.")]
        [SerializeField] private TMP_Text _dialogueText;

        [Header("캐릭터 스프라이트 (인덱스 = CharacterId)")]
        [Tooltip("캐릭터 ID를 인덱스로 사용해 스프라이트를 가져옵니다.\n" +
                 "[0]은 비워두고 [1]부터 캐릭터 스프라이트를 넣습니다.\n" +
                 "예: [1]=#1 스프라이트, [2]=#2 스프라이트, ..., [7]=#7 스프라이트")]
        [SerializeField] private List<Sprite> _characterSprites = new();

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

        // ── 퍼스널 컬러 ───────────────────────────────────────────────────

        private static readonly Color[] PersonalColors =
        {
            Color.white,                                    // [0] 미사용
            new Color(0xC8/255f, 0xA8/255f, 0x88/255f),   // [1] 엔비  #C8A888
            new Color(0x48/255f, 0x78/255f, 0x48/255f),   // [2] 메이  #487848
            new Color(0x58/255f, 0x58/255f, 0x88/255f),   // [3] 데우스 #585888
            new Color(0xD8/255f, 0xD8/255f, 0xE8/255f),   // [4] 루이스 #D8D8E8
            new Color(0xE8/255f, 0xD8/255f, 0x98/255f),   // [5] 토니  #E8D898
            new Color(0xE8/255f, 0x88/255f, 0x68/255f),   // [6] 프리드 #E88868
            new Color(0x98/255f, 0x88/255f, 0x68/255f),   // [7] 새턴  #988868
        };

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private string _currentTypeLabel;
        private bool _isPlaying;
        private bool _waitingForClick;
        private bool _clickBlocked;
        private Coroutine _playCoroutine;
        private Action _onComplete;

        public bool IsPlaying => _isPlaying;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(false);
        }

        private void Update()
        {
            if (_clickBlocked) return;
            if (_waitingForClick && _clickToAdvance && Input.GetMouseButtonDown(0))
                _waitingForClick = false;
        }

        private void OnDestroy()
        {
            if (_playCoroutine != null)
                StopCoroutine(_playCoroutine);
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 대사 목록을 순서대로 재생합니다.
        /// IReadOnlyList를 받으므로 List / 배열 / SO IReadOnlyList 모두 호환됩니다.
        /// </summary>
        public void Play(IReadOnlyList<DialogueLine> lines, Action onComplete = null, DialogueType? dialogueType = null)
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

            _currentTypeLabel = dialogueType.HasValue ? GetTypeLabel(dialogueType.Value) : "";
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

        /// <summary>획득 알림 텍스트를 순서대로 표시합니다. 화자 이미지 없이 텍스트만 표시합니다.</summary>
        public void PlayNotification(List<string> messages, Action onComplete = null)
        {
            if (messages == null || messages.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }
            StartCoroutine(NotificationCoroutine(messages, onComplete));
        }

        // ── Private — 재생 코루틴 ─────────────────────────────────────────

        private IEnumerator PlayCoroutine(IReadOnlyList<DialogueLine> lines)
        {
            // 첫 줄 내용을 먼저 세팅한 뒤 패널 활성화
            var firstLine = lines[0];
            if (firstLine != null)
            {
                if (int.TryParse(firstLine.TextId, out int firstSpeakerId))
                    UpdateCharacterDisplay(firstSpeakerId);
                if (_dialogueText != null)
                    _dialogueText.text = BuildDialogueText(firstLine.Text);
            }

            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(true);

            yield return new WaitUntil(() => !Input.GetMouseButton(0));
            _clickBlocked = true;
            yield return new WaitForSeconds(_clickBlockDuration);
            _clickBlocked = false;

            if (_clickToAdvance)
            {
                _waitingForClick = true;
                yield return new WaitUntil(() => !_waitingForClick);
            }
            else
                yield return new WaitForSeconds(_autoAdvanceDelay);

            for (int i = 1; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null) continue;

                if (int.TryParse(line.TextId, out int speakerId))
                    UpdateCharacterDisplay(speakerId);

                if (_dialogueText != null)
                    _dialogueText.text = BuildDialogueText(line.Text);

                if (_clickToAdvance)
                {
                    _waitingForClick = true;
                    yield return new WaitUntil(() => !_waitingForClick);
                }
                else
                    yield return new WaitForSeconds(_autoAdvanceDelay);
            }

            FinishPlay();
        }

        private IEnumerator NotificationCoroutine(List<string> messages, Action onComplete)
        {
            if (_dialoguePanel != null)
                _dialoguePanel.SetActive(true);

            if (_characterImage != null) _characterImage.enabled = false;
            if (_nameText != null) _nameText.text = "";

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

        private static string GetTypeLabel(DialogueType type) => type switch
        {
            DialogueType.Core => "핵심 대화 내용",
            DialogueType.Hint => "힌트 대화 내용",
            DialogueType.Special => "특수 대화 내용",
            DialogueType.Normal => "일반 대화 내용",
            _ => ""
        };

        private string BuildNameText(int speakerId)
        {
            string collectedName = CharacterRecordPanelManager.Instance?.GetCollectedName(speakerId);
            if (!string.IsNullOrEmpty(collectedName))
                return collectedName;

            var profile = _profileData?.FindProfile(speakerId);
            if (profile != null && !string.IsNullOrEmpty(profile.CharacterFullName))
                return profile.CharacterFullName;

            return $"#{speakerId}";
        }

        private string BuildDialogueText(string text) => text ?? "";

        private Color GetPersonalColor(int characterId)
        {
            if (characterId < 0 || characterId >= PersonalColors.Length)
                return Color.white;
            return PersonalColors[characterId];
        }

        // ── Private — 화자 표시 ───────────────────────────────────────────

        private void UpdateCharacterDisplay(int characterId)
        {
            Color color = GetPersonalColor(characterId);

            if (_codeText != null)
                _codeText.text = _currentTypeLabel;

            if (_nameText != null)
            {
                _nameText.text = BuildNameText(characterId);
                _nameText.color = color;
            }

            if (_characterImage != null)
            {
                Sprite sprite = GetSpriteById(characterId);
                if (sprite != null)
                {
                    _characterImage.sprite = sprite;
                    _characterImage.enabled = true;
                }
                else
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