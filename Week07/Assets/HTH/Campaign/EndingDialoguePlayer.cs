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
    /// 캠페인 엔딩 전용 DialoguePlayer입니다.
    /// 기존 DialoguePlayer와 완전히 독립된 컴포넌트입니다.
    ///
    /// ─── 기존 DialoguePlayer와의 차이 ───────────────────────────────────
    ///   기존: 인게임 대화 — 화자 이미지 + 이름 + 대사 / 클릭 진행
    ///   엔딩: 소설체 연출 — 타이핑 효과 + 페이드 전환 + 배경 교체 / 클릭 또는 자동
    ///
    /// ─── 연출 흐름 ───────────────────────────────────────────────────────
    ///   Play(lines) 호출
    ///     → 패널 페이드 인
    ///     → 각 줄마다:
    ///         배경 이미지 교체 (BackgroundImage, SpeakerId 기반)
    ///         화자 이름 표시 (퍼스널 컬러)
    ///         타이핑 효과로 대사 출력
    ///         클릭 또는 AutoAdvanceDelay 대기
    ///         줄 사이 페이드 전환 (FadePerLine = true 시)
    ///     → 모든 줄 완료 → 패널 페이드 아웃 → onComplete 호출
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Panel                → 전체 패널 GameObject (CanvasGroup 필요)
    ///   Background Image     → 배경 이미지 Image
    ///   Name Text            → 화자 이름 TMP (없으면 숨김)
    ///   Dialogue Text        → 대사 내용 TMP
    ///   Click Indicator      → "클릭하여 계속" 아이콘 GameObject (선택)
    ///   Background Sprites   → SpeakerId 인덱스 기반 배경 스프라이트
    ///                          [0] = 공통 배경 / [1]=#1 / [2]=#2 ... [7]=#7
    ///   Click To Advance     → true = 클릭 진행 / false = 자동 진행
    ///   Auto Advance Delay   → 자동 진행 대기 시간 (초)
    ///   Typing Speed         → 타이핑 속도 (초/글자, 기본 0.03)
    ///   Fade Duration        → 페이드 시간 (초, 기본 0.4)
    ///   Fade Per Line        → true = 줄마다 페이드 / false = 시작·끝만 페이드
    ///   Click Block Duration → 줄 시작 후 클릭 차단 시간 (기본 0.8초)
    ///   Hide Name For Speaker Zero → SpeakerId=0이면 이름 숨김 (내레이션용)
    /// </summary>
    [DisallowMultipleComponent]
    public class EndingDialoguePlayer : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("데이터")]
        [Tooltip("화자 이름 조회용 ProfileDataSO입니다.")]
        [SerializeField] private ProfileDataSO _profileData;

        [Header("UI 참조")]
        [Tooltip("전체 패널입니다. CanvasGroup 컴포넌트가 필요합니다.")]
        [SerializeField] private GameObject _panel;

        [Tooltip("배경 이미지입니다. SpeakerId에 따라 교체됩니다.")]
        [SerializeField] private Image _backgroundImage;

        [Tooltip("화자 이름 TMP입니다. null이면 이름 표시를 생략합니다.")]
        [SerializeField] private TMP_Text _nameText;

        [Tooltip("대사 내용 TMP입니다.")]
        [SerializeField] private TMP_Text _dialogueText;

        [Tooltip("클릭 대기 중 표시되는 아이콘 GameObject입니다. (선택)")]
        [SerializeField] private GameObject _clickIndicator;

        [Header("배경 스프라이트 (인덱스 = SpeakerId)")]
        [Tooltip("[0] = 공통 / [1]=#1 / ... / [7]=#7\n" +
                 "해당 SpeakerId 스프라이트가 없으면 [0]을 사용합니다.")]
        [SerializeField] private List<Sprite> _backgroundSprites = new();

        [Header("진행 설정")]
        [Tooltip("true = 클릭으로 다음 줄 진행\nfalse = 자동 진행")]
        [SerializeField] private bool _clickToAdvance = true;

        [Tooltip("자동 진행 시 대기 시간 (초)")]
        [SerializeField] private float _autoAdvanceDelay = 3.0f;

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

        [Header("클릭 차단")]
        [Tooltip("줄 시작 후 클릭을 차단할 시간 (초)\n타이핑 중 의도치 않은 스킵 방지")]
        [SerializeField] private float _clickBlockDuration = 0.8f;

        [Header("화자 표시")]
        [Tooltip("true = SpeakerId가 0이면 이름을 숨깁니다. (내레이션용)")]
        [SerializeField] private bool _hideNameForSpeakerZero = true;

        // ── 퍼스널 컬러 (DialoguePlayer와 동일) ──────────────────────────

        private static readonly Color[] PersonalColors =
        {
            Color.white,                                    // [0] 내레이션
            new Color(0xC8/255f, 0xA8/255f, 0x88/255f),   // [1] 엔비  #C8A888
            new Color(0x48/255f, 0x78/255f, 0x48/255f),   // [2] 메이  #487848
            new Color(0x58/255f, 0x58/255f, 0x88/255f),   // [3] 데우스 #585888
            new Color(0xD8/255f, 0xD8/255f, 0xE8/255f),   // [4] 루이스 #D8D8E8
            new Color(0xE8/255f, 0xD8/255f, 0x98/255f),   // [5] 토니  #E8D898
            new Color(0xE8/255f, 0x88/255f, 0x68/255f),   // [6] 프리드 #E88868
            new Color(0x98/255f, 0x88/255f, 0x68/255f),   // [7] 새턴  #988868
        };

        // ── 내부 상태 ─────────────────────────────────────────────────────

        private CanvasGroup _canvasGroup;
        private Coroutine _playCoroutine;
        private Action _onComplete;

        private bool _isPlaying;
        private bool _waitingForClick;
        private bool _clickBlocked;
        private bool _isTyping;
        private bool _skipTyping;

        public bool IsPlaying => _isPlaying;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
                _canvasGroup = _panel.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                    _canvasGroup = _panel.AddComponent<CanvasGroup>();
            }

            if (_clickIndicator != null)
                _clickIndicator.SetActive(false);
        }

        private void Update()
        {
            if (!_isPlaying || _clickBlocked) return;
            if (!Input.GetMouseButtonDown(0)) return;

            // 타이핑 중 클릭 → 타이핑 스킵
            if (_isTyping && _skipTypingOnClick)
            {
                _skipTyping = true;
                return;
            }

            // 클릭 대기 중 → 다음 줄로
            if (_waitingForClick)
                _waitingForClick = false;
        }

        private void OnDestroy()
        {
            if (_playCoroutine != null)
                StopCoroutine(_playCoroutine);
        }

        // ── 공개 API ─────────────────────────────────────────────────────

        /// <summary>
        /// FinalTalkLine 목록을 엔딩 연출로 재생합니다.
        /// ProfileInquiryUI의 결과 대화 / 엔딩 시퀀스에서 호출합니다.
        /// </summary>
        public void Play(IReadOnlyList<FinalTalkLine> lines, Action onComplete = null)
        {
            if (_isPlaying)
            {
                Debug.LogWarning("[EndingDialoguePlayer] 이미 재생 중 — 무시됩니다.");
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

        /// <summary>재생 중인 대사를 즉시 종료합니다.</summary>
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

        // ── Private — 메인 코루틴 ─────────────────────────────────────────

        private IEnumerator PlayCoroutine(IReadOnlyList<FinalTalkLine> lines)
        {
            // 패널 활성화 + 페이드 인
            _panel?.SetActive(true);
            yield return FadeIn();

            // 이전 마우스 버튼 해제 대기 + 클릭 차단
            yield return new WaitUntil(() => !Input.GetMouseButton(0));
            yield return BlockClick(_clickBlockDuration);

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null || string.IsNullOrEmpty(line.Text)) continue;

                // 줄마다 페이드 아웃 → 콘텐츠 교체 → 페이드 인
                if (_fadePerLine && i > 0)
                {
                    yield return FadeOut();
                    UpdateDisplay(line);
                    yield return FadeIn();
                }
                else
                {
                    UpdateDisplay(line);
                }

                // 클릭 차단
                yield return BlockClick(_clickBlockDuration);

                // 타이핑 효과
                yield return TypeText(line.Text);

                // 클릭 인디케이터 표시
                if (_clickIndicator != null)
                    _clickIndicator.SetActive(_clickToAdvance);

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

                if (_clickIndicator != null)
                    _clickIndicator.SetActive(false);
            }

            // 패널 페이드 아웃
            yield return FadeOut();

            FinishPlay();
        }

        // ── Private — 타이핑 효과 ─────────────────────────────────────────

        private IEnumerator TypeText(string fullText)
        {
            if (_dialogueText == null) yield break;

            if (_typingSpeed <= 0f)
            {
                _dialogueText.text = fullText;
                yield break;
            }

            _isTyping = true;
            _skipTyping = false;
            _dialogueText.text = "";

            foreach (char c in fullText)
            {
                if (_skipTyping)
                {
                    _dialogueText.text = fullText;
                    break;
                }
                _dialogueText.text += c;
                yield return new WaitForSeconds(_typingSpeed);
            }

            _isTyping = false;
            _skipTyping = false;
        }

        // ── Private — 페이드 ─────────────────────────────────────────────

        private IEnumerator FadeIn()
        {
            if (_canvasGroup == null) yield break;
            _canvasGroup.alpha = 0f;
            yield return _canvasGroup.DOFade(1f, _fadeDuration)
                                     .SetEase(Ease.OutQuad)
                                     .WaitForCompletion();
        }

        private IEnumerator FadeOut()
        {
            if (_canvasGroup == null) yield break;
            yield return _canvasGroup.DOFade(0f, _fadeDuration)
                                     .SetEase(Ease.InQuad)
                                     .WaitForCompletion();
        }

        // ── Private — 클릭 차단 ───────────────────────────────────────────

        private IEnumerator BlockClick(float duration)
        {
            _clickBlocked = true;
            yield return new WaitForSeconds(duration);
            _clickBlocked = false;
        }

        // ── Private — 화면 갱신 ───────────────────────────────────────────

        private void UpdateDisplay(FinalTalkLine line)
        {
            int speakerId = line.SpeakerId;

            // 배경 이미지 교체
            UpdateBackground(speakerId);

            // 이름 표시
            UpdateNameText(speakerId);

            // 대사 초기화 (타이핑 전)
            if (_dialogueText != null)
                _dialogueText.text = "";
        }

        private void UpdateBackground(int speakerId)
        {
            if (_backgroundImage == null || _backgroundSprites == null) return;

            Sprite sprite = null;

            // SpeakerId 인덱스 스프라이트 → 없으면 [0] 공통 배경
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
            {
                _backgroundImage.enabled = false;
            }
        }

        private void UpdateNameText(int speakerId)
        {
            if (_nameText == null) return;

            // SpeakerId = 0이면 내레이션 — 이름 숨김
            if (_hideNameForSpeakerZero && speakerId == 0)
            {
                _nameText.text = "";
                _nameText.enabled = false;
                return;
            }

            _nameText.enabled = true;

            // 수집된 이름 → ProfileDataSO.CharacterFullName → #ID
            string name = CharacterRecordPanelManager.Instance?.GetCollectedName(speakerId);
            if (string.IsNullOrEmpty(name))
            {
                var profile = _profileData?.FindProfile(speakerId);
                name = profile?.CharacterFullName;
            }
            _nameText.text = !string.IsNullOrEmpty(name) ? name : $"#{speakerId}";
            _nameText.color = GetPersonalColor(speakerId);
        }

        private void FinishPlay()
        {
            _isPlaying = false;
            _waitingForClick = false;
            _clickBlocked = false;
            _isTyping = false;
            _skipTyping = false;
            _playCoroutine = null;

            if (_panel != null) _panel.SetActive(false);
            if (_clickIndicator != null) _clickIndicator.SetActive(false);

            var callback = _onComplete;
            _onComplete = null;
            callback?.Invoke();
        }

        // ── Private — 유틸 ───────────────────────────────────────────────

        private static Color GetPersonalColor(int characterId)
        {
            if (characterId < 0 || characterId >= PersonalColors.Length)
                return Color.white;
            return PersonalColors[characterId];
        }
    }
}