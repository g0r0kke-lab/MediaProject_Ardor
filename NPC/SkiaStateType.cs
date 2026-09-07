/// <summary>
/// 스키아 FSM 상태 열거형
/// 문서의 ST_01 ~ ST_09에 대응
/// </summary>
public enum SkiaStateType
{
    IdlePatrol,       // ST_01: 대기(걷기) - 정해진 경로 순찰
    AlertWarn,        // ST_02: 경계 - 시야 내 플레이어 감지, 탐지 게이지 충전
    RoarStart,        // ST_03: 포효(1회) - 게이지 100% 도달, 포효 애니메이션
    Investigate,      // ST_04: 대기(걷기) - 소음 감지 시 LKP로 이동
    ChaseActive,      // ST_05: 추격(달리기) - 플레이어 위치로 전력 질주
    StopCheck,        // ST_06: 정지 - 소음 원점 도착 후 두리번거림
    StopLost,         // ST_07: 정지 - 추격 중 시야 이탈 7초 경과
    RoarFail,         // ST_08: 포효(1회) - 도달 불가 판정, 화내는 연출
    Return,            // ST_09: 대기(걷기) - Home 위치로 복귀
    // Storage 전용
    StorageLure,      // ST_S01: 던진 물체를 따라가는 상태
    StorageTrigger,   // ST_S02: 타임라인 재생 준비 (회전 후 ExecuteTrigger)
    DeactivateChase,
    ExecutionChase    // ST_E01: ExecutionSkia 전용 추격 (NavMeshAgent)
}
