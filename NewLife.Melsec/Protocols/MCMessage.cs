using NewLife.Data;
using NewLife.Serialization;

namespace NewLife.Melsec.Protocols;

/// <summary>MC 프로토콜 3E 프레임 요청 메시지 (바이너리/ASCII 모드)</summary>
/// <remarks>
/// 프레임 형식 (바이너리 Little-Endian):
/// [0~1]  서브헤드    2    고정 0x50 0x00
/// [2]    네트워크 번호 1    보통 0x00
/// [3]    PC 번호     1    보통 0xFF
/// [4~5]  IO 유니트 번호 2    고정 0xFF 0x03 (= 0x03FF LE)
/// [6]    채널 번호   1    보통 0x00
/// [7~8]  데이터 길이 2    LE, 바이트[9]부터의 바이트 수
/// [9~10] 모니터링 타이머 2    LE, 기본 0x000A (10 × 250ms = 2.5s)
/// [11~12]명령        2    LE, 0x0401=벌크 읽기, 0x1401=벌크 쓰기
/// [13~14]서브 명령   2    LE, 0x0000=워드 모드, 0x0001=비트 모드
/// [15~17]시작 소프트 요소 3    LE, 시작 주소 (최대 0xFFFFFF)
/// [18]   소프트 요소 코드 1    DeviceCode enum 참조
/// [19~20]소프트 요소 포인트 수 2    LE
/// [21~]  쓰기 데이터 N    쓰기 명령에만 포함
///
/// ASCII 모드에서는 각 바이트를 2개의 ASCII 16진 문자로 표현하며,
/// 바이트 순서는 바이너리 모드와 동일 (16진 문자열에서 Little-Endian 순서 유지).
/// 예: 명령 0x0401은 바이너리로 [01][04], ASCII로 "0104".
/// </remarks>
public class MCMessage : IAccessor
{
    #region 상수

    /// <summary>벌크 읽기 명령 코드</summary>
    public const UInt16 CMD_READ = 0x0401;

    /// <summary>벌크 쓰기 명령 코드</summary>
    public const UInt16 CMD_WRITE = 0x1401;

    /// <summary>랜덤 읽기 명령 코드</summary>
    public const UInt16 CMD_RANDOM_READ = 0x0403;

    /// <summary>랜덤 쓰기 명령 코드</summary>
    public const UInt16 CMD_RANDOM_WRITE = 0x1403;

    /// <summary>리모트 RUN 명령 코드</summary>
    public const UInt16 CMD_REMOTE_RUN = 0x1001;

    /// <summary>리모트 STOP 명령 코드</summary>
    public const UInt16 CMD_REMOTE_STOP = 0x1002;

    /// <summary>리모트 비밀번호 잠금 해제 명령 코드</summary>
    public const UInt16 CMD_REMOTE_UNLOCK = 0x1630;

    /// <summary>리모트 비밀번호 잠금 명령 코드</summary>
    public const UInt16 CMD_REMOTE_LOCK = 0x1631;

    /// <summary>모니터링 등록 명령 코드</summary>
    public const UInt16 CMD_MONITOR_REGIST = 0x0801;

    /// <summary>워드 소프트 요소 서브 명령</summary>
    public const UInt16 SUBCMD_WORD = 0x0000;

    /// <summary>비트 소프트 요소 서브 명령</summary>
    public const UInt16 SUBCMD_BIT = 0x0001;

    /// <summary>3E 프레임 서브헤드 (요청, 바이너리)</summary>
    public const UInt16 SUB_HEADER = 0x0050;

    /// <summary>4E 프레임 서브헤드 (요청, 바이너리). 4E 프레임은 서브헤드 뒤에 4바이트 시리얼 번호 필드를 추가</summary>
    public const UInt16 SUB_HEADER_4E = 0x0054;

    /// <summary>3E 프레임 서브헤드 (요청, ASCII)</summary>
    public const String SUB_HEADER_ASCII = "5000";

