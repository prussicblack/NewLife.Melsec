namespace NewLife.Melsec.Protocols;

/// <summary>MC 프로토콜 종료 코드 (EndCode)</summary>
/// <remarks>종료 코드 0x0000은 성공을 의미. 나머지는 모두 에러 코드. 일반적인 에러 코드는 다음과 같이 정의된다.</remarks>
public enum MCEndCode : UInt16
{
    /// <summary>정상 종료</summary>
    Success = 0x0000,

    /// <summary>PLC가 실행 중이라 수행할 수 없음 (요청이 RUN 모드에서 허용되지 않음)</summary>
    PlcRunning = 0xC050,

    /// <summary>소프트 요소 주소 범위 초과</summary>
    AddressOutOfRange = 0xC056,

    /// <summary>명령 또는 서브명령 에러</summary>
    CommandError = 0xC059,

    /// <summary>포인트 수 범위 초과</summary>
    PointCountError = 0xC05B,

    /// <summary>요청 데이터 길이가 실제와 불일치</summary>
    DataLengthError = 0xC060,

    /// <summary>CPU가 다른 요청을 처리 중</summary>
    CpuBusy = 0xC0B5,

    /// <summary>통신 타임아웃 (라우팅 타임아웃)</summary>
    Timeout = 0xC100,
}

/// <summary>MC 프로토콜 예외</summary>
public class MCException : Exception
{
    /// <summary>종료 코드</summary>
    public UInt16 EndCode { get; set; }

    /// <summary>MC 프로토콜 예외 인스턴스 생성</summary>
    /// <param name="endCode">종료 코드</param>
    public MCException(UInt16 endCode)
        : base(GetMessage(endCode)) => EndCode = endCode;

    /// <summary>MC 프로토콜 예외 인스턴스 생성</summary>
    /// <param name="endCode">종료 코드</param>
    /// <param name="message">추가 설명</param>
    public MCException(UInt16 endCode, String message)
        : base(message) => EndCode = endCode;

    private static String GetMessage(UInt16 code)
    {
        var known = (MCEndCode)code;
        return known switch
        {
            MCEndCode.Success => "OK",
            MCEndCode.PlcRunning => $"0x{code:X4} PLC is running, cannot execute request",
            MCEndCode.AddressOutOfRange => $"0x{code:X4} Device address out of range",
            MCEndCode.CommandError => $"0x{code:X4} Command or sub-command error",
            MCEndCode.PointCountError => $"0x{code:X4} Read/write point count out of range",
            MCEndCode.DataLengthError => $"0x{code:X4} Request data length mismatch",
            MCEndCode.CpuBusy => $"0x{code:X4} CPU busy, please retry later",
            MCEndCode.Timeout => $"0x{code:X4} Communication timeout",
            _ => $"0x{code:X4} MC protocol error",
        };
    }
}