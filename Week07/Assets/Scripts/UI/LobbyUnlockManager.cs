using System;
using UnityEngine;

/// <summary>
/// 로비에서 스테이지 클리어 기록을 확인해 버튼 오브젝트의 활성화 여부를 관리합니다.
///
/// Inspector 설정:
///   Entries 배열에 (ButtonObject, RequiredClearStageId) 쌍을 등록하세요.
///   RequiredClearStageId가 비어있으면 항상 활성화됩니다.
/// </summary>
public class LobbyUnlockManager : MonoBehaviour
{
    [Serializable]
    public struct UnlockEntry
    {
        [Tooltip("활성화/비활성화할 버튼 오브젝트")]
        public GameObject buttonObject;
        [Tooltip("클리어되어야 열리는 스테이지 ID. 비워두면 항상 활성화.")]
        public string requiredClearStageId;
    }

    [SerializeField] private UnlockEntry[] _entries;

    private void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        foreach (var entry in _entries)
        {
            if (entry.buttonObject == null) continue;

            bool unlocked = string.IsNullOrEmpty(entry.requiredClearStageId)
                         || StageClearRepository.Instance.HasCleared(entry.requiredClearStageId);

            entry.buttonObject.SetActive(unlocked);
        }
    }
}
