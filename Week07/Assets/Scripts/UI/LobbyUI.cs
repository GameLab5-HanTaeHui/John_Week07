using HTH.Campaign;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 로비 화면의 기본 진입 흐름을 관리합니다.
///
/// ─── 책임 ────────────────────────────────────────────────────────────
///   튜토리얼 미진행 감지 → 튜토리얼 씬 자동 이동
///   MainPanel ↔ NewGamePanel 전환
///   씬 이름 / 스테이지 ID / 시드는 LobbyConfig SO에서만 읽습니다.
///
/// ─── Inspector 연결 ──────────────────────────────────────────────────
///   Config            → LobbyConfig 에셋 (필수)
///   Main Panel        → 메인 패널 GameObject
///   New Game Panel    → 새로하기 패널 GameObject
///   Continue Button   → 이어하기 버튼 (기본모드)
///   New Game Button   → 새로하기 버튼
///   Tutorial Retry Button → 튜토리얼 다시하기 버튼
///   Seed Input Field  → 시드 입력 필드
///   Start Seed Button → 시드로 시작 버튼
///   Start Random Button → 랜덤 시드 시작 버튼
///   Back Button       → 새로하기 패널 닫기 버튼
/// </summary>
public class LobbyUI : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("씬 이름 / 스테이지 ID / 시드 설정 에셋입니다.")]
    [SerializeField] private HTH.Campaign.LobbyConfig _config;

    [Header("메인 패널")]
    [SerializeField] private GameObject _mainPanel;
    [SerializeField] private Button _continueButton;
    [SerializeField] private Button _newGameButton;
    [SerializeField] private Button _tutorialRetryButton;

    [Header("새로하기 패널")]
    [SerializeField] private GameObject _newGamePanel;
    [SerializeField] private TMP_InputField _seedInputField;
    [SerializeField] private Button _startSeedButton;
    [SerializeField] private Button _startRandomButton;
    [SerializeField] private Button _backButton;

    // ── Unity ────────────────────────────────────────────────────────────

    private void Start()
    {
        if (_config == null)
        {
            Debug.LogError("[LobbyUI] LobbyConfig가 연결되지 않았습니다.");
            return;
        }

        TutorialProgressRepository.Instance.TryLoad();

        if (!TutorialProgressRepository.Instance.IsStarted)
        {
            CampaignLobbyNavigator.Instance?.StartTutorial();
            return;
        }

        SetupButtons();
    }

    // ── 버튼 설정 ─────────────────────────────────────────────────────────

    private void SetupButtons()
    {
        if (_continueButton != null)
            _continueButton.interactable = false;

        if (_tutorialRetryButton != null)
            _tutorialRetryButton.gameObject.SetActive(
                TutorialProgressRepository.Instance.IsStarted);

        _continueButton?.onClick.AddListener(OnContinueClicked);
        _newGameButton?.onClick.AddListener(OnNewGameClicked);
        _tutorialRetryButton?.onClick.AddListener(OnTutorialRetryClicked);
        _startSeedButton?.onClick.AddListener(OnStartWithSeedClicked);
        _startRandomButton?.onClick.AddListener(OnStartRandomClicked);
        _backButton?.onClick.AddListener(ShowMain);
    }

    // ── 버튼 콜백 ─────────────────────────────────────────────────────────

    private void OnContinueClicked()
    {
        NewGameConfig.Clear();
        SceneManager.LoadScene(_config.DefaultGameSceneName);
    }

    private void OnNewGameClicked()
    {
        _mainPanel?.SetActive(false);
        _newGamePanel?.SetActive(true);
        if (_seedInputField != null) _seedInputField.text = "";
    }

    private void OnTutorialRetryClicked()
        => CampaignLobbyNavigator.Instance?.RetryTutorial();

    private void OnStartWithSeedClicked()
    {
        if (_seedInputField == null || !int.TryParse(_seedInputField.text, out int seed))
        {
            Debug.LogWarning("[LobbyUI] 유효한 숫자 시드를 입력하세요.");
            return;
        }
        CampaignLobbyNavigator.Instance?.StartWithSeed(seed);
    }

    private void OnStartRandomClicked()
        => CampaignLobbyNavigator.Instance?.StartRandom();

    public void ShowMain()
    {
        _mainPanel?.SetActive(true);
        _newGamePanel?.SetActive(false);
    }
}