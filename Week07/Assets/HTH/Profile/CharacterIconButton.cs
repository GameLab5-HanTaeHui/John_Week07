using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HTH.Campaign
{
    /// <summary>
    /// 인물 기록장 좌측 캐릭터 그리드의 선택 버튼 1개입니다.
    ///
    /// ─── 동작 방식 ───────────────────────────────────────────────────────
    ///   미수집/수집 여부와 관계없이 항상 아이콘 완전 표시
    ///   클릭 시 선택 상태로 전환 (배경색 변경)
    ///   이미 선택된 버튼은 다시 클릭해도 무시
    ///   다른 버튼 클릭 시 이 버튼 선택 해제 (CharacterRecordBook에서 관리)
    ///   버튼 누름 효과(Press) 없음 — Transition을 None으로 설정
    ///
    /// ─── 이름 표시 ───────────────────────────────────────────────────────
    ///   이름 미수집: _unknownNameText 표시 (예: "$%&")
    ///   이름 수집됨: 실제 이름 표시 (예: "엔비")
    ///   텍스트는 아이콘 우측 하단에 배치합니다.
    ///
    /// ─── 프리팹 구조 ─────────────────────────────────────────────────────
    ///   CharacterIconButton (Button + 이 컴포넌트)
    ///   ├── Background  (Image)    ← 선택 상태 배경색
    ///   ├── IconImage   (Image)    ← 캐릭터 아이콘
    ///   └── NameText    (TMP_Text) ← 이름, 우측 하단 배치
    ///
    /// ─── Inspector 설정 ──────────────────────────────────────────────────
    ///   Button 컴포넌트 → Transition: None  ← 누름 효과 제거
    ///   NameText Rect Transform:
    ///       Anchor: Bottom Right
    ///       Pivot: (1, 0)
    ///       Pos X: -4 / Pos Y: 4 (여백)
    ///       Alignment: Bottom Right
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class CharacterIconButton : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Image _iconImage;

        [Tooltip("아이콘 우측 하단에 표시되는 이름 텍스트입니다.\n" +
                 "Rect Transform → Anchor: Bottom Right으로 설정하세요.")]
        [SerializeField] private TMP_Text _nameText;

        [Tooltip("아이콘 우측 하단에 표시되는 이름 텍스트입니다.\n" +
                 "Rect Transform → Anchor: Bottom Right으로 설정하세요.")]
        [SerializeField] private GameObject _completeText;

        [Header("선택 상태 색상")]
        [Tooltip("선택되지 않은 상태의 배경색")]
        [SerializeField] private Color _normalColor = new Color(0.2f, 0.2f, 0.2f, 0f);

        [Tooltip("선택된 상태의 배경색")]
        [SerializeField] private Color _selectedColor = new Color(0.8f, 0.6f, 0.2f, 0.8f);
        // ── 내부 상태 ─────────────────────────────────────────────────────

        private Button _button;
        private int _characterId;
        private Action _onClicked;
        private string _collectedName;
        private bool _isSelected;

        /// <summary>이 버튼에 연결된 캐릭터 ID입니다.</summary>
        public int CharacterId => _characterId;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            _button = GetComponent<Button>();

            // 누름 효과 제거
            if (_button != null)
                _button.transition = Selectable.Transition.None;
        }

        // ── 초기화 ───────────────────────────────────────────────────────

        /// <summary>
        /// 버튼을 초기화합니다.
        /// CharacterRecordBook.BuildGrid()에서 호출합니다.
        /// </summary>
        public void Setup(int characterId, Sprite icon, string collectedName, Action onClicked)
        {
            _characterId = characterId;
            _onClicked = onClicked;
            _collectedName = collectedName;

            if (_iconImage != null)
            {
                _iconImage.sprite = icon;
                _iconImage.enabled = icon != null;
            }

            // 이름 텍스트 기입
            if (_nameText != null)
                _nameText.text = string.IsNullOrEmpty(collectedName) ? "" : collectedName;


            SetSelected(false);

            // _button이 null이면 Awake가 실행 안 된 것
            if (_button == null)
                _button = GetComponent<Button>();

            _button?.onClick.AddListener(() => { if (_isSelected) return; _onClicked?.Invoke(); });
        }

        // ── 상태 갱신 ─────────────────────────────────────────────────────

        /// <summary>
        /// 선택 상태를 설정합니다.
        /// CharacterRecordBook에서 버튼 그룹 관리 시 호출합니다.
        /// true  → 배경 선택 색상으로 변경, 재클릭 차단
        /// false → 배경 기본 색상으로 변경, 재클릭 허용
        /// </summary>
        public void SetSelected(bool selected)
        {
            _isSelected = selected;

            if (_background != null)
                _background.color = selected ? _selectedColor : _normalColor;
        }

        /// <summary>
        /// 이름을 갱신합니다.
        /// 이름 공개 시 CharacterRecordBook.RegisterCharacterName()에서 호출합니다.
        /// </summary>
        public void UpdateName(string name)
        {
            _collectedName = name;
            if (_nameText != null)
                _nameText.text = string.IsNullOrEmpty(name) ? "" : name;
        }

        // ── 클리어 상태 갱신 ──────────────────────────────────
        /// <summary>
        /// 클리어(에필로그 해금) 여부에 따라 전용 텍스트/마크를 켜거나 끕니다.
        /// ProfileInquirySelectPanel.RefreshButtonStates()에서 호출합니다.
        /// </summary>
        public void SetClearedState(bool isCleared)
        {
            if (isCleared) _completeText.SetActive(isCleared);
            else _completeText.SetActive(isCleared);

            // 로그를 통해 실제로 활성화 됐는지, 그리고 이 오브젝트가 '복제본'이 맞는지 확인
            Debug.Log($"[#{_characterId}] {gameObject.name}의 텍스트 상태: {_completeText.activeSelf} / 오브젝트 경로: {(_completeText)}");
        }

        // ── Private ──────────────────────────────────────────────────────


        private void OnDestroy()
        {
            _button?.onClick.RemoveAllListeners();
        }
    }
}