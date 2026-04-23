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
        // [HTH추가] stageId가 없는 랜덤 모드는 "Random"으로 고정
        NewGameConfig.SetRandom(!string.IsNullOrEmpty(_stageId) ? _stageId : "Random");
        UnityEngine.SceneManagement.SceneManager.LoadScene(_gameSceneName);
    }
}