    /// <summary>4E 프레임 서브헤드 (요청, ASCII)</summary>
    public const String SUB_HEADER_4E_ASCII = "5400";

    /// <summary>IO 유니트 번호 (Q/L/FX5U 시리즈 고정값)</summary>
    public const UInt16 IO_UNIT_NO = 0x03FF;

    #endregion

    #region 속성

    /// <summary>데이터 형식. 기본값 Binary</summary>
    public MCDataFormat DataFormat { get; set; } = MCDataFormat.Binary;

    /// <summary>프레임 타입. 기본 3E 프레임, 시리얼 번호 지원을 위해 4E 프레임으로 전환 가능</summary>
    public MCFrameType FrameType { get; set; } = MCFrameType.Frame3E;

    /// <summary>시리얼 번호 (4E 프레임 전용). 요청-응답 엄밀한 매칭 검증에 사용</summary>
    public UInt16 SerialNumber { get; set; }

    /// <summary>네트워크 번호. 보통 0x00 = 로컬</summary>
    public Byte NetworkNo { get; set; } = 0x00;

    /// <summary>PC 번호. 보통 0xFF</summary>
    public Byte PCNo { get; set; } = 0xFF;

    /// <summary>IO 유니트 번호. Q/L/FX5U 시리즈 고정 0x03FF</summary>
    public UInt16 IOUnitNo { get; set; } = IO_UNIT_NO;

    /// <summary>채널 번호. 보통 0x00</summary>
    public Byte ChannelNo { get; set; } = 0x00;

    /// <summary>모니터링 타이머. 단위 250ms, 기본 10 (= 2500ms)</summary>
    public UInt16 MonitoringTimer { get; set; } = 0x000A;

    /// <summary>명령 코드. 0x0401=벌크 읽기, 0x1401=벌크 쓰기</summary>
    public UInt16 Command { get; set; }

    /// <summary>서브 명령. 0x0000=워드 모드, 0x0001=비트 모드</summary>
    public UInt16 SubCommand { get; set; }

    /// <summary>시작 소프트 요소 주소</summary>
    public Int32 StartAddress { get; set; }

    /// <summary>소프트 요소 코드</summary>
    public DeviceCode DeviceCode { get; set; }

    /// <summary>소프트 요소 포인트 수</summary>
    public UInt16 Count { get; set; }

    /// <summary>쓰기 데이터 (쓰기 명령에만 포함). 비트 쓰기는 각 요소 0=OFF 1=ON; 워드 쓰기는 워드 값</summary>
    public UInt16[] WriteData { get; set; }

    /// <summary>원시 요청 데이터. 이 속성을 설정하면 직렬화 시 표준의 시작 주소+소프트 요소 코드+포인트 수 영역 대신 이것을 사용</summary>
    /// <remarks>
    /// 랜덤 읽기 (0403h), 리모트 제어 (1001h/1002h) 등 비표준 요청에 사용.
    /// 설정 후 <see cref="StartAddress"/>, <see cref="DeviceCode"/>, <see cref="Count"/> 는 무시된다.
    /// 서브헤드+네트워크번호+PC번호+IO유니트번호+채널번호+데이터길이+모니터링타이머+명령+서브명령 등 고정 헤더를 포함하지 않는다.
    /// </remarks>
    public Byte[] RawRequestData { get; set; }

    #endregion

    #region 생성

    /// <summary>오버라이드. 친화적인 문자열</summary>
    public override String ToString()
    {
        var cmd = Command == CMD_READ ? "Read" : Command == CMD_WRITE ? "Write" : $"Cmd={Command:X4}";
        var mode = SubCommand == SUBCMD_BIT ? "Bit" : "Word";
        return $"MC {cmd}({mode}) {DeviceCode} addr={StartAddress} count={Count}";
    }

    #endregion

    #region 직렬화

