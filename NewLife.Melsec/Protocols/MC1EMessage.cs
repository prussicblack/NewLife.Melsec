namespace NewLife.Melsec.Protocols;

/// <summary>MC 프로토콜 1E 프레임 요청 메시지 (A 시리즈 호환)</summary>
/// <remarks>
/// 1E 프레임 형식 (Little-Endian):
/// [0]    서브헤드      1    0x00=워드 읽기, 0x01=비트 읽기, 0x02=워드 쓰기, 0x03=비트 쓰기
/// [1]    PLC 번호      1    보통 0xFF
/// [2~3]  모니터링 타이머 2    LE
/// [4~5]  시작 주소     2    LE (1E 프레임 주소는 16비트)
/// [6]    소프트 요소 코드 1    MC1EDeviceCode enum 참조 (A 시리즈 호환)
/// [7~8]  포인트 수     2    LE
/// [9~]   쓰기 데이터   N    쓰기 명령에만 포함, 3E와 동일한 형식 (워드 쓰기: 워드당 2바이트 LE; 비트 쓰기: 바이트당 2포인트)
/// </remarks>
public class MC1EMessage
{
    #region 상수

    /// <summary>워드 소프트 요소 읽기 서브헤드</summary>
    public const Byte SUB_READ_WORD = 0x00;

    /// <summary>비트 소프트 요소 읽기 서브헤드</summary>
    public const Byte SUB_READ_BIT = 0x01;

    /// <summary>워드 소프트 요소 쓰기 서브헤드</summary>
    public const Byte SUB_WRITE_WORD = 0x02;

    /// <summary>비트 소프트 요소 쓰기 서브헤드</summary>
    public const Byte SUB_WRITE_BIT = 0x03;

    /// <summary>PLC 번호 기본값</summary>
    public const Byte DEFAULT_PC_NO = 0xFF;

    /// <summary>1E 프레임 요청 고정 헤더 길이 (서브헤드 + PLC 번호 + 모니터링 타이머 + 시작 주소 + 소프트 요소 코드 + 포인트 수 = 9바이트)</summary>
    public const Int32 FIXED_HEADER_LEN = 9;

    #endregion

    #region 속성

    /// <summary>서브헤드. 읽기/쓰기 작업 및 워드/비트 모드 표시</summary>
    public Byte SubHeader { get; set; }

    /// <summary>PLC 번호. 보통 0xFF</summary>
    public Byte PCNo { get; set; } = DEFAULT_PC_NO;

    /// <summary>모니터링 타이머. 단위 250ms, 기본 10 (= 2500ms)</summary>
    public UInt16 MonitoringTimer { get; set; } = 0x000A;

    /// <summary>시작 주소 (16비트). 1E 프레임 주소 최대 0xFFFF</summary>
    public UInt16 StartAddress { get; set; }

    /// <summary>소프트 요소 코드 (A 시리즈 호환)</summary>
    public MC1EDeviceCode DeviceCode { get; set; }

    /// <summary>소프트 요소 포인트 수</summary>
    public UInt16 Count { get; set; }

    /// <summary>쓰기 데이터 (쓰기 명령에만 포함). 비트 쓰기는 각 요소 0=OFF 1=ON, 워드 쓰기는 워드 값</summary>
    public UInt16[] WriteData { get; set; }

    #endregion

    #region 생성

    /// <summary>오버라이드. 친화적인 문자열</summary>
    public override String ToString()
    {
        var op = SubHeader switch
        {
            SUB_READ_WORD => "Read Word",
            SUB_READ_BIT => "Read Bit",
            SUB_WRITE_WORD => "Write Word",
            SUB_WRITE_BIT => "Write Bit",
            _ => $"Cmd={SubHeader:X2}",
        };
        return $"MC1E {op} {DeviceCode} addr={StartAddress} count={Count}";
    }

    #endregion

    #region 직렬화

    /// <summary>바이트 배열로 직렬화</summary>
    public Byte[] ToBytes()
    {
        var dataLength = FIXED_HEADER_LEN;

        // 쓰기 데이터 패킹
        Byte[] writeBytes = null;
        if (WriteData != null && WriteData.Length > 0)
        {
            if (SubHeader == SUB_WRITE_BIT || SubHeader == SUB_READ_BIT)
                writeBytes = MCMessage.PackBits(WriteData);
            else
                writeBytes = MCMessage.PackWords(WriteData);
            dataLength += writeBytes.Length;
        }

        var buffer = new Byte[dataLength];
        var offset = 0;

        buffer[offset++] = SubHeader;
        buffer[offset++] = PCNo;
        WriteUInt16LE(buffer, ref offset, MonitoringTimer);
        WriteUInt16LE(buffer, ref offset, StartAddress);
        buffer[offset++] = (Byte)DeviceCode;
        WriteUInt16LE(buffer, ref offset, Count);

        if (writeBytes != null)
        {
            Array.Copy(writeBytes, 0, buffer, offset, writeBytes.Length);
            offset += writeBytes.Length;
        }

        return buffer;
    }

