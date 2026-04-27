
/// <summary>
/// 로비에서 게임 씬으로 새 게임 설정을 전달하는 정적 컨테이너입니다.
/// LobbyUI에서 Set* 호출 → GameSetupState에서 소비 후 Clear().
/// </summary>
public static class NewGameConfig
{
    public static bool   IsSet      { get; private set; }
    public static bool   UseRandom  { get; private set; }
    public static int    Seed       { get; private set; }
    public static bool   IsTutorial { get; private set; }
    public static string StageId    { get; private set; }

    // [캠패인모드]
    public static string PendingPhase2StageId { get; set; }

    // ★ 추가: 기본 모드를 스킵하고 처음부터 캠페인 모드로 시작
    public static bool ForceStartAsPhase2 { get; set; }

    public static void SetRandom(string stageId = null) { IsSet = true; UseRandom = true; StageId = stageId; }
    public static void SetSeed(int seed, string stageId = null) { IsSet = true; UseRandom = false; Seed = seed; StageId = stageId; }
    public static void SetTutorial(int fixedSeed)
    {
        IsSet      = true;
        UseRandom  = false;
        Seed       = fixedSeed;
        IsTutorial = true;
    }
    public static void Clear()
    {
        IsSet = false;
        UseRandom = false;
        Seed = 0;
        IsTutorial = false;
        StageId = null;
        PendingPhase2StageId = null;

        ForceStartAsPhase2 = false; // ★ 추가
    }
}