    /// <summary>프레임 데이터 읽기 (역직렬화)</summary>
    /// <param name="stream">데이터 스트림</param>
    /// <param name="context">컨텍스트 (무시)</param>
    /// <returns>성공 여부</returns>
    public virtual Boolean Read(Stream stream, Object context)
    {
        if (DataFormat.IsAscii())
            return ReadAscii(stream);

        // 서브헤드 0x5000 (3E) 또는 0x5400 (4E)
        var sh0 = stream.ReadByte();
        var sh1 = stream.ReadByte();

        if (sh0 == 0x54 && sh1 == 0x00)
        {
            // 4E 프레임: 시리얼 번호 읽기 (4바이트: 2바이트 시리얼 + 2바이트 예약)
            FrameType = MCFrameType.Frame4E;
            SerialNumber = ReadUInt16LE(stream);
            stream.ReadByte(); // 예약 바이트
            stream.ReadByte(); // 예약 바이트
        }
        else if (sh0 == 0x50 && sh1 == 0x00)
        {
            FrameType = MCFrameType.Frame3E;
        }
        else
        {
            return false;
        }

        NetworkNo = (Byte)stream.ReadByte();
        PCNo = (Byte)stream.ReadByte();
        IOUnitNo = ReadUInt16LE(stream);
        ChannelNo = (Byte)stream.ReadByte();

        var dataLength = ReadUInt16LE(stream);
        MonitoringTimer = ReadUInt16LE(stream);
        Command = ReadUInt16LE(stream);
        SubCommand = ReadUInt16LE(stream);

        // 시작 주소 3바이트 LE
        StartAddress = stream.ReadByte() | (stream.ReadByte() << 8) | (stream.ReadByte() << 16);
        DeviceCode = (DeviceCode)stream.ReadByte();
        Count = ReadUInt16LE(stream);

        // 쓰기 데이터 (있는 경우)
        // dataLength = MonTimer(2)+Cmd(2)+SubCmd(2)+StartAddr(3)+DevCode(1)+Count(2) + writeDataBytes
        //            = 12 + writeDataBytes
        var fixedLen = 2 + 2 + 2 + 3 + 1 + 2; // 12
        var extraLen = dataLength - fixedLen;
        if (extraLen > 0)
        {
            var buf = new Byte[extraLen];
            stream.Read(buf, 0, extraLen);
            WriteData = UnpackWriteData(buf, Count, SubCommand);
        }

        return true;
    }

    /// <summary>ASCII 모드 역직렬화</summary>
    private Boolean ReadAscii(Stream stream)
    {
        // 서브헤드 "5000" (3E) 또는 "5400" (4E), 4 ASCII 문자 → 2바이트
        var sh = ReadAsciiHex(stream, 2);

        if (sh[0] == 0x54 && sh[1] == 0x00)
        {
            // 4E 프레임: 시리얼 번호 읽기 (4바이트: 2바이트 시리얼 + 2바이트 예약)
            FrameType = MCFrameType.Frame4E;
            var serialBytes = ReadAsciiHex(stream, 4);
            SerialNumber = (UInt16)(serialBytes[0] | (serialBytes[1] << 8));
        }
        else if (sh[0] == 0x50 && sh[1] == 0x00)
        {
            FrameType = MCFrameType.Frame3E;
        }
        else
        {
            return false;
        }

        NetworkNo = ReadAsciiByte(stream);
        PCNo = ReadAsciiByte(stream);
        IOUnitNo = ReadAsciiUInt16(stream);
        ChannelNo = ReadAsciiByte(stream);

        var dataLength = ReadAsciiUInt16(stream);
        MonitoringTimer = ReadAsciiUInt16(stream);
        Command = ReadAsciiUInt16(stream);
        SubCommand = ReadAsciiUInt16(stream);

        // 시작 주소 3바이트 LE
        var addrBytes = ReadAsciiHex(stream, 3);
        StartAddress = addrBytes[0] | (addrBytes[1] << 8) | (addrBytes[2] << 16);
        DeviceCode = (DeviceCode)ReadAsciiByte(stream);
        Count = ReadAsciiUInt16(stream);

        // 쓰기 데이터 (있는 경우)
        var fixedLen = 2 + 2 + 2 + 3 + 1 + 2; // 12
        var extraLen = dataLength - fixedLen;
        if (extraLen > 0)
        {
            var buf = ReadAsciiHex(stream, extraLen);
            WriteData = UnpackWriteData(buf, Count, SubCommand);
        }

        return true;
    }

