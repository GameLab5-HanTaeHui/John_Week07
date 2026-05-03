using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HTH.Campaign
{
    /// <summary>
    /// 캠페인 전용 대화 출력기입니다.
    /// 인게임 대화 조각(FragmentDialogueLine)과 엔딩 대화(FinalTalkLine) 모두 처리합니다.
    ///
    /// ─── 인게임 모드 (Play FragmentDialogueLine) ─────────────────────────
    ///   화자 이미지 + 이름(퍼스널컬러) + 대사 / 클릭 진행
    ///   DTM → DialoguePlayer.Play(FragmentDialogueLine 목록)
    ///
    /// ─── 엔딩 모드 (Play FinalTalkLine) ─────────────────────────────────
    ///   타이핑 효과 + 페이드 전환 + 배경 교체 / 클릭 또는 자동
    ///   FinalTalkUI → DialoguePlayer.Play(FinalTalkLine 목록)
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   [인게임 공통]
    ///   Dialogue Panel       → 인게임 대화 패널 GameObject
    ///   Character Image      → 화자 이미지 Image
    ///   Name Text            → 화자 이름 TMP_Text
    ///   Dialogue Text        → 대사 내용 TMP_Text
    ///   Profile Data         → ProfileDataSO (화자 이름 조회)
    ///   Character Sprites    → 캐릭터별 스프라이트 (인덱스 = CharacterId, [0] 비워두기)
    ///   Click To Advance     → true=클릭 진행 / false=자동 진행
    ///   Auto Advance Delay   → 자동 진행 대기 시간 (초)
    ///   Click Block Duration → 대사 시작 후 클릭 차단 시간 (기본 1.5초)
    ///
    ///   [엔딩 전용]
    ///   Ending Panel         → 엔딩 패널 GameObject (CanvasGroup 필요)
    ///   Background Image     → 배경 이미지 Image
    ///   Click Indicator      → 클릭 대기 아이콘 GameObject (선택)
    ///   Background Sprites   → SpeakerId 인덱스 기반 배경 스프라이트
    ///   Typing Speed         → 타이핑 속도 (초/글자, 기본 0.03)
    ///   Fade Duration        → 페이드 시간 (초, 기본 0.4)
    ///   Fade Per Line        → true = 줄마다 페이드 / false = 시작·끝만 페이드
    ///   Ending Click Block Duration → 엔딩 줄 시작 후 클릭 차단 시간 (기본 0.8초)
    ///   Hide Name For Speaker Zero  → SpeakerId=0이면 이름 숨김 (내레이션용)
    /// </summary>
    [DisallowMultipleComponent]
    public class DialoguePlayer : MonoBehaviour
    {
        // ── Inspector — 공통 ─────────────────────────────────────────────

        [Header("데이터")]
        [Tooltip("화자 이름 조회용 ProfileDataSO 에셋입니다.")]
        [SerializeField] private ProfileDataSO _profileData;

        [Header("인게임 UI")]
        [Tooltip("인게임 대화 전체 패널입니다.")]
        [SerializeField] private GameObject _dialoguePanel;

        [Tooltip("화자의 이미지를 표시하는 Image 컴포넌트입니다.")]
        [SerializeField] private Image _characterImage;

        [Tooltip("화자의 이름을 표시하는 TMP_Text입니다.")]
        [SerializeField] private TMP_Text _nameText;

        [Tooltip("대사 내용을 표시하는 TMP_Text입니다.")]
        [SerializeField] private TMP_Text _dialogueText;

        [Header("캐릭터 스프라이트 (인덱스 = CharacterId)")]
        [Tooltip("[0]은 비워두고 [1]부터 캐릭터 스프라이트를 넣습니다.")]
        [SerializeField] private List<Sprite> _characterSprites = new();

        [Header("인게임 진행 설정")]
        [Tooltip("true = 클릭으로 다음 줄 진행 / false = 자동 진행")]
        [SerializeField] private bool _clickToAdvance = true;

        [Tooltip("자동 진행 시 각 줄마다 대기하는 시간(초)입니다.")]
        [SerializeField] private float _autoAdvanceDelay = 2.0f;

        [Tooltip("대사 시작 후 클릭을 무시할 시간(초)입니다. 기본 1.5초.")]
        [SerializeField] private float _clickBlockDuration = 1.5f;

        // ── Inspector — 엔딩 전용 ────────────────────────────────────────

        [Header("엔딩 UI")]
        [Tooltip("엔딩 전용 패널입니다. CanvasGroup 컴포넌트가 필요합니다.")]
        [SerializeField] private GameObject _endingPanel;

        [Tooltip("배경 이미지입니다. SpeakerId에 따라 교체됩니다.")]
        [SerializeField] private Image _backgroundImage;

        [Tooltip("클릭 대기 중 표시되는 아이콘 GameObject입니다. (선택)")]
        [SerializeField] private GameObject _clickIndicator;

        [Header("배경 스프라이트 (인덱스 = SpeakerId)")]
        [Tooltip("[0] = 공통 / [1]=#1 / ... / [7]=#7\n" +
                 "해당 SpeakerId 스프라이트가 없으면 [0]을 사용합니다.")]
        [SerializeField] private List<Sprite> _backgroundSprites = new();

        [Header("엔딩 진행 설정")]
        [Tooltip("엔딩: true = 클릭 진행 / false = 자동 진행")]
        [SerializeField] private bool _endingClickToAdvance = true;

        [Tooltip("엔딩 자동 진행 대기 시간 (초)")]
        [SerializeField] private float _endingAutoAdvanceDelay = 3.0f;

        [Header("타이핑 효과")]
        [Tooltip("글자당 출력 시간 (초). 0이면 즉시 출력합니다.")]
        [SerializeField] private float _typingSpeed = 0.03f;

        [Tooltip("타이핑 중 클릭하면 전체 텍스트를 즉시 표시합니다.")]
        [SerializeField] private bool _skipTypingOnClick = true;

        [Header("페이드 설정")]
        [Tooltip("패널/줄 전환 페이드 시간 (초)")]
        [SerializeField] private float _fadeDuration = 0.4f;

        [Tooltip("true = 줄마다 페이드 인/아웃\nfalse = 시작·끝만 페이드")]
        [SerializeField] private bool _fadePerLine = false;

        [Tooltip("엔딩 줄 시작 후 클릭을 차단할 시간 (초)")]
        [SerializeField] private float _endingClickBlockDuration = 0.8f;

        [Tooltip("true = SpeakerId가 0이면 이름을 숨깁니다. (내레이션용)")]
        [SerializeField] private bool _hideNameForSpeakerZero = true;

        // ── 퍼스널 컬러 ───────────────────────────────────────────────────

        private static readonly Color[] PersonalColors =
        {
            Color.white,                                    // [0] 내레이션/미사용
            new Color(0xC8/255f, 0xA8/255f, 0x88/255f),   // [1] 엔비  #C8A888
            new Color(0x48/255f, 0x78/255f, 0x48/255f),   // [2] 메이  #487848
            new Color(0x58/255f, 0x58/255f, 0x88/255f),   // [3] 데우스 #585888
            new Color(0xD8/255f, 0xD8/255f, 0xE8/255f),   // [4] 루이스 #D8D8E8
            new Color(0xE8/255f, 0xD8/255f, 0x98/255f),   // [5] 토니  #E8D898
            new Color(0xE8/255f, 0x88/255f, 0x68/255f),   // [6] 프리드 #E88868
            new Color(0x98/255f, 0x88/255f, 0x68/255f),   // [7] 새턴  #988868
        };

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private CanvasGroup _endingCanvasGroup;

        private bool _isPlaying;
        private bool _isNotifying; // 알림 코루틴 중 클릭 감지용
        private bool _waitingForClick;
        private bool _clickBlocked;
        private bool _isTyping;
        private bool _skipTyping;
        private Coroutine _playCoroutine;
        private Action _onComplete;

        public bool IsPlaying => _isPlaying;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_dialoguePanel != null) _dialoguePanel.SetActive(false);

            if (_endingPanel != null)
            {
                _endingPanel.SetActive(false);
                _endingCanvasGroup = _endingPanel.GetComponent<CanvasGroup>();
                if (_endingCanvasGroup == null)
                    _endingCanvasGroup = _endingPanel.AddComponent<CanvasGroup>();
            }

            if (_clickIndicator != null) _clickIndicator.SetActive(false);
        }

        private void Update()
        {
            if (_clickBlocked) return;
            if (!_isPlaying && !_isNotifying) return;
            if (!Input.GetMouseButtonDown(0)) return;

            // 타이핑 중 클릭 → 스킵
            if (_isTyping && _skipTypingOnClick)
            {
                _skipTyping = true;
                return;
            }

            if (_waitingForClick)
                _waitingForClick = false;
        }

        private void OnDestroy()
        {
            if (_playCoroutine != null) StopCoroutine(_playCoroutine);
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// 인게임 대화 조각을 재생합니다.
        /// FragmentDataSO.Lines → DTM → 이 메서드.
        /// </summary>
        public void Play(IReadOnlyList<FragmentDialogueLine> lines, Action onComplete = null)
        {
            if (_isPlaying) { Debug.LogWarning("[DialoguePlayer] 이미 재생 중"); return; }
            if (lines == null || lines.Count == 0) { onComplete?.Invoke(); return; }

            _onComplete = onComplete;
            _isPlaying = true;
            _playCoroutine = StartCoroutine(PlayFragmentCoroutine(lines));
        }

        /// <summary>
        /// 엔딩 대화를 재생합니다.
        /// ProfileDataSO.FinalTalkLine → FinalTalkUI → 이 메서드.
        /// 타이핑 효과 + 페이드 + 배경 교체 연출.
        /// </summary>
        public void Play(IReadOnlyList<FinalTalkLine> lines, Action onComplete = null)
        {
            if (_isPlaying) { Debug.LogWarning("[DialoguePlayer] 이미 재생 중"); return; }
            if (lines == null || lines.Count == 0) { onComplete?.Invoke(); return; }

            _onComplete = onComplete;
            _isPlaying = true;
            _playCoroutine = StartCoroutine(PlayEndingCoroutine(lines));
        }

        /// <summary>알림 텍스트를 표시합니다. 화자 이미지 없이 텍스트만 출력합니다.</summary>
        public void PlayNotification(List<string> messages, Action onComplete = null)
        {
            if (messages == null || messages.Count == 0) { onComplete?.Invoke(); return; }
            StartCoroutine(NotificationCoroutine(messages, onComplete));
        }

        /// <summary>재생 중인 대사를 즉시 종료합니다.</summary>
        public void Skip()
        {
            if (!_isPlaying) return;
            if (_playCoroutine != null) { StopCoroutine(_playCoroutine); _playCoroutine = null; }
            FinishPlay(hideDialogue: true, hideEnding: true);
        }

        /// <summary>ProfileDataSO에서 화자 이름을 반환합니다. 없으면 #ID.</summary>
        public string BuildNameText(int speakerId)
        {
            var profile = _profileData?.FindProfile(speakerId);
            return !string.IsNullOrEmpty(profile?.CharacterFullName)
                ? profile.CharacterFullName
                : $"#{speakerId}";
        }

        // ── 인게임 코루틴 ────────────────────────────────────────────────

        private IEnumerator PlayFragmentCoroutine(IReadOnlyList<FragmentDialogueLine> lines)
        {
            var first = lines[0];
            if (first != null)
            {
                if (int.TryParse(first.TextId, out int id)) UpdateInGameDisplay(id);
                if (_dialogueText != null) _dialogueText.text = first.Text ?? "";
            }

            if (_dialoguePanel != null) _dialoguePanel.SetActive(true);

            yield return BlockClickInGame();

            yield return WaitForAdvanceInGame();

            for (int i = 1; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null) continue;

                if (int.TryParse(line.TextId, out int speakerId))
                    UpdateInGameDisplay(speakerId);

                if (_dialogueText != null) _dialogueText.text = line.Text ?? "";

                yield return WaitForAdvanceInGame();
            }

            FinishPlay(hideDialogue: true, hideEnding: false);
        }

        private void UpdateInGameDisplay(int characterId)
        {
            Color color = GetPersonalColor(characterId);

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

        private IEnumerator BlockClickInGame()
        {
            yield return new WaitUntil(() => !Input.GetMouseButton(0));
            _clickBlocked = true;
            yield return new WaitForSeconds(_clickBlockDuration);
            _clickBlocked = false;
        }

        private IEnumerator WaitForAdvanceInGame()
        {
            if (_clickToAdvance)
            {
                _waitingForClick = true;
                yield return new WaitUntil(() => !_waitingForClick);
            }
            else
                yield return new WaitForSeconds(_autoAdvanceDelay);
        }

        // ── 엔딩 코루틴 ──────────────────────────────────────────────────

        private IEnumerator PlayEndingCoroutine(IReadOnlyList<FinalTalkLine> lines)
        {
            _endingPanel?.SetActive(true);
            yield return FadeIn();

            yield return new WaitUntil(() => !Input.GetMouseButton(0));
            yield return BlockClickEnding();

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null || string.IsNullOrEmpty(line.Text)) continue;

                if (_fadePerLine && i > 0)
                {
                    yield return FadeOut();
                    UpdateEndingDisplay(line);
                    yield return FadeIn();
                }
                else
                {
                    UpdateEndingDisplay(line);
                }

                yield return BlockClickEnding();
                yield return TypeText(line.Text);

                if (_clickIndicator != null)
                    _clickIndicator.SetActive(_endingClickToAdvance);

                if (_endingClickToAdvance)
                {
                    _waitingForClick = true;
                    yield return new WaitUntil(() => !_waitingForClick);
                }
                else
                    yield return new WaitForSeconds(_endingAutoAdvanceDelay);

                if (_clickIndicator != null) _clickIndicator.SetActive(false);
            }

            yield return FadeOut();
            FinishPlay(hideDialogue: false, hideEnding: true);
        }

        private void UpdateEndingDisplay(FinalTalkLine line)
        {
            int speakerId = line.SpeakerId;
            UpdateEndingBackground(speakerId);
            UpdateEndingNameText(speakerId);
            if (_dialogueText != null) _dialogueText.text = "";
        }

        private void UpdateEndingBackground(int speakerId)
        {
            if (_backgroundImage == null || _backgroundSprites == null) return;

            Sprite sprite = null;
            if (speakerId >= 0 && speakerId < _backgroundSprites.Count)
                sprite = _backgroundSprites[speakerId];
            if (sprite == null && _backgroundSprites.Count > 0)
                sprite = _backgroundSprites[0];

            if (sprite != null)
            {
                _backgroundImage.sprite = sprite;
                _backgroundImage.enabled = true;
            }
            else
                _backgroundImage.enabled = false;
        }

        private void UpdateEndingNameText(int speakerId)
        {
            if (_nameText == null) return;

            if (_hideNameForSpeakerZero && speakerId == 0)
            {
                _nameText.text = "";
                _nameText.enabled = false;
                return;
            }

            _nameText.enabled = true;
            _nameText.text = BuildNameText(speakerId);
            _nameText.color = GetPersonalColor(speakerId);
        }

        private IEnumerator TypeText(string fullText)
        {
            if (_dialogueText == null) yield break;

            if (_typingSpeed <= 0f) { _dialogueText.text = fullText; yield break; }

            _isTyping = true;
            _skipTyping = false;
            _dialogueText.text = "";

            foreach (char c in fullText)
            {
                if (_skipTyping) { _dialogueText.text = fullText; break; }
                _dialogueText.text += c;
                yield return new WaitForSeconds(_typingSpeed);
            }

            _isTyping = false;
            _skipTyping = false;
        }

        private IEnumerator FadeIn()
        {
            if (_endingCanvasGroup == null) yield break;
            _endingCanvasGroup.alpha = 0f;
            yield return _endingCanvasGroup.DOFade(1f, _fadeDuration)
                                           .SetEase(Ease.OutQuad)
                                           .WaitForCompletion();
        }

        private IEnumerator FadeOut()
        {
            if (_endingCanvasGroup == null) yield break;
            yield return _endingCanvasGroup.DOFade(0f, _fadeDuration)
                                           .SetEase(Ease.InQuad)
                                           .WaitForCompletion();
        }

        private IEnumerator BlockClickEnding()
        {
            _clickBlocked = true;
            yield return new WaitForSeconds(_endingClickBlockDuration);
            _clickBlocked = false;
        }

        // ── 알림 코루틴 ──────────────────────────────────────────────────

        private IEnumerator NotificationCoroutine(List<string> messages, Action onComplete)
        {
            _isNotifying = true;

            if (_dialoguePanel != null) _dialoguePanel.SetActive(true);
            if (_characterImage != null) _characterImage.enabled = false;
            if (_nameText != null) _nameText.text = "";

            _clickBlocked = true;
            yield return new WaitForSeconds(0.3f);
            _clickBlocked = false;

            foreach (var message in messages)
            {
                if (string.IsNullOrEmpty(message)) continue;
                if (_dialogueText != null) _dialogueText.text = message;

                _waitingForClick = true;
                yield return new WaitUntil(() => !_waitingForClick);
            }

            _isNotifying = false;
            if (_dialoguePanel != null) _dialoguePanel.SetActive(false);
            onComplete?.Invoke();
        }

        // ── 공통 종료 ────────────────────────────────────────────────────

        private void FinishPlay(bool hideDialogue, bool hideEnding)
        {
            _isPlaying = false;
            _waitingForClick = false;
            _clickBlocked = false;
            _isTyping = false;
            _skipTyping = false;
            _playCoroutine = null;

            if (hideDialogue && _dialoguePanel != null) _dialoguePanel.SetActive(false);
            if (hideEnding && _endingPanel != null) _endingPanel.SetActive(false);
            if (_clickIndicator != null) _clickIndicator.SetActive(false);

            var callback = _onComplete;
            _onComplete = null;
            callback?.Invoke();
        }

        // ── 유틸 ─────────────────────────────────────────────────────────

        private static Color GetPersonalColor(int characterId)
        {
            if (characterId < 0 || characterId >= PersonalColors.Length) return Color.white;
            return PersonalColors[characterId];
        }

        private Sprite GetSpriteById(int characterId)
        {
            if (_characterSprites == null) return null;
            if (characterId < 0 || characterId >= _characterSprites.Count) return null;
            return _characterSprites[characterId];
        }
    }
}