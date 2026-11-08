using NewLife.Serialization;

namespace NewLife.Melsec.Protocols;

/// <summary>MC 프로토콜 3E 프레임 응답 메시지 (바이너리/ASCII 모드)</summary>
/// <remarks>
/// 프레임 형식 (바이너리 Little-Endian):
/// [0~1]  서브헤드    2    고정 0xD0 0x00
/// [2]    네트워크 번호 1
/// [3]    PC 번호     1
/// [4~5]  IO 유니트 번호 2
/// [6]    채널 번호   1
/// [7~8]  데이터 길이 2    LE, 바이트[9]부터의 바이트 수
/// [9~10] 종료 코드   2    LE, 0x0000=성공, 0이 아닌 값=오류
/// [11~]  응답 데이터 N    워드 읽기: 워드당 2바이트 LE; 비트 읽기: 바이트당 2포인트 (저4비트=1번째, 고4비트=2번째)
///
/// ASCII 모드에서는 각 바이트를 2개의 ASCII 16진 문자로 표현하며,
/// 바이트 순서는 바이너리 모드와 동일하다.
/// </remarks>
public class MCResponse : IAccessor
{
    #region 상수

    /// <summary>3E 프레임 서브헤드 (응답, 바이너리)</summary>
    public const UInt16 SUB_HEADER = 0x00D0;

    /// <summary>4E 프레임 서브헤드 (응답, 바이너리). 4E 프레임은 서브헤드 뒤에 4바이트 시리얼 번호 필드를 추가</summary>
    public const UInt16 SUB_HEADER_4E = 0x00D4;

    /// <summary>3E 프레임 서브헤드 (응답, ASCII)</summary>
    public const String SUB_HEADER_ASCII = "D000";

    /// <summary>4E 프레임 서브헤드 (응답, ASCII)</summary>
    public const String SUB_HEADER_4E_ASCII = "D400";

    /// <summary>3E 응답 프레임 고정 헤더 길이 (서브헤드+네트워크번호+PC번호+IO유니트번호+채널번호+데이터길이 = 9바이트)</summary>
    public const Int32 FIXED_HEADER_LEN = 9;

    /// <summary>4E 응답 프레임 고정 헤더 길이 (서브헤드+시리얼번호+네트워크번호+PC번호+IO유니트번호+채널번호+데이터길이 = 13바이트)</summary>
    public const Int32 FIXED_HEADER_4E_LEN = 13;

    /// <summary>ASCII 3E 응답 프레임 고정 헤더 길이 (바이너리 9바이트 → 18 ASCII 문자)</summary>
    public const Int32 FIXED_HEADER_ASCII_LEN = 18;

    /// <summary>ASCII 4E 응답 프레임 고정 헤더 길이 (바이너리 13바이트 → 26 ASCII 문자)</summary>
    public const Int32 FIXED_HEADER_4E_ASCII_LEN = 26;

    #endregion

    #region 속성

    /// <summary>데이터 형식. 기본값 Binary</summary>
    public MCDataFormat DataFormat { get; set; } = MCDataFormat.Binary;

    /// <summary>프레임 타입. 서브헤드에서 자동 감지 (3E/4E)</summary>
    public MCFrameType FrameType { get; set; } = MCFrameType.Frame3E;

    /// <summary>시리얼 번호 (4E 프레임 전용). 요청-응답 매칭 검증에 사용</summary>
    public UInt16 SerialNumber { get; set; }

    /// <summary>네트워크 번호</summary>
    public Byte NetworkNo { get; set; }

    /// <summary>PC 번호</summary>
    public Byte PCNo { get; set; }

    /// <summary>IO 유니트 번호</summary>
    public UInt16 IOUnitNo { get; set; }

    /// <summary>채널 번호</summary>
    public Byte ChannelNo { get; set; }

    /// <summary>종료 코드. 0x0000 = 성공; 0이 아닌 값 = 오류</summary>
    public UInt16 EndCode { get; set; }

    /// <summary>응답 원시 데이터 (종료 코드 이후의 바이트)</summary>
    public Byte[] RawData { get; set; }

    #endregion

    #region 생성

    /// <summary>오버라이드. 친화적인 문자열</summary>
    public override String ToString()
    {
        if (EndCode != 0) return $"MC Response ERROR 0x{EndCode:X4}";
        return $"MC Response OK, DataLen={RawData?.Length ?? 0}";
    }

    #endregion

    #region 직렬화

