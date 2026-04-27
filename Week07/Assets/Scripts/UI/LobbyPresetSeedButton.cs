using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼에 붙여서 Inspector에서 시드값을 지정합니다.
/// 해금 조건 관리는 LobbyUnlockManager가 담당합니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class LobbyPresetSeedButton : MonoBehaviour
{
    [SerializeField] private int    _seed;
    [SerializeField] private string _stageId;
    [SerializeField] private string _gameSceneName = "Stage_1";

    [Header("스테이지 활성화")]
    [Tooltip("false로 설정하면 버튼이 비활성화됩니다.\n" +
             "기획상 미개방 스테이지를 막을 때 사용합니다.")]
    [SerializeField] private bool _isEnabled = true;

    [Header("클리어 취소선")]
    [SerializeField] private TMP_Text _label;

    // 추가 — 필드
    [Header("캠페인 이어하기")]
    [Tooltip("true로 설정하면 ForceStartAsPhase2로 진입합니다.")]
    [SerializeField] private bool _isCampaignContinue = false;

    // ✅ 수정 — 튜토리얼은 SetTutorial로 진입 보장
    [Header("튜토리얼 설정")]
    [Tooltip("true면 SetTutorial()로 진입합니다. 튜토리얼 재시작 버튼에 사용하세요.")]
    [SerializeField] private bool _isTutorial = false;
    [SerializeField] private int _tutorialFixedSeed = 0;

    private void Start()
    {
        var btn = GetComponent<Button>();
        btn.onClick.AddListener(OnClicked);

        // 비활성화 스테이지는 버튼을 잠급니다.
        btn.interactable = _isEnabled;

        RefreshLabel();

        // ★ 캠페인 이어하기 버튼: 저장 데이터 없으면 숨김
        if (_isCampaignContinue)
        {
            string phase2Id = _stageId + "_Phase2";
            bool hasSave = HTH.Campaign.CampaignSaveManager.Instance != null
                && HTH.Campaign.CampaignSaveManager.Instance.HasSave(phase2Id);
            gameObject.SetActive(hasSave);
        }
    }

    private void RefreshLabel()
    {
        if (_label == null) return;

        bool cleared = StageClearRepository.Instance.HasCleared(_stageId);
        string raw   = cleared
            ? $"<s>{_label.text}</s>"
            : StripStrikethrough(_label.text);
        _label.text = raw;
    }

    private void OnClicked()
    {
        TurnHistoryRepository.Instance.ClearAll();

        if (_isTutorial)
        {
            // ★ 튜토리얼은 고정 시드 + IsTutorial 플래그
            NewGameConfig.SetTutorial(_tutorialFixedSeed);
        }
        else
        {
            NewGameConfig.SetRandom(_stageId);
            NewGameConfig.ForceStartAsPhase2 = _isCampaignContinue;
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene(_gameSceneName);
    }

    // Inspector에서 텍스트를 직접 수정했을 때 <s> 태그가 이중 적용되지 않도록 제거합니다.
    private static string StripStrikethrough(string text)
        => text.Replace("<s>", "").Replace("</s>", "");
}