    /// <summary>프레임 데이터 쓰기 (직렬화)</summary>
    /// <param name="stream">데이터 스트림</param>
    /// <param name="context">컨텍스트 (무시)</param>
    /// <returns>성공 여부</returns>
    public virtual Boolean Write(Stream stream, Object context)
    {
        if (DataFormat.IsAscii())
            return WriteAscii(stream);

        if (FrameType.Is4E())
        {
            // 4E 프레임 서브헤드 0x54 0x00 + 시리얼 번호 4바이트
            stream.WriteByte(0x54);
            stream.WriteByte(0x00);
            WriteUInt16LE(stream, SerialNumber);
            stream.WriteByte(0x00); // 예약
            stream.WriteByte(0x00); // 예약
        }
        else
        {
            // 3E 프레임 서브헤드 0x50 0x00
            stream.WriteByte(0x50);
            stream.WriteByte(0x00);
        }

        stream.WriteByte(NetworkNo);
        stream.WriteByte(PCNo);
        WriteUInt16LE(stream, IOUnitNo);
        stream.WriteByte(ChannelNo);

        // 쓰기 데이터 패킹
        Byte[] writeBytes = null;
        if (WriteData != null && WriteData.Length > 0)
            writeBytes = SubCommand == SUBCMD_BIT ? PackBits(WriteData) : PackWords(WriteData);

        if (RawRequestData != null)
        {
            // 원시 요청 데이터로 표준 영역 교체
            var dataLength = (UInt16)(12 - 3 - 1 - 2 + RawRequestData.Length); // 표준 영역=6바이트, 커스텀 데이터로 교체
            WriteUInt16LE(stream, dataLength);

            WriteUInt16LE(stream, MonitoringTimer);
            WriteUInt16LE(stream, Command);
            WriteUInt16LE(stream, SubCommand);

            stream.Write(RawRequestData, 0, RawRequestData.Length);
        }
        else
        {
            // 일반: 데이터 길이 = MonTimer(2) + Cmd(2) + SubCmd(2) + StartAddr(3) + DevCode(1) + Count(2) + writeBytes
            var dataLength = (UInt16)(12 + (writeBytes?.Length ?? 0));
            WriteUInt16LE(stream, dataLength);

            WriteUInt16LE(stream, MonitoringTimer);
            WriteUInt16LE(stream, Command);
            WriteUInt16LE(stream, SubCommand);

            // 시작 주소 3바이트 LE
            stream.WriteByte((Byte)(StartAddress & 0xFF));
            stream.WriteByte((Byte)((StartAddress >> 8) & 0xFF));
            stream.WriteByte((Byte)((StartAddress >> 16) & 0xFF));

            stream.WriteByte((Byte)DeviceCode);
            WriteUInt16LE(stream, Count);

            if (writeBytes != null)
                stream.Write(writeBytes, 0, writeBytes.Length);
        }

        return true;
    }

