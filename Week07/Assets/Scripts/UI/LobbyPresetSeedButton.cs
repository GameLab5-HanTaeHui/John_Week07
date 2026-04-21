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

    [Header("클리어 취소선")]
    [SerializeField] private TMP_Text _label;

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnClicked);
        RefreshLabel();
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
        NewGameConfig.SetSeed(_seed, _stageId);
        UnityEngine.SceneManagement.SceneManager.LoadScene(_gameSceneName);
    }

    // Inspector에서 텍스트를 직접 수정했을 때 <s> 태그가 이중 적용되지 않도록 제거합니다.
    private static string StripStrikethrough(string text)
        => text.Replace("<s>", "").Replace("</s>", "");
}