    /// <summary>응답 프레임 읽기 (역직렬화)</summary>
    /// <param name="stream">데이터 스트림</param>
    /// <param name="context">컨텍스트 (무시)</param>
    /// <returns>성공 여부</returns>
    public virtual Boolean Read(Stream stream, Object context)
    {
        if (DataFormat.IsAscii())
            return ReadAscii(stream);

        // 서브헤드 0xD0 0x00 (3E) 또는 0xD4 0x00 (4E)
        var sh0 = stream.ReadByte();
        var sh1 = stream.ReadByte();

        if (sh0 == 0xD4 && sh1 == 0x00)
        {
            // 4E 프레임: 시리얼 번호 읽기 (4바이트: 2바이트 시리얼 + 2바이트 예약)
            FrameType = MCFrameType.Frame4E;
            SerialNumber = MCMessage.ReadUInt16LE(stream);
            stream.ReadByte(); // 예약 바이트
            stream.ReadByte(); // 예약 바이트
        }
        else if (sh0 == 0xD0 && sh1 == 0x00)
        {
            FrameType = MCFrameType.Frame3E;
        }
        else
        {
            return false;
        }

        NetworkNo = (Byte)stream.ReadByte();
        PCNo = (Byte)stream.ReadByte();
        IOUnitNo = MCMessage.ReadUInt16LE(stream);
        ChannelNo = (Byte)stream.ReadByte();

        var dataLength = MCMessage.ReadUInt16LE(stream);  // EndCode(2) + ResponseData(N)
        EndCode = MCMessage.ReadUInt16LE(stream);

        // 응답 데이터 = dataLength - EndCode(2)
        var dataLen = dataLength - 2;
        if (dataLen > 0)
        {
            RawData = new Byte[dataLen];
            stream.Read(RawData, 0, dataLen);
        }

        return true;
    }

    /// <summary>ASCII 모드 역직렬화</summary>
    private Boolean ReadAscii(Stream stream)
    {
        // 서브헤드 "D000" (3E) 또는 "D400" (4E), 4 ASCII 문자 → 2바이트
        var sh = MCMessage.ReadAsciiHex(stream, 2);

        if (sh[0] == 0xD4 && sh[1] == 0x00)
        {
            // 4E 프레임: 시리얼 번호 읽기 (4바이트: 2바이트 시리얼 + 2바이트 예약)
            FrameType = MCFrameType.Frame4E;
            var serialBytes = MCMessage.ReadAsciiHex(stream, 4);
            SerialNumber = (UInt16)(serialBytes[0] | (serialBytes[1] << 8));
        }
        else if (sh[0] == 0xD0 && sh[1] == 0x00)
        {
            FrameType = MCFrameType.Frame3E;
        }
        else
        {
            return false;
        }

        NetworkNo = MCMessage.ReadAsciiByte(stream);
        PCNo = MCMessage.ReadAsciiByte(stream);
        IOUnitNo = MCMessage.ReadAsciiUInt16(stream);
        ChannelNo = MCMessage.ReadAsciiByte(stream);

        var dataLength = MCMessage.ReadAsciiUInt16(stream);  // EndCode(2) + ResponseData(N)
        EndCode = MCMessage.ReadAsciiUInt16(stream);

        // 응답 데이터 = dataLength - EndCode(2)
        var dataLen = dataLength - 2;
        if (dataLen > 0)
        {
            RawData = MCMessage.ReadAsciiHex(stream, dataLen);
        }

        return true;
    }

    /// <summary>응답 프레임 쓰기 (직렬화, 주로 테스트 시뮬레이션용)</summary>
    /// <param name="stream">데이터 스트림</param>
    /// <param name="context">컨텍스트 (무시)</param>
    /// <returns>성공 여부</returns>
    public virtual Boolean Write(Stream stream, Object context)
    {
        if (DataFormat.IsAscii())
            return WriteAscii(stream);

        if (FrameType.Is4E())
        {
            // 4E 프레임 서브헤드 0xD4 0x00 + 시리얼 번호 4바이트
            stream.WriteByte(0xD4);
            stream.WriteByte(0x00);
            MCMessage.WriteUInt16LE(stream, SerialNumber);
            stream.WriteByte(0x00); // 예약
            stream.WriteByte(0x00); // 예약
        }
        else
        {
            // 3E 프레임 서브헤드 0xD0 0x00
            stream.WriteByte(0xD0);
            stream.WriteByte(0x00);
        }

        stream.WriteByte(NetworkNo);
        stream.WriteByte(PCNo);
        MCMessage.WriteUInt16LE(stream, IOUnitNo);
        stream.WriteByte(ChannelNo);

        // 데이터 길이 = EndCode(2) + RawData
        var dataLength = (UInt16)(2 + (RawData?.Length ?? 0));
        MCMessage.WriteUInt16LE(stream, dataLength);
        MCMessage.WriteUInt16LE(stream, EndCode);

        if (RawData != null && RawData.Length > 0)
            stream.Write(RawData, 0, RawData.Length);

        return true;
    }

