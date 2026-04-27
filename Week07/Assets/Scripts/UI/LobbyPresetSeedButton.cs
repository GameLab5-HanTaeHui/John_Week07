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

    [Header("튜토리얼 설정")]
    [SerializeField] private bool _isTutorial = false;
    [SerializeField] private int _tutorialFixedSeed = 0;


    private void OnEnable()
    {
        var btn = GetComponent<Button>();
        btn.onClick.AddListener(OnClicked);

        if (!_isEnabled)
            btn.interactable = false;

        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (_label == null) return;
        bool cleared = StageClearRepository.Instance.HasCleared(_stageId);
        _label.text = cleared
            ? $"<s>{StripStrikethrough(_label.text)}</s>"
            : StripStrikethrough(_label.text);
    }

    private void OnClicked()
    {
        TurnHistoryRepository.Instance.ClearAll();

        if (_isTutorial)
            NewGameConfig.SetTutorial(_tutorialFixedSeed);
        else
        {
            NewGameConfig.SetSeed(0, _stageId);
            NewGameConfig.ForceStartAsPhase2 = false;
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(_gameSceneName);
    }

    // Inspector에서 텍스트를 직접 수정했을 때 <s> 태그가 이중 적용되지 않도록 제거합니다.
    private static string StripStrikethrough(string text)
        => text.Replace("<s>", "").Replace("</s>", "");
}
