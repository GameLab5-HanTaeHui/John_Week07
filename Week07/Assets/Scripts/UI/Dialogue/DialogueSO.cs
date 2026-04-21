using UnityEngine;

/// <summary>
/// 다이어로그 라인 목록을 담는 ScriptableObject입니다.
/// 상황별(사망 발생, 루프 리셋 등) DialogueSO를 각각 제작해 TurnEndDialogueConfig에 연결하세요.
/// </summary>
[CreateAssetMenu(fileName = "New Dialogue", menuName = "Dialogue/Dialogue Lines")]
public class DialogueSO : ScriptableObject
{
    [TextArea(2, 5)]
    public string[] lines;
}