    /// <summary>ASCII 모드 직렬화</summary>
    private Boolean WriteAscii(Stream stream)
    {
        using var ms = new MemoryStream();

        if (FrameType.Is4E())
        {
            // 4E 프레임 서브헤드 0xD4 0x00 + 시리얼 번호 4바이트
            ms.WriteByte(0xD4);
            ms.WriteByte(0x00);
            MCMessage.WriteUInt16LE(ms, SerialNumber);
            ms.WriteByte(0x00); // 예약
            ms.WriteByte(0x00); // 예약
        }
        else
        {
            ms.WriteByte(0xD0);
            ms.WriteByte(0x00);
        }

        ms.WriteByte(NetworkNo);
        ms.WriteByte(PCNo);
        MCMessage.WriteUInt16LE(ms, IOUnitNo);
        ms.WriteByte(ChannelNo);

        var dataLength = (UInt16)(2 + (RawData?.Length ?? 0));
        MCMessage.WriteUInt16LE(ms, dataLength);
        MCMessage.WriteUInt16LE(ms, EndCode);

        if (RawData != null && RawData.Length > 0)
            ms.Write(RawData, 0, RawData.Length);

        // 바이너리 바이트 → ASCII 16진 문자열 → 출력 스트림에 쓰기
        var binary = ms.ToArray();
        var hex = binary.ToHex();
        var hexBytes = System.Text.Encoding.ASCII.GetBytes(hex);
        stream.Write(hexBytes, 0, hexBytes.Length);

        return true;
    }

    /// <summary>바이트 배열로 직렬화 (테스트용 서버 응답 데이터 시뮬레이션)</summary>
    public Byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        Write(ms, null);
        return ms.ToArray();
    }

    /// <summary>바이트 배열에서 응답 프레임 해독</summary>
    /// <param name="data">프레임 데이터 (바이너리 또는 ASCII 모드)</param>
    /// <param name="format">데이터 형식</param>
    public static MCResponse FromBytes(Byte[] data, MCDataFormat format = MCDataFormat.Binary)
    {
        using var ms = new MemoryStream(data);
        var response = new MCResponse { DataFormat = format };
        response.Read(ms, null);
        return response;
    }

    #endregion

    #region 데이터 해독

    /// <summary>응답 데이터를 워드 소프트 요소 배열로 해독 (워드당 2바이트 LE)</summary>
    public UInt16[] GetWordData()
    {
        if (RawData == null || RawData.Length == 0) return [];
        var words = new UInt16[RawData.Length / 2];
        for (var i = 0; i < words.Length; i++)
            words[i] = (UInt16)(RawData[i * 2] | (RawData[i * 2 + 1] << 8));
        return words;
    }

    /// <summary>응답 데이터를 비트 소프트 요소 배열로 해독 (바이트당 2포인트, 저 4비트 = 1번째, 고 4비트 = 2번째)</summary>
    /// <param name="count">기대하는 포인트 수</param>
    public Boolean[] GetBitData(Int32 count)
    {
        if (RawData == null || RawData.Length == 0) return [];
        var bits = new Boolean[count];
        for (var i = 0; i < count; i++)
        {
            var byteIdx = i / 2;
            var shift = (i % 2) * 4;
            if (byteIdx < RawData.Length)
                bits[i] = ((RawData[byteIdx] >> shift) & 0x0F) != 0;
        }
        return bits;
    }

    #endregion

    #region 팩토리 메서드 (테스트 헬퍼)

    /// <summary>성공한 워드 읽기 응답 생성 (테스트용)</summary>
    /// <param name="values">워드 데이터</param>
    public static MCResponse BuildWordResponse(UInt16[] values)
    {
        var raw = MCMessage.PackWords(values);
        return new MCResponse { EndCode = 0, RawData = raw };
    }

    /// <summary>성공한 비트 읽기 응답 생성 (테스트용)</summary>
    /// <param name="values">비트 데이터 (true=ON, false=OFF)</param>
    public static MCResponse BuildBitResponse(Boolean[] values)
    {
        var ushorts = Array.ConvertAll(values, v => v ? (UInt16)1 : (UInt16)0);
        var raw = MCMessage.PackBits(ushorts);
        return new MCResponse { EndCode = 0, RawData = raw };
    }

    /// <summary>성공한 쓰기 응답 생성 (테스트용)</summary>
    public static MCResponse BuildWriteResponse() => new() { EndCode = 0 };

    /// <summary>오류 응답 생성 (테스트용)</summary>
    /// <param name="endCode">오류 코드</param>
    public static MCResponse BuildErrorResponse(UInt16 endCode) => new() { EndCode = endCode };

    #endregion
}