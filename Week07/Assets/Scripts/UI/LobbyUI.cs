using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 로비 화면을 관리합니다.
///
/// 진입 시 튜토리얼 진행 여부를 확인합니다.
///   - 미진행 → 튜토리얼 씬으로 자동 이동
///   - 진행 후(실패 포함) → 로비 표시, "튜토리얼 다시하기" 버튼 노출
///
/// Canvas 구조 예시:
///   LobbyUI
///   ├── MainPanel
///   │   ├── ContinueButton       ← 세이브 없으면 비활성
///   │   ├── NewGameButton
///   │   └── TutorialRetryButton  ← IsStarted 일 때만 노출
///   └── NewGamePanel
///       ├── SeedInputField
///       ├── StartSeedButton
///       ├── StartRandomButton
///       └── BackButton
/// </summary>
public class LobbyUI : MonoBehaviour
{
    [Header("씬 이름")]
    [SerializeField] private string _gameSceneName         = "GameScene";
    [SerializeField] private string _tutorialSceneName     = "TutorialScene";
    [SerializeField] private string _tutorialRetrySceneName = "TutorialRetryScene";

    [Header("튜토리얼 고정 시드")]
    [SerializeField] private int _tutorialFixedSeed = 0;

    [Header("메인 패널")]
    [SerializeField] private GameObject _mainPanel;
    [SerializeField] private Button     _continueButton;
    [SerializeField] private Button     _newGameButton;
    [SerializeField] private Button     _tutorialRetryButton;

    [Header("새로하기 패널")]
    [SerializeField] private GameObject     _newGamePanel;
    [SerializeField] private TMP_InputField _seedInputField;
    [SerializeField] private Button         _startSeedButton;
    [SerializeField] private Button         _startRandomButton;
    [SerializeField] private Button         _backButton;

    // ── Unity ────────────────────────────────────────────────────────────────

    private void Start()
    {
        TutorialProgressRepository.Instance.TryLoad();

        // 튜토리얼 미진행 → 자동으로 튜토리얼 씬 이동
        if (!TutorialProgressRepository.Instance.IsStarted)
        {
            NewGameConfig.SetTutorial(_tutorialFixedSeed);
            SceneManager.LoadScene(_tutorialSceneName);
            return;
        }

        SetupButtons();
    }

    // ── 버튼 설정 ─────────────────────────────────────────────────────────────

    private void SetupButtons()
    {
        if (_continueButton != null)
            _continueButton.interactable = false;

        // 튜토리얼 다시하기: 진행한 적 있을 때만 표시
        if (_tutorialRetryButton != null)
            _tutorialRetryButton.gameObject.SetActive(TutorialProgressRepository.Instance.IsStarted);

        _continueButton?.onClick.AddListener(OnContinueClicked);
        _newGameButton?.onClick.AddListener(OnNewGameClicked);
        _tutorialRetryButton?.onClick.AddListener(OnTutorialRetryClicked);
        _startSeedButton?.onClick.AddListener(OnStartWithSeedClicked);
        _startRandomButton?.onClick.AddListener(OnStartRandomClicked);
        _backButton?.onClick.AddListener(ShowMain);
    }

    // ── 버튼 콜백 ─────────────────────────────────────────────────────────────

    private void OnContinueClicked()
    {
        NewGameConfig.Clear();
        SceneManager.LoadScene(_gameSceneName);
    }

    private void OnNewGameClicked()
    {
        if (_mainPanel != null)    _mainPanel.SetActive(false);
        if (_newGamePanel != null) _newGamePanel.SetActive(true);
        if (_seedInputField != null) _seedInputField.text = "";
    }

    private void OnTutorialRetryClicked()
    {
        NewGameConfig.SetTutorial(_tutorialFixedSeed);
        SceneManager.LoadScene(_tutorialRetrySceneName);
    }

    private void OnStartWithSeedClicked()
    {
        if (_seedInputField == null || !int.TryParse(_seedInputField.text, out int seed))
        {
            Debug.LogWarning("[LobbyUI] 유효한 숫자 시드를 입력하세요.");
            return;
        }
        TurnHistoryRepository.Instance.ClearAll();
        NewGameConfig.SetSeed(seed);
        SceneManager.LoadScene(_gameSceneName);
    }

    private void OnStartRandomClicked()
    {
        TurnHistoryRepository.Instance.ClearAll();
        NewGameConfig.SetRandom();
        SceneManager.LoadScene(_gameSceneName);
    }

    // ── Private ──────────────────────────────────────────────────────────────

    public void ShowMain()
    {
        if (_mainPanel != null)    _mainPanel.SetActive(true);
        if (_newGamePanel != null) _newGamePanel.SetActive(false);
    }


}