    /// <summary>ASCII 모드 직렬화</summary>
    private Boolean WriteAscii(Stream stream)
    {
        // 먼저 바이너리 바이트로 직렬화한 뒤, 전체를 ASCII 16진 문자열로 변환하여 쓰기
        using var ms = new MemoryStream();

        if (FrameType.Is4E())
        {
            // 4E 프레임 서브헤드 0x54 0x00 + 시리얼 번호 4바이트
            ms.WriteByte(0x54);
            ms.WriteByte(0x00);
            WriteUInt16LE(ms, SerialNumber);
            ms.WriteByte(0x00); // 예약
            ms.WriteByte(0x00); // 예약
        }
        else
        {
            ms.WriteByte(0x50);
            ms.WriteByte(0x00);
        }

        ms.WriteByte(NetworkNo);
        ms.WriteByte(PCNo);
        WriteUInt16LE(ms, IOUnitNo);
        ms.WriteByte(ChannelNo);

        // 쓰기 데이터 패킹
        Byte[] writeBytes = null;
        if (WriteData != null && WriteData.Length > 0)
            writeBytes = SubCommand == SUBCMD_BIT ? PackBits(WriteData) : PackWords(WriteData);

        if (RawRequestData != null)
        {
            var dataLength = (UInt16)(6 + RawRequestData.Length);
            WriteUInt16LE(ms, dataLength);

            WriteUInt16LE(ms, MonitoringTimer);
            WriteUInt16LE(ms, Command);
            WriteUInt16LE(ms, SubCommand);

            ms.Write(RawRequestData, 0, RawRequestData.Length);
        }
        else
        {
            var dataLength = (UInt16)(12 + (writeBytes?.Length ?? 0));
            WriteUInt16LE(ms, dataLength);

            WriteUInt16LE(ms, MonitoringTimer);
            WriteUInt16LE(ms, Command);
            WriteUInt16LE(ms, SubCommand);

            ms.WriteByte((Byte)(StartAddress & 0xFF));
            ms.WriteByte((Byte)((StartAddress >> 8) & 0xFF));
            ms.WriteByte((Byte)((StartAddress >> 16) & 0xFF));

            ms.WriteByte((Byte)DeviceCode);
            WriteUInt16LE(ms, Count);

            if (writeBytes != null)
                ms.Write(writeBytes, 0, writeBytes.Length);
        }

        // 바이너리 바이트 → ASCII 16진 문자열 → 출력 스트림에 쓰기
        var binary = ms.ToArray();
        var hex = binary.ToHex();
        var hexBytes = System.Text.Encoding.ASCII.GetBytes(hex);
        stream.Write(hexBytes, 0, hexBytes.Length);

        return true;
    }

