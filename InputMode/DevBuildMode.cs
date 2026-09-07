/// <summary>
/// DevTeleport로 진입한 세션임을 나타내는 런타임 플래그.
/// 빌드에서도 개발자 편의 기능(대화 스킵, 타임라인 스킵)을 활성화한다.
/// </summary>
public static class DevBuildMode
{
    public static bool IsActive { get; private set; }

    public static void Activate() => IsActive = true;
    public static void Deactivate() => IsActive = false;
}
