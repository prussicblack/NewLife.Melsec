namespace NewLife.Melsec.Protocols;

/// <summary>MC 프로토콜 프레임 유형</summary>
/// <remarks>
/// Mitsubishi MC 프로토콜은 여러 프레임 형식을 지원한다:
/// <see cref="Frame3E"/> (Qna 호환 3E 프레임, Q/L/iQ-R/FX5U 시리즈),
/// <see cref="Frame1E"/> (A 호환 1E 프레임, A 시리즈 구형 PLC) 및
/// <see cref="Frame4E"/> (확장 4E 프레임, iQ-R 시리즈, 시퀀스 번호 필드 포함).
/// </remarks>
public enum MCFrameType
{
    /// <summary>3E 프레임 (Qna 호환). 네트워크 번호/PC 번호/IO 유닛 번호 등 필드 포함, 서브헤드 5000h</summary>
    Frame3E = 0,

    /// <summary>1E 프레임 (A 호환). 네트워크 번호 등 필드 없음, 구조가 더 단순하고 서브헤드가 1바이트 명령</summary>
    Frame1E = 1,

    /// <summary>4E 프레임 (iQ-R 시리즈 확장). 서브헤드 5400h/D400h, 서브헤드 뒤에 4바이트 시퀀스 번호 필드 추가</summary>
    /// <remarks>
    /// 4E 프레임은 표준 3E 프레임의 서브헤드 뒤에 4바이트 시퀀스 번호 필드 (2바이트 시퀀스 번호 + 2바이트 예약)를
    /// 삽입하여 요청-응답의 엄밀한 매칭 검증을 수행한다. 이후 필드 레이아웃은 3E 프레임과 완전히 동일하다.
    /// 요청 서브헤드: 0x5400 (바이너리) / "5400" (ASCII)
    /// 응답 서브헤드: 0xD400 (바이너리) / "D400" (ASCII)
    /// </remarks>
    Frame4E = 2,
}

/// <summary>MCFrameType 확장 메서드</summary>
internal static class MCFrameTypeHelper
{
    /// <summary>1E 프레임인지 판단</summary>
    public static Boolean Is1E(this MCFrameType frameType) => frameType == MCFrameType.Frame1E;

    /// <summary>4E 프레임인지 판단</summary>
    public static Boolean Is4E(this MCFrameType frameType) => frameType == MCFrameType.Frame4E;

    /// <summary>3E 또는 4E 프레임인지 판단 (동일한 이후 필드 레이아웃 공유)</summary>
    public static Boolean Is3EOr4E(this MCFrameType frameType) => frameType == MCFrameType.Frame3E || frameType == MCFrameType.Frame4E;
}