    /// <summary>바이트 배열로 직렬화</summary>
    public Byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        Write(ms, null);
        return ms.ToArray();
    }

    /// <summary>해당 응답 객체 생성 (현재 데이터 형식과 프레임 타입 상속)</summary>
    public MCResponse CreateReply() => new()
    {
        DataFormat = DataFormat,
        FrameType = FrameType,
        SerialNumber = SerialNumber,
        NetworkNo = NetworkNo,
        PCNo = PCNo,
        IOUnitNo = IOUnitNo,
        ChannelNo = ChannelNo,
    };

    #endregion

    #region 팩토리 메서드

    /// <summary>워드 소프트 요소 벌크 읽기 요청 구성</summary>
    /// <param name="devCode">소프트 요소 코드</param>
    /// <param name="startAddr">시작 주소</param>
    /// <param name="count">포인트 수</param>
    public static MCMessage BuildReadWord(DeviceCode devCode, Int32 startAddr, Int32 count) => new()
    {
        Command = CMD_READ,
        SubCommand = SUBCMD_WORD,
        DeviceCode = devCode,
        StartAddress = startAddr,
        Count = (UInt16)count,
    };

    /// <summary>비트 소프트 요소 벌크 읽기 요청 구성</summary>
    /// <param name="devCode">소프트 요소 코드</param>
    /// <param name="startAddr">시작 주소</param>
    /// <param name="count">포인트 수</param>
    public static MCMessage BuildReadBit(DeviceCode devCode, Int32 startAddr, Int32 count) => new()
    {
        Command = CMD_READ,
        SubCommand = SUBCMD_BIT,
        DeviceCode = devCode,
        StartAddress = startAddr,
        Count = (UInt16)count,
    };

    /// <summary>워드 소프트 요소 벌크 쓰기 요청 구성</summary>
    /// <param name="devCode">소프트 요소 코드</param>
    /// <param name="startAddr">시작 주소</param>
    /// <param name="values">워드 데이터 (각 UInt16)</param>
    public static MCMessage BuildWriteWord(DeviceCode devCode, Int32 startAddr, UInt16[] values) => new()
    {
        Command = CMD_WRITE,
        SubCommand = SUBCMD_WORD,
        DeviceCode = devCode,
        StartAddress = startAddr,
        Count = (UInt16)values.Length,
        WriteData = values,
    };

    /// <summary>비트 소프트 요소 벌크 쓰기 요청 구성</summary>
    /// <param name="devCode">소프트 요소 코드</param>
    /// <param name="startAddr">시작 주소</param>
    /// <param name="values">비트 데이터, 0=OFF, 1=ON</param>
    public static MCMessage BuildWriteBit(DeviceCode devCode, Int32 startAddr, UInt16[] values) => new()
    {
        Command = CMD_WRITE,
        SubCommand = SUBCMD_BIT,
        DeviceCode = devCode,
        StartAddress = startAddr,
        Count = (UInt16)values.Length,
        WriteData = values,
    };

    #endregion

    #region 헬퍼

    /// <summary>비트 데이터 (0/1 배열)를 바이트 스트림으로 패킹 (바이트당 저 4비트 = 1번째 포인트, 고 4비트 = 2번째 포인트)</summary>
    internal static Byte[] PackBits(UInt16[] values)
    {
        var len = (values.Length + 1) / 2;
        var result = new Byte[len];
        for (var i = 0; i < values.Length; i++)
        {
            var byteIdx = i / 2;
            var shift = (i % 2) * 4;
            result[byteIdx] |= (Byte)((values[i] & 0x0F) << shift);
        }
        return result;
    }

    /// <summary>워드 데이터를 바이트 스트림으로 패킹 (워드당 2바이트 LE)</summary>
    internal static Byte[] PackWords(UInt16[] values)
    {
        var result = new Byte[values.Length * 2];
        for (var i = 0; i < values.Length; i++)
        {
            result[i * 2] = (Byte)(values[i] & 0xFF);
            result[i * 2 + 1] = (Byte)(values[i] >> 8);
        }
        return result;
    }

    private static UInt16[] UnpackWriteData(Byte[] buf, UInt16 count, UInt16 subCommand)
    {
        if (subCommand == SUBCMD_BIT)
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

    internal static UInt16 ReadUInt16LE(Stream stream) =>
        (UInt16)(stream.ReadByte() | (stream.ReadByte() << 8));

    internal static void WriteUInt16LE(Stream stream, UInt16 value)
    {
        stream.WriteByte((Byte)(value & 0xFF));
        stream.WriteByte((Byte)(value >> 8));
    }

    #endregion

    #region ASCII 헬퍼

    /// <summary>ASCII 스트림에서 지정 바이트 수만큼의 16진 데이터 읽기</summary>
    /// <param name="stream">데이터 스트림</param>
    /// <param name="byteCount">기대하는 바이너리 바이트 수</param>
    internal static Byte[] ReadAsciiHex(Stream stream, Int32 byteCount)
    {
        var hexLen = byteCount * 2;
        var hex = new Char[hexLen];
        for (var i = 0; i < hexLen; i++)
        {
            var b = stream.ReadByte();
            if (b < 0) throw new InvalidOperationException("无法读取 ASCII 十六进制数据，流已结束");
            hex[i] = (Char)b;
        }
        return new String(hex).ToHex();
    }

    /// <summary>ASCII 스트림에서 1바이트의 16진 데이터 읽기</summary>
    internal static Byte ReadAsciiByte(Stream stream)
    {
        var buf = ReadAsciiHex(stream, 1);
        return buf[0];
    }

    /// <summary>ASCII 스트림에서 1개의 UInt16 16진 데이터 읽기</summary>
    internal static UInt16 ReadAsciiUInt16(Stream stream)
    {
        var buf = ReadAsciiHex(stream, 2);
        return (UInt16)(buf[0] | (buf[1] << 8));
    }

    #endregion
}