using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 최종 결정 UI입니다. 씬에 미리 배치된 슬롯·카드를 사용합니다.
///
/// 흐름:
///   Submit 클릭 → (오답 시) 정답 카드 표시 → 클릭 대기 → PreDialogueObject 활성화 → SubmitFinalDecision()
///   → 게임 종료 다이얼로그(DialogueManager)
///   → OnGameEndDialogueComplete → PreDialogueObject 비활성화 → Win/Lose 텍스트 활성화
///   → _continueDelay 후 Continue 텍스트 활성화
///   → 아무 곳 클릭 → FinishGameEndDialogue() → WinState/LoseState → 로비
/// </summary>
public class FinalDecisionUI : MonoBehaviour
{
    [Header("빈칸 슬롯 (배열 인덱스 = CharacterId)")]
    [SerializeField] private RoleSlot[] _roleSlots;

    [Header("직업 카드 (RoleType은 Inspector에서 enum으로 설정)")]
    [SerializeField] private RoleCard[] _roleCards;

    [Header("결과 텍스트 — 다이얼로그 종료 후 활성화")]
    [SerializeField] private TMP_Text _winText;
    [SerializeField] private TMP_Text _loseText;
    [SerializeField] private TMP_Text _continueText;

    [Header("제출 후 다이얼로그 전 활성화할 오브젝트")]
    [SerializeField] private GameObject _preDialogueObject;

    [Header("버튼")]
    [SerializeField] private Button _submitButton;

    [Header("연출 설정")]
    [SerializeField] private float _fadeInDuration = 0.6f;
    [SerializeField] private float _continueDelay  = 2f;

    [Header("오답 정답 표시")]
    [Tooltip("정답 카드 표시 후 입력을 막을 시간(초)")]
    [SerializeField] private float _wrongAnswerBlockDuration = 2f;
    [Tooltip("틀린 슬롯 자식 텍스트에 적용할 색상")]
    [SerializeField] private Color _wrongTextColor = Color.red;

    private CanvasGroup _canvasGroup;
    private bool        _awaitingContinueClick;
    private bool        _awaitingWrongAnswerClick;
    private Coroutine   _wrongAnswerCo;

    // ── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        var gfc = GameFlowController.Instance;
        if (gfc != null)
        {
            gfc.OnFinalDecisionEntered    += Show;
            gfc.OnFinalDecisionExited     += Hide;
            gfc.OnGameEndDialogueComplete += HandleGameEndDialogueComplete;
        }

        if (_submitButton != null)
            _submitButton.onClick.AddListener(OnSubmitClicked);

