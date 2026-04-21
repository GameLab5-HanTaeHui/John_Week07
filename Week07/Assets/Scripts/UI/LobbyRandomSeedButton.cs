using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼에 붙이면 클릭 시 무작위 시드로 게임을 시작합니다.
/// 해금 조건 관리는 LobbyUnlockManager가 담당합니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class LobbyRandomSeedButton : MonoBehaviour
{
    [SerializeField] private string _stageId;
    [SerializeField] private string _gameSceneName = "Stage_1";

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnClicked);
    }

    private void OnClicked()
    {
        TurnHistoryRepository.Instance.ClearAll();
        NewGameConfig.SetRandom(_stageId);
        UnityEngine.SceneManagement.SceneManager.LoadScene(_gameSceneName);
    }
}
