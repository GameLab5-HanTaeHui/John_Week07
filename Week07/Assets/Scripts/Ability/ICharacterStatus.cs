/// <summary>
/// 직업 능력 처리 시 캐릭터 정보를 읽기 전용으로 제공하는 인터페이스입니다.
/// 캐릭터 시스템 구현 시 이 인터페이스를 구현하세요.
/// </summary>
public interface ICharacterStatus
{
    int    CharacterId   { get; }
    string CharacterName { get; }
    bool   IsAlive       { get; }
}