        if (_preDialogueObject != null) _preDialogueObject.SetActive(false);
        HideText(_winText);
        HideText(_loseText);
        HideText(_continueText);

        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        var gfc = GameFlowController.Instance;
        if (gfc == null) return;
        gfc.OnFinalDecisionEntered    -= Show;
        gfc.OnFinalDecisionExited     -= Hide;
        gfc.OnGameEndDialogueComplete -= HandleGameEndDialogueComplete;
    }

    private void Update()
    {
        if (_awaitingWrongAnswerClick)
        {
            if (Input.GetMouseButtonDown(0))
            {
                _awaitingWrongAnswerClick = false;
                if (_preDialogueObject != null) _preDialogueObject.SetActive(true);
                GameFlowController.Instance?.SubmitFinalDecision(false);
            }
            return;
        }

        if (!_awaitingContinueClick) return;
        if (Input.GetMouseButtonDown(0))
        {
            _awaitingContinueClick = false;
            GameFlowController.Instance?.FinishGameEndDialogue();
        }
    }

    // ── 표시 / 숨기기 ────────────────────────────────────────────────────────

    private void Show()
    {
        gameObject.SetActive(true);
        _awaitingContinueClick = false;
        if (_preDialogueObject != null) _preDialogueObject.SetActive(false);
        HideText(_winText);
        HideText(_loseText);
        HideText(_continueText);

        for (int i = 0; i < _roleSlots.Length; i++)
            if (_roleSlots[i] != null) _roleSlots[i].Init();

        if (_submitButton != null)
            _submitButton.interactable = true;

        _canvasGroup.alpha = 0f;
        _canvasGroup.DOFade(1f, _fadeInDuration).SetEase(Ease.OutQuad).SetLink(gameObject);
    }

    private void Hide()
    {
        _awaitingContinueClick    = false;
        _awaitingWrongAnswerClick = false;
        if (_wrongAnswerCo != null) { StopCoroutine(_wrongAnswerCo); _wrongAnswerCo = null; }
        DOTween.Kill(gameObject);
        foreach (var slot in _roleSlots) slot?.ForceRelease();
        foreach (var card in _roleCards) card?.ReturnHomeInstant();
        if (_preDialogueObject != null) _preDialogueObject.SetActive(false);
        HideText(_winText);
        HideText(_loseText);
        HideText(_continueText);
        _canvasGroup.alpha = 1f;
        gameObject.SetActive(false);
    }

    // ── 제출 & 판정 ──────────────────────────────────────────────────────────

    private void OnSubmitClicked()
    {
        var gfc = GameFlowController.Instance;
        if (gfc == null) return;

        foreach (var slot in _roleSlots)
            if (slot != null && slot.AssignedCard == null) return;

        var wrongSlots = new List<RoleSlot>();
        foreach (var slot in _roleSlots)
        {
            if (slot == null || slot.AssignedCard == null) continue;
            var actual   = gfc.GetActualRole(slot.CharacterId);
            var assigned = slot.AssignedCard.RoleType;
            bool correct = assigned == actual;
            Debug.Log($"[FinalDecision] CharId={slot.CharacterId}  배정={assigned}  정답={actual}  → {(correct ? "O" : "X")}");
            if (!correct) wrongSlots.Add(slot);
        }

        _submitButton.interactable = false;

        if (wrongSlots.Count > 0)
        {
            ShowCorrectAnswers(wrongSlots);
            if (_wrongAnswerCo != null) StopCoroutine(_wrongAnswerCo);
            _wrongAnswerCo = StartCoroutine(WaitThenAllowDialogue());
        }
        else
        {
            if (_preDialogueObject != null) _preDialogueObject.SetActive(true);
            gfc.SubmitFinalDecision(true);
        }
    }

    private void ShowCorrectAnswers(List<RoleSlot> wrongSlots)
    {
        var gfc = GameFlowController.Instance;
        if (gfc == null) return;

        // 틀린 슬롯의 현재 카드를 홈으로 돌려보냄
        foreach (var slot in wrongSlots)
            if (slot.AssignedCard != null) slot.AssignedCard.ReturnHome();

        // 모든 슬롯에 정답 카드 스냅, 틀린 슬롯에 붙은 카드 텍스트는 빨간색
        var wrongSet = new HashSet<RoleSlot>(wrongSlots);
        foreach (var slot in _roleSlots)
        {
            if (slot == null) continue;
            var actualRole = gfc.GetActualRole(slot.CharacterId);
            foreach (var card in _roleCards)
            {
                if (card == null || card.RoleType != actualRole) continue;
                card.SnapToSlot(slot);
                if (wrongSet.Contains(slot))
                {
                    foreach (var text in card.GetComponentsInChildren<TMP_Text>())
                        text.color = _wrongTextColor;
                }
                break;
            }
        }
    }

    private IEnumerator WaitThenAllowDialogue()
    {
        yield return new WaitForSeconds(_wrongAnswerBlockDuration);
        _awaitingWrongAnswerClick = true;
        _wrongAnswerCo = null;
    }

    // ── 게임 종료 결과 텍스트 ────────────────────────────────────────────────

    private void HandleGameEndDialogueComplete(bool isWin)
    {
        var resultText = isWin ? _winText : _loseText;
        if (resultText != null) resultText.gameObject.SetActive(true);

        DOVirtual.DelayedCall(_continueDelay, () =>
        {
            if (_continueText != null) _continueText.gameObject.SetActive(true);
            _awaitingContinueClick = true;
        }).SetLink(gameObject);
    }

    // ── 유틸 ─────────────────────────────────────────────────────────────────

    private static void HideText(TMP_Text text)
    {
        if (text == null) return;
        text.gameObject.SetActive(false);
    }
}
