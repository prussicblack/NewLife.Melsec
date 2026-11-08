namespace NewLife.Melsec.Protocols;

/// <summary>SLMP 프로토콜 3C 프레임 응답 메시지</summary>
/// <remarks>
/// SLMP 3C 프레임 응답 형식은 MC 3E 프레임과 완전히 호환된다.
/// 서브헤드 D000h, 종료 코드 0000h=성공.
/// 이 클래스는 <see cref="MCResponse"/>의 SLMP 의미론적 래핑이다.
/// </remarks>
public class SLMPResponse
{
    private readonly MCResponse _inner;

    /// <summary>내부 MCResponse 인스턴스</summary>
    public MCResponse Inner => _inner;

    /// <summary>SLMP 응답 인스턴스화</summary>
    public SLMPResponse() => _inner = new MCResponse();

    /// <summary>기존 MCResponse 인스턴스로 래핑</summary>
    public SLMPResponse(MCResponse response) => _inner = response;

    /// <summary>종료 코드. 0x0000 = 성공; 0이 아닌 값 = 오류</summary>
    public UInt16 EndCode => _inner.EndCode;

    /// <summary>응답 원시 데이터</summary>
    public Byte[] RawData { get => _inner.RawData; set => _inner.RawData = value; }

    /// <summary>응답 데이터를 워드 소프트 요소 배열로 해독 (워드당 2바이트 LE)</summary>
    public UInt16[] GetWordData() => _inner.GetWordData();

    /// <summary>응답 데이터를 비트 소프트 요소 배열로 해독</summary>
    public Boolean[] GetBitData(Int32 count) => _inner.GetBitData(count);

    /// <summary>바이트 배열에서 응답 프레임 해독</summary>
    public Boolean Read(Byte[] data)
    {
        using var ms = new MemoryStream(data);
        return _inner.Read(ms, null);
    }

    /// <summary>바이트 배열로 직렬화 (테스트용 시뮬레이션)</summary>
    public Byte[] ToBytes() => _inner.ToBytes();

    /// <summary>친화적인 문자열</summary>
    public override String ToString() => $"SLMP {_inner}";

    #region 팩토리 메서드 (테스트 헬퍼)

    /// <summary>성공한 워드 읽기 응답 생성 (테스트용)</summary>
    public static SLMPResponse BuildWordResponse(UInt16[] values) =>
        new(MCResponse.BuildWordResponse(values));

    /// <summary>성공한 비트 읽기 응답 생성 (테스트용)</summary>
    public static SLMPResponse BuildBitResponse(Boolean[] values) =>
        new(MCResponse.BuildBitResponse(values));

    /// <summary>성공한 쓰기 응답 생성 (테스트용)</summary>
    public static SLMPResponse BuildWriteResponse() =>
        new(MCResponse.BuildWriteResponse());

    /// <summary>오류 응답 생성 (테스트용)</summary>
    public static SLMPResponse BuildErrorResponse(UInt16 endCode) =>
        new(MCResponse.BuildErrorResponse(endCode));

    #endregion
}