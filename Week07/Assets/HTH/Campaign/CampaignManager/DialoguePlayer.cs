using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HTH.Campaign
{
    /// <summary>
    /// 대화 조각 대사를 순서대로 출력합니다.
    ///
    /// ─── 변경 이력 ───────────────────────────────────────────────────────
    ///   FragmentDataSO 단일 SO 병합으로 DialogueType(Core/Hint/Special/Normal) 제거.
    ///   _codeText / _currentTypeLabel / GetTypeLabel() 제거.
    ///   Play() 시그니처에서 dialogueType 파라미터 제거.
    ///   입력 타입: IReadOnlyList<FragmentDialogueLine> (FragmentDataSO.Lines와 직결)
    ///   호환용 오버로드: IReadOnlyList<DialogueLine> (기존 시스템 연동용)
    ///
    /// ─── 역할 ────────────────────────────────────────────────────────────
    ///   FragmentDialogueLine / DialogueLine 목록을 받아 화면에 순서대로 출력합니다.
    ///   화자 ID(TextId)로 퍼스널 컬러와 스프라이트를 결정합니다.
    ///   클릭 또는 자동 진행으로 다음 줄로 넘어갑니다.
    ///   모든 대사 완료 시 onComplete 콜백을 호출합니다.
    ///
    /// ─── Inspector 연결 ──────────────────────────────────────────────────
    ///   Dialogue Panel       → Canvas/DialoguePanel
    ///   Character Image      → 화자 이미지 Image
    ///   Name Text            → 화자 이름 TMP_Text
    ///   Dialogue Text        → 대사 내용 TMP_Text
    ///   Character Sprites    → 캐릭터별 스프라이트 (인덱스 = CharacterId, [0] 비워두기)
    ///   Click To Advance     → true=클릭 진행 / false=자동 진행
    ///   Auto Advance Delay   → 자동 진행 대기 시간 (초)
    ///   Click Block Duration → 대사 시작 후 클릭 차단 시간 (기본 1.5초)
    /// </summary>
    [DisallowMultipleComponent]
    public class DialoguePlayer : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────

        [Header("UI 참조")]
        [Tooltip("다이얼로그 전체 패널입니다.")]
        [SerializeField] private GameObject _dialoguePanel;

        [Tooltip("화자의 이미지를 표시하는 Image 컴포넌트입니다.")]
        [SerializeField] private Image _characterImage;

        [Tooltip("화자의 이름을 표시하는 TMP_Text입니다.")]
        [SerializeField] private TMP_Text _nameText;

        [Tooltip("대사 내용을 표시하는 TMP_Text입니다.")]
        [SerializeField] private TMP_Text _dialogueText;

        [Header("캐릭터 데이터")]
        [Tooltip("화자 이름 표시용 ProfileDataSO 에셋입니다.")]
        [SerializeField] private ProfileDataSO _profileData;

        [Header("캐릭터 스프라이트 (인덱스 = CharacterId)")]
        [Tooltip("[0]은 비워두고 [1]부터 캐릭터 스프라이트를 넣습니다.")]
        [SerializeField] private List<Sprite> _characterSprites = new();

        [Header("진행 설정")]
        [Tooltip("true = 클릭으로 다음 줄 진행 / false = 자동 진행")]
        [SerializeField] private bool _clickToAdvance = true;

        [Tooltip("자동 진행 시 각 줄마다 대기하는 시간(초)입니다.")]
        [SerializeField] private float _autoAdvanceDelay = 2.0f;

        [Header("클릭 차단")]
        [Tooltip("대사 시작 후 클릭을 무시할 시간(초)입니다. 기본 1.5초.")]
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
        /// FragmentDialogueLine 목록을 순서대로 재생합니다.
        /// FragmentDataSO.Lines와 직결됩니다.
        /// </summary>
        public void Play(IReadOnlyList<FragmentDialogueLine> lines, Action onComplete = null)
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
            _playCoroutine = StartCoroutine(PlayFragmentLinesCoroutine(lines));
        }
        /// <summary>알림 텍스트를 순서대로 표시합니다. 화자 이미지 없이 텍스트만 출력합니다.</summary>
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

        private IEnumerator PlayFragmentLinesCoroutine(IReadOnlyList<FragmentDialogueLine> lines)
        {
            // 첫 줄 세팅 후 패널 활성화
            var first = lines[0];
            if (first != null)
            {
                if (int.TryParse(first.TextId, out int id)) UpdateCharacterDisplay(id);
                if (_dialogueText != null) _dialogueText.text = first.Text ?? "";
            }

            if (_dialoguePanel != null) _dialoguePanel.SetActive(true);

            yield return BlockClick();

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

                if (_dialogueText != null) _dialogueText.text = line.Text ?? "";

                yield return WaitForAdvance();
            }

            FinishPlay();
        }
        private IEnumerator NotificationCoroutine(List<string> messages, Action onComplete)
        {
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

            if (_dialoguePanel != null) _dialoguePanel.SetActive(false);
            onComplete?.Invoke();
        }

        private void FinishPlay()
        {
            _isPlaying = false;
            _waitingForClick = false;
            _clickBlocked = false;
            _playCoroutine = null;

            if (_dialoguePanel != null) _dialoguePanel.SetActive(false);

            var callback = _onComplete;
            _onComplete = null;
            callback?.Invoke();
        }

        // ── Private — 진행 유틸 ───────────────────────────────────────────

        /// <summary>마우스 버튼 완전 해제 대기 + _clickBlockDuration 차단</summary>
        private IEnumerator BlockClick()
        {
            yield return new WaitUntil(() => !Input.GetMouseButton(0));
            _clickBlocked = true;
            yield return new WaitForSeconds(_clickBlockDuration);
            _clickBlocked = false;
        }

        /// <summary>클릭 또는 자동 대기</summary>
        private IEnumerator WaitForAdvance()
        {
            if (_clickToAdvance)
            {
                _waitingForClick = true;
                yield return new WaitUntil(() => !_waitingForClick);
            }
            else
                yield return new WaitForSeconds(_autoAdvanceDelay);
        }

        // ── Private — 화자 표시 ───────────────────────────────────────────

        private void UpdateCharacterDisplay(int characterId)
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

        /// <summary>
        /// 화자 ID로 이름을 반환합니다.
        /// ProfileDataSO에서 CharacterFullName을 조회하고, 없으면 #ID 폴백.
        /// </summary>
        public string BuildNameText(int speakerId)
        {
            var profile = _profileData?.FindProfile(speakerId);
            return !string.IsNullOrEmpty(profile?.CharacterFullName)
                ? profile.CharacterFullName
                : $"#{speakerId}";
        }

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