    /// <summary>바이트 배열에서 역직렬화</summary>
    /// <param name="data">프레임 데이터</param>
    /// <returns>성공 여부</returns>
    public Boolean Read(Byte[] data)
    {
        if (data == null || data.Length < FIXED_HEADER_LEN) return false;

        var offset = 0;
        SubHeader = data[offset++];
        PCNo = data[offset++];
        MonitoringTimer = ReadUInt16LE(data, ref offset);
        StartAddress = ReadUInt16LE(data, ref offset);
        DeviceCode = (MC1EDeviceCode)data[offset++];
        Count = ReadUInt16LE(data, ref offset);

        // 쓰기 데이터
        var extraLen = data.Length - FIXED_HEADER_LEN;
        if (extraLen > 0)
        {
            var buf = new Byte[extraLen];
            Array.Copy(data, offset, buf, 0, extraLen);
            WriteData = UnpackWriteData(buf, Count, SubHeader);
        }

        return true;
    }

    /// <summary>해당 1E 응답 객체 생성</summary>
    public MC1EResponse CreateReply() => new();

    #endregion

    #region 팩토리 메서드

    /// <summary>워드 소프트 요소 일괄 읽기 요청 생성</summary>
    /// <param name="devCode">소프트 요소 코드</param>
    /// <param name="startAddr">시작 주소</param>
    /// <param name="count">포인트 수</param>
    public static MC1EMessage BuildReadWord(MC1EDeviceCode devCode, UInt16 startAddr, Int32 count) => new()
    {
        SubHeader = SUB_READ_WORD,
        DeviceCode = devCode,
        StartAddress = startAddr,
        Count = (UInt16)count,
    };

    /// <summary>비트 소프트 요소 일괄 읽기 요청 생성</summary>
    public static MC1EMessage BuildReadBit(MC1EDeviceCode devCode, UInt16 startAddr, Int32 count) => new()
    {
        SubHeader = SUB_READ_BIT,
        DeviceCode = devCode,
        StartAddress = startAddr,
        Count = (UInt16)count,
    };

    /// <summary>워드 소프트 요소 일괄 쓰기 요청 생성</summary>
    public static MC1EMessage BuildWriteWord(MC1EDeviceCode devCode, UInt16 startAddr, UInt16[] values) => new()
    {
        SubHeader = SUB_WRITE_WORD,
        DeviceCode = devCode,
        StartAddress = startAddr,
        Count = (UInt16)values.Length,
        WriteData = values,
    };

    /// <summary>비트 소프트 요소 일괄 쓰기 요청 생성</summary>
    public static MC1EMessage BuildWriteBit(MC1EDeviceCode devCode, UInt16 startAddr, UInt16[] values) => new()
    {
        SubHeader = SUB_WRITE_BIT,
        DeviceCode = devCode,
        StartAddress = startAddr,
        Count = (UInt16)values.Length,
        WriteData = values,
    };

    #endregion

    #region 헬퍼

    private static UInt16[] UnpackWriteData(Byte[] buf, UInt16 count, Byte subHeader)
    {
        if (subHeader == SUB_WRITE_BIT || subHeader == SUB_READ_BIT)
        {
            var result = new UInt16[count];
            for (var i = 0; i < count; i++)
            {
                var byteIdx = i / 2;
                var shift = (i % 2) * 4;
                result[i] = (UInt16)((buf[byteIdx] >> shift) & 0x0F);
            }
            return result;
        }
        else
        {
            var result = new UInt16[buf.Length / 2];
            for (var i = 0; i < result.Length; i++)
                result[i] = (UInt16)(buf[i * 2] | (buf[i * 2 + 1] << 8));
            return result;
        }
    }

    private static void WriteUInt16LE(Byte[] buffer, ref Int32 offset, UInt16 value)
    {
        buffer[offset++] = (Byte)(value & 0xFF);
        buffer[offset++] = (Byte)(value >> 8);
    }

    private static UInt16 ReadUInt16LE(Byte[] data, ref Int32 offset)
    {
        var value = (UInt16)(data[offset] | (data[offset + 1] << 8));
        offset += 2;
        return value;
    }

    #endregion
}