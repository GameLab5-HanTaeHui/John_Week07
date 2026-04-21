using UnityEngine;

/// <summary>
/// 턴 종료 및 게임 시작 시 상황별 DialogueSO 후보군을 묶는 설정 에셋입니다.
/// 각 상황마다 여러 개의 SO를 등록해두면 그 중 하나가 랜덤으로 선택됩니다.
///
/// 선택 우선순위 (턴 종료): 루프 리셋 > 사망 발생 > 평범
/// </summary>
[CreateAssetMenu(fileName = "TurnEndDialogueConfig", menuName = "Dialogue/Turn End Config")]
public class TurnEndDialogueConfig : ScriptableObject
{
    [Header("게임 최초 시작 시 (첫 루프 첫 턴)")]
    public DialogueSO[] gameStartDialogues;

    [Header("아무 사건 없을 때")]
    public DialogueSO[] normalDialogues;

    [Header("이번 루프에 사망 사건이 있을 때")]
    public DialogueSO[] deathDialogues;

    [Header("루프 강제 종료 조건 달성 시")]
    public DialogueSO[] loopResetDialogues;

    [Header("최종 결정 — 승리 시")]
    public DialogueSO[] winDialogues;

    [Header("최종 결정 — 패배 시")]
    public DialogueSO[] loseDialogues;

    public DialogueSO SelectGameStart()              => PickRandom(gameStartDialogues);
    public DialogueSO SelectWin()                    => PickRandom(winDialogues);
    public DialogueSO SelectLose()                   => PickRandom(loseDialogues);
    public DialogueSO Select(bool isLoopReset, bool hasDeath)
    {
        if (isLoopReset) return PickRandom(loopResetDialogues) ?? PickRandom(normalDialogues);
        if (hasDeath)    return PickRandom(deathDialogues)     ?? PickRandom(normalDialogues);
        return PickRandom(normalDialogues);
    }

    private static DialogueSO PickRandom(DialogueSO[] pool)
    {
        if (pool == null || pool.Length == 0) return null;
        return pool[Random.Range(0, pool.Length)];
    }
}
