namespace NewLife.Melsec.Protocols;

/// <summary>SLMP 프로토콜 3C 프레임 요청 메시지</summary>
/// <remarks>
/// SLMP(Seamless Message Protocol) 3C 프레임 형식은 MC 3E 프레임 바이너리 모드와 완전히 호환된다.
/// 서브헤드 5000h, Little-Endian 바이트 순서.
///
/// 이 클래스는 <see cref="MCMessage"/>의 SLMP 의미론적 래핑이며, 내부적으로 직렬화 로직을 재사용한다.
/// SLMP 표준을 지원하는 iQ-R/iQ-F 시리즈 등 미쓰비시 장비에 적합.
/// </remarks>
public class SLMPMessage
{
    private readonly MCMessage _inner;

    /// <summary>내부 MCMessage 인스턴스</summary>
    public MCMessage Inner => _inner;

    /// <summary>SLMP 요청 메시지 인스턴스화</summary>
    public SLMPMessage() => _inner = new MCMessage { DataFormat = MCDataFormat.Binary };

    /// <summary>기존 MCMessage 인스턴스로 래핑</summary>
    /// <param name="message">MCMessage 인스턴스</param>
    public SLMPMessage(MCMessage message) => _inner = message;

    /// <summary>네트워크 번호. 보통 0x00</summary>
    public Byte NetworkNo { get => _inner.NetworkNo; set => _inner.NetworkNo = value; }

    /// <summary>PC 번호. 보통 0xFF</summary>
    public Byte PCNo { get => _inner.PCNo; set => _inner.PCNo = value; }

    /// <summary>명령 코드</summary>
    public UInt16 Command { get => _inner.Command; set => _inner.Command = value; }

    /// <summary>서브 명령</summary>
    public UInt16 SubCommand { get => _inner.SubCommand; set => _inner.SubCommand = value; }

    /// <summary>시작 주소</summary>
    public Int32 StartAddress { get => _inner.StartAddress; set => _inner.StartAddress = value; }

    /// <summary>소프트 요소 코드</summary>
    public SLMPDeviceCode DeviceCode { get => (SLMPDeviceCode)_inner.DeviceCode; set => _inner.DeviceCode = (DeviceCode)value; }

    /// <summary>소프트 요소 포인트 수</summary>
    public UInt16 Count { get => _inner.Count; set => _inner.Count = value; }

    /// <summary>쓰기 데이터</summary>
    public UInt16[] WriteData { get => _inner.WriteData; set => _inner.WriteData = value; }

    /// <summary>바이트 배열로 직렬화</summary>
    public Byte[] ToBytes() => _inner.ToBytes();

    /// <summary>바이트 배열에서 역직렬화</summary>
    public Boolean Read(Byte[] data)
    {
        using var ms = new MemoryStream(data);
        return _inner.Read(ms, null);
    }

    /// <summary>해당 응답 객체 생성</summary>
    public SLMPResponse CreateReply() => new(_inner.CreateReply());

    /// <summary>친화적인 문자열</summary>
    public override String ToString() => $"SLMP {_inner}";

    #region 팩토리 메서드

    /// <summary>워드 소프트 요소 벌크 읽기 요청 구성</summary>
    public static SLMPMessage BuildReadWord(SLMPDeviceCode devCode, Int32 startAddr, Int32 count)
    {
        var inner = MCMessage.BuildReadWord((DeviceCode)devCode, startAddr, count);
        return new SLMPMessage(inner);
    }

    /// <summary>비트 소프트 요소 벌크 읽기 요청 구성</summary>
    public static SLMPMessage BuildReadBit(SLMPDeviceCode devCode, Int32 startAddr, Int32 count)
    {
        var inner = MCMessage.BuildReadBit((DeviceCode)devCode, startAddr, count);
        return new SLMPMessage(inner);
    }

    /// <summary>워드 소프트 요소 벌크 쓰기 요청 구성</summary>
    public static SLMPMessage BuildWriteWord(SLMPDeviceCode devCode, Int32 startAddr, UInt16[] values)
    {
        var inner = MCMessage.BuildWriteWord((DeviceCode)devCode, startAddr, values);
        return new SLMPMessage(inner);
    }

    /// <summary>비트 소프트 요소 벌크 쓰기 요청 구성</summary>
    public static SLMPMessage BuildWriteBit(SLMPDeviceCode devCode, Int32 startAddr, UInt16[] values)
    {
        var inner = MCMessage.BuildWriteBit((DeviceCode)devCode, startAddr, values);
        return new SLMPMessage(inner);
    }

    #endregion
}