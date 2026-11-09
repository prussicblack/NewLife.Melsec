using System.Net.Sockets;
using NewLife.Log;

namespace NewLife.Melsec.Protocols;

/// <summary>MC 프로토콜 전송 모드</summary>
public enum MCTransportMode
{
    /// <summary>TCP 전송 (기본)</summary>
    Tcp = 0,

    /// <summary>UDP 전송</summary>
    Udp = 1,

    /// <summary>시리얼 포트 전송 (RS-232/RS-485). 3E/1E와 동일한 프레임 형식, SerialPort로 전송</summary>
    Serial = 2,
}

/// <summary>미쓰비시 MC 프로토콜 스택 (TCP 롱 커넥션, 3E/1E 프레임 지원)</summary>
/// <remarks>
/// PLC와의 TCP 연결을 관리하고 워드/비트 소프트 요소의 벌크 읽기/쓰기를 제공한다.
/// 3E 프레임 (Qna 호환)과 1E 프레임 (A 시리즈 호환) 두 가지 프레임 형식을 지원.
/// 3E 프레임은 바이너리 모드와 ASCII 16진 모드의 두 가지 데이터 형식을 지원.
/// 연결이 끊기면 자동 재연결; 모든 작업은 락을 통해 스레드 안전성을 보장.
/// </remarks>
public class MCProtocol : DisposeBase
{
    #region 속성

    /// <summary>이름</summary>
    public String Name { get; set; }

    /// <summary>PLC 주소. 형식: IP:포트 (예: 192.168.1.10:6000) 또는 시리얼 포트 이름 (예: COM3)</summary>
    public String Address { get; set; }

    /// <summary>프레임 타입. 3E 프레임 (기본), 1E 프레임 또는 4E 프레임</summary>
    public MCFrameType FrameType { get; set; } = MCFrameType.Frame3E;

    /// <summary>4E 프레임 시리얼 번호 카운터. 4E 프레임 요청을 전송할 때마다 증가</summary>
    private UInt16 _serialNumber;

    /// <summary>전송 모드. TCP (기본), UDP 또는 Serial (시리얼 포트)</summary>
    public MCTransportMode TransportMode { get; set; } = MCTransportMode.Tcp;

    /// <summary>네트워크 번호 (3E 프레임 전용). 보통 0x00</summary>
    public Byte NetworkNo { get; set; } = 0x00;

    /// <summary>PC 번호 (3E 프레임 전용). 보통 0xFF</summary>
    public Byte PCNo { get; set; } = 0xFF;

    /// <summary>데이터 형식 (3E 프레임 전용). 기본 바이너리 모드, ASCII 16진 모드로 전환 가능</summary>
    public MCDataFormat DataFormat { get; set; } = MCDataFormat.Binary;

    /// <summary>네트워크 타임아웃 (밀리초). 기본 5000ms</summary>
    public Int32 Timeout { get; set; } = 5000;

    /// <summary>보레이트 (시리얼 포트 모드). 기본 9600</summary>
    public Int32 Baudrate { get; set; } = 9600;

    /// <summary>데이터 비트 (시리얼 포트 모드). 기본 8</summary>
    public Int32 DataBits { get; set; } = 8;

    /// <summary>패리티 (시리얼 포트 모드). 기본 None</summary>
    public System.IO.Ports.Parity Parity { get; set; } = System.IO.Ports.Parity.None;

    /// <summary>스톱 비트 (시리얼 포트 모드). 기본 One</summary>
    public System.IO.Ports.StopBits StopBits { get; set; } = System.IO.Ports.StopBits.One;

    /// <summary>성능 트래커</summary>
    public ITracer Tracer { get; set; }

    /// <summary>로그</summary>
    public ILog Log { get; set; }

    private TcpClient _client;
    private NetworkStream _stream;
    private UdpClient _udp;
    private System.IO.Ports.SerialPort _serialPort;
    private System.Net.IPEndPoint _remoteEndPoint;
    private readonly Object _lock = new();

    #endregion

    #region 생성

    /// <summary>인스턴스화</summary>
    public MCProtocol() => Name = GetType().Name;

    /// <summary>소멸</summary>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        Close();
    }

    #endregion

    #region 연결 관리

    /// <summary>연결 열기 (TCP/UDP/Serial)</summary>
    public void Open()
    {
        if (TransportMode == MCTransportMode.Serial)
        {
            if (_serialPort?.IsOpen == true) return;

            var portName = Address ?? throw new InvalidOperationException("Address is not set (serial mode requires a COM port)");

            _serialPort = new System.IO.Ports.SerialPort(portName, Baudrate, Parity, DataBits, StopBits)
            {
                ReadTimeout = Timeout,
                WriteTimeout = Timeout,
            };
            _serialPort.Open();

            WriteLog("MCProtocol.Open Serial {0} {1},{2},{3},{4}", portName, Baudrate, DataBits, Parity, StopBits);
        }
        else if (TransportMode == MCTransportMode.Udp)
        {
            if (_udp != null) return;

            var addr = Address ?? throw new InvalidOperationException("Address is not set");
            var colonIdx = addr.LastIndexOf(':');
            var host = colonIdx > 0 ? addr[..colonIdx] : addr;
            var port = colonIdx > 0 ? Int32.Parse(addr[(colonIdx + 1)..]) : 6000;

            _udp = new UdpClient(host, port);
            _udp.Client.ReceiveTimeout = Timeout;
            _udp.Client.SendTimeout = Timeout;

            WriteLog("MCProtocol.Open UDP {0}", Address);
        }
        else
        {
            if (_client?.Connected == true) return;

            var addr = Address ?? throw new InvalidOperationException("Address is not set");
            var colonIdx = addr.LastIndexOf(':');
            var host = colonIdx > 0 ? addr[..colonIdx] : addr;
            var port = colonIdx > 0 ? Int32.Parse(addr[(colonIdx + 1)..]) : 6000;

            var client = new TcpClient
            {
                ReceiveTimeout = Timeout,
                SendTimeout = Timeout,
            };
            client.Connect(host, port);
            _client = client;
            _stream = client.GetStream();

            WriteLog("MCProtocol.Open TCP {0}", Address);
        }
    }

    /// <summary>연결 닫기</summary>
    public void Close()
    {
        _stream?.Dispose();
        _stream = null;
        _client?.TryDispose();
        _client = null;
        if (_udp != null) { ((IDisposable)_udp).Dispose(); _udp = null; }
        if (_serialPort != null)
        {
            if (_serialPort.IsOpen) _serialPort.Close();
            _serialPort.Dispose();
            _serialPort = null;
        }
    }

    /// <summary>연결이 유효한지 확인, 끊기면 재연결</summary>
    protected void EnsureConnect()
    {
        if (TransportMode == MCTransportMode.Serial)
        {
            if (_serialPort?.IsOpen != true) Open();
        }
        else if (TransportMode == MCTransportMode.Udp)
        {
            Open();
        }
        else if (_client?.Connected != true)
        {
            Open();
        }
    }

    #endregion

    #region 읽기/쓰기 인터페이스

    /// <summary>워드 소프트 요소 벌크 읽기</summary>
    /// <param name="devCode">소프트 요소 코드</param>
    /// <param name="startAddr">시작 주소</param>
    /// <param name="count">포인트 수 (최대 960)</param>
    /// <returns>워드 데이터 배열</returns>
    public virtual UInt16[] ReadWords(DeviceCode devCode, Int32 startAddr, Int32 count)
    {
        var msg = MCMessage.BuildReadWord(devCode, startAddr, count);
        msg.NetworkNo = NetworkNo;
        msg.PCNo = PCNo;
        msg.DataFormat = DataFormat;
        msg.FrameType = FrameType;

        var response = SendCommand(msg);
        if (response.EndCode != 0)
            throw new MCException(response.EndCode);

        return response.GetWordData();
    }

    /// <summary>비트 소프트 요소 벌크 읽기</summary>
    /// <param name="devCode">소프트 요소 코드</param>
    /// <param name="startAddr">시작 주소</param>
    /// <param name="count">포인트 수 (최대 7168)</param>
    /// <returns>비트 데이터 배열 (true=ON, false=OFF)</returns>
    public virtual Boolean[] ReadBits(DeviceCode devCode, Int32 startAddr, Int32 count)
    {
        var msg = MCMessage.BuildReadBit(devCode, startAddr, count);
        msg.NetworkNo = NetworkNo;
        msg.PCNo = PCNo;
        msg.DataFormat = DataFormat;
        msg.FrameType = FrameType;

        var response = SendCommand(msg);
        if (response.EndCode != 0)
            throw new MCException(response.EndCode);

        return response.GetBitData(count);
    }

    /// <summary>워드 소프트 요소 벌크 쓰기</summary>
    /// <param name="devCode">소프트 요소 코드</param>
    /// <param name="startAddr">시작 주소</param>
    /// <param name="values">워드 데이터 (최대 960 워드)</param>
    public virtual void WriteWords(DeviceCode devCode, Int32 startAddr, UInt16[] values)
    {
        var msg = MCMessage.BuildWriteWord(devCode, startAddr, values);
        msg.NetworkNo = NetworkNo;
        msg.PCNo = PCNo;
        msg.DataFormat = DataFormat;
        msg.FrameType = FrameType;

        var response = SendCommand(msg);
        if (response.EndCode != 0)
            throw new MCException(response.EndCode);
    }

    /// <summary>비트 소프트 요소 벌크 쓰기</summary>
    /// <param name="devCode">소프트 요소 코드</param>
    /// <param name="startAddr">시작 주소</param>
    /// <param name="values">비트 데이터 (true=ON, false=OFF)</param>
    public virtual void WriteBits(DeviceCode devCode, Int32 startAddr, Boolean[] values)
    {
        var ushorts = Array.ConvertAll(values, v => v ? (UInt16)1 : (UInt16)0);
        var msg = MCMessage.BuildWriteBit(devCode, startAddr, ushorts);
        msg.NetworkNo = NetworkNo;
        msg.PCNo = PCNo;
        msg.DataFormat = DataFormat;
        msg.FrameType = FrameType;

        var response = SendCommand(msg);
        if (response.EndCode != 0)
            throw new MCException(response.EndCode);
    }

    #endregion

    #region 1E 프레임 읽기/쓰기

    /// <summary>1E 프레임 워드 소프트 요소 벌크 읽기</summary>
    /// <param name="devCode">소프트 요소 코드 (A 시리즈 호환)</param>
    /// <param name="startAddr">시작 주소 (16비트)</param>
    /// <param name="count">포인트 수</param>
    /// <returns>워드 데이터 배열</returns>
    public virtual UInt16[] ReadWords1E(MC1EDeviceCode devCode, UInt16 startAddr, Int32 count)
    {
        var msg = MC1EMessage.BuildReadWord(devCode, startAddr, count);
        var response = SendCommand1E(msg);
        if (response.EndCode != 0)
            throw new MCException(response.EndCode);

        return response.GetWordData();
    }

    /// <summary>1E 프레임 비트 소프트 요소 벌크 읽기</summary>
    /// <param name="devCode">소프트 요소 코드 (A 시리즈 호환)</param>
    /// <param name="startAddr">시작 주소 (16비트)</param>
    /// <param name="count">포인트 수</param>
    /// <returns>비트 데이터 배열 (true=ON, false=OFF)</returns>
    public virtual Boolean[] ReadBits1E(MC1EDeviceCode devCode, UInt16 startAddr, Int32 count)
    {
        var msg = MC1EMessage.BuildReadBit(devCode, startAddr, count);
        var response = SendCommand1E(msg);
        if (response.EndCode != 0)
            throw new MCException(response.EndCode);

        return response.GetBitData(count);
    }

    /// <summary>1E 프레임 워드 소프트 요소 벌크 쓰기</summary>
    /// <param name="devCode">소프트 요소 코드 (A 시리즈 호환)</param>
    /// <param name="startAddr">시작 주소 (16비트)</param>
    /// <param name="values">워드 데이터</param>
    public virtual void WriteWords1E(MC1EDeviceCode devCode, UInt16 startAddr, UInt16[] values)
    {
        var msg = MC1EMessage.BuildWriteWord(devCode, startAddr, values);
        var response = SendCommand1E(msg);
        if (response.EndCode != 0)
            throw new MCException(response.EndCode);
    }

    /// <summary>1E 프레임 비트 소프트 요소 벌크 쓰기</summary>
    /// <param name="devCode">소프트 요소 코드 (A 시리즈 호환)</param>
    /// <param name="startAddr">시작 주소 (16비트)</param>
    /// <param name="values">비트 데이터 (true=ON, false=OFF)</param>
    public virtual void WriteBits1E(MC1EDeviceCode devCode, UInt16 startAddr, Boolean[] values)
    {
        var ushorts = Array.ConvertAll(values, v => v ? (UInt16)1 : (UInt16)0);
        var msg = MC1EMessage.BuildWriteBit(devCode, startAddr, ushorts);
        var response = SendCommand1E(msg);
        if (response.EndCode != 0)
            throw new MCException(response.EndCode);
    }

    #endregion

    #region 타입 변환 읽기

    /// <summary>Int32 타입 데이터 벌크 읽기 (값당 2워드, 부호 있음)</summary>
    /// <param name="devCode">소프트 요소 코드</param>
    /// <param name="startAddr">시작 주소</param>
    /// <param name="count">포인트 수</param>
    public virtual Int32[] ReadInt32(DeviceCode devCode, Int32 startAddr, Int32 count)
    {
        var words = ReadWords(devCode, startAddr, count * 2);
        var result = new Int32[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = (Int32)(UInt32)(words[i * 2] | (words[i * 2 + 1] << 16));
        }
        return result;
    }

    /// <summary>UInt32 타입 데이터 벌크 읽기 (값당 2워드)</summary>
    /// <param name="devCode">소프트 요소 코드</param>
    /// <param name="startAddr">시작 주소</param>
    /// <param name="count">포인트 수</param>
    public virtual UInt32[] ReadUInt32(DeviceCode devCode, Int32 startAddr, Int32 count)
    {
        var words = ReadWords(devCode, startAddr, count * 2);
        var result = new UInt32[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = (UInt32)(words[i * 2] | (words[i * 2 + 1] << 16));
        }
        return result;
    }

    /// <summary>Single (Float) 타입 데이터 벌크 읽기 (값당 2워드, IEEE 754)</summary>
    /// <param name="devCode">소프트 요소 코드</param>
    /// <param name="startAddr">시작 주소</param>
    /// <param name="count">포인트 수</param>
    public virtual Single[] ReadSingle(DeviceCode devCode, Int32 startAddr, Int32 count)
    {
        var words = ReadWords(devCode, startAddr, count * 2);
        var result = new Single[count];
        for (var i = 0; i < count; i++)
        {
            var raw = (UInt32)(words[i * 2] | (words[i * 2 + 1] << 16));
            result[i] = BitConverter.ToSingle(BitConverter.GetBytes(raw), 0);
        }
        return result;
    }

    /// <summary>Double 타입 데이터 벌크 읽기 (값당 4워드, IEEE 754)</summary>
    /// <param name="devCode">소프트 요소 코드</param>
    /// <param name="startAddr">시작 주소</param>
    /// <param name="count">포인트 수</param>
    public virtual Double[] ReadDouble(DeviceCode devCode, Int32 startAddr, Int32 count)
    {
        var words = ReadWords(devCode, startAddr, count * 4);
        var result = new Double[count];
        for (var i = 0; i < count; i++)
        {
            var b0 = BitConverter.GetBytes(words[i * 4]);
            var b1 = BitConverter.GetBytes(words[i * 4 + 1]);
            var b2 = BitConverter.GetBytes(words[i * 4 + 2]);
            var b3 = BitConverter.GetBytes(words[i * 4 + 3]);
            var raw = new Byte[8];
            Array.Copy(b0, 0, raw, 0, 2);
            Array.Copy(b1, 0, raw, 2, 2);
            Array.Copy(b2, 0, raw, 4, 2);
            Array.Copy(b3, 0, raw, 6, 2);
            result[i] = BitConverter.ToDouble(raw, 0);
        }
        return result;
    }

    /// <summary>문자열 타입 데이터 읽기</summary>
    /// <param name="devCode">소프트 요소 코드</param>
    /// <param name="startAddr">시작 주소</param>
    /// <param name="wordLength">워드 수</param>
    /// <returns>ASCII 인코딩 문자열 (끝 공백 및 null 문자 제거)</returns>
    public virtual String ReadString(DeviceCode devCode, Int32 startAddr, Int32 wordLength)
    {
        var words = ReadWords(devCode, startAddr, wordLength);
        var bytes = new Byte[wordLength * 2];
        for (var i = 0; i < wordLength; i++)
        {
            bytes[i * 2] = (Byte)(words[i] & 0xFF);
            bytes[i * 2 + 1] = (Byte)(words[i] >> 8);
        }
        var str = System.Text.Encoding.ASCII.GetString(bytes).TrimEnd('\0', ' ', '\t');
        return str;
    }

    /// <summary>1E 프레임 Int32 타입 데이터 벌크 읽기</summary>
    public virtual Int32[] ReadInt32_1E(MC1EDeviceCode devCode, UInt16 startAddr, Int32 count)
    {
        var words = ReadWords1E(devCode, startAddr, count * 2);
        var result = new Int32[count];
        for (var i = 0; i < count; i++)
            result[i] = (Int32)(UInt32)(words[i * 2] | (words[i * 2 + 1] << 16));
        return result;
    }

    /// <summary>1E 프레임 Single 타입 데이터 벌크 읽기</summary>
    public virtual Single[] ReadSingle_1E(MC1EDeviceCode devCode, UInt16 startAddr, Int32 count)
    {
        var words = ReadWords1E(devCode, startAddr, count * 2);
        var result = new Single[count];
        for (var i = 0; i < count; i++)
        {
            var raw = (UInt32)(words[i * 2] | (words[i * 2 + 1] << 16));
            result[i] = BitConverter.ToSingle(BitConverter.GetBytes(raw), 0);
        }
        return result;
    }

    #endregion

    #region 랜덤 읽기 (Random Read)

    /// <summary>여러 워드 소프트 요소 랜덤 읽기 (명령 0403h)</summary>
    /// <remarks>
    /// 단일 요청으로 여러 불연속 주소의 워드 소프트 요소 값을 읽는다.
    /// 각 주소당 1워드 (2바이트) 반환.
    /// </remarks>
    /// <param name="items">랜덤 읽기 항목 목록 (소프트 요소 코드 + 시작 주소)</param>
    /// <returns>워드 데이터 배열, 요청 순서대로 정렬</returns>
    public virtual UInt16[] ReadRandomWords(params (DeviceCode code, Int32 addr)[] items)
    {
        var raw = BuildRandomReadRequestData(items);
        var msg = new MCMessage
        {
            Command = MCMessage.CMD_RANDOM_READ,
            SubCommand = MCMessage.SUBCMD_WORD,
            RawRequestData = raw,
        };
        msg.NetworkNo = NetworkNo;
        msg.PCNo = PCNo;
        msg.DataFormat = DataFormat;
        msg.FrameType = FrameType;

        var response = SendCommand(msg);
        if (response.EndCode != 0)
            throw new MCException(response.EndCode);

        return response.GetWordData();
    }

    /// <summary>여러 비트 소프트 요소 랜덤 읽기 (명령 0403h)</summary>
    /// <param name="items">랜덤 읽기 항목 목록 (소프트 요소 코드 + 시작 주소)</param>
    /// <returns>비트 데이터 배열, 요청 순서대로 정렬</returns>
    public virtual Boolean[] ReadRandomBits(params (DeviceCode code, Int32 addr)[] items)
    {
        var raw = BuildRandomReadRequestData(items);
        var msg = new MCMessage
        {
            Command = MCMessage.CMD_RANDOM_READ,
            SubCommand = MCMessage.SUBCMD_BIT,
            RawRequestData = raw,
        };
        msg.NetworkNo = NetworkNo;
        msg.PCNo = PCNo;
        msg.DataFormat = DataFormat;
        msg.FrameType = FrameType;

        var response = SendCommand(msg);
        if (response.EndCode != 0)
            throw new MCException(response.EndCode);

        return response.GetBitData(items.Length);
    }

    /// <summary>랜덤 읽기 요청 데이터 구성</summary>
    private static Byte[] BuildRandomReadRequestData((DeviceCode code, Int32 addr)[] items)
    {
        using var ms = new MemoryStream();
        // 포인트 수 (1바이트)
        ms.WriteByte((Byte)items.Length);
        for (var i = 0; i < items.Length; i++)
        {
            ms.WriteByte((Byte)items[i].code);
            var addr = items[i].addr;
            ms.WriteByte((Byte)(addr & 0xFF));
            ms.WriteByte((Byte)((addr >> 8) & 0xFF));
            ms.WriteByte((Byte)((addr >> 16) & 0xFF));
        }
        return ms.ToArray();
    }

    #endregion

    #region 리모트 제어 (RUN/STOP)

    /// <summary>리모트 RUN (실행)</summary>
    /// <param name="clearMode">클리어 모드: 0=클리어 안 함, 1=클리어</param>
    public virtual void RemoteRun(Byte clearMode = 0)
    {
        var raw = new Byte[] { clearMode };
        var msg = new MCMessage
        {
            Command = MCMessage.CMD_REMOTE_RUN,
            SubCommand = 0x0000,
            RawRequestData = raw,
        };
        msg.NetworkNo = NetworkNo;
        msg.PCNo = PCNo;
        msg.DataFormat = DataFormat;
        msg.FrameType = FrameType;

        var response = SendCommand(msg);
        if (response.EndCode != 0)
            throw new MCException(response.EndCode);
    }

    /// <summary>리모트 STOP (정지)</summary>
    public virtual void RemoteStop()
    {
        var msg = new MCMessage
        {
            Command = MCMessage.CMD_REMOTE_STOP,
            SubCommand = 0x0000,
        };
        msg.NetworkNo = NetworkNo;
        msg.PCNo = PCNo;
        msg.DataFormat = DataFormat;
        msg.FrameType = FrameType;

        var response = SendCommand(msg);
        if (response.EndCode != 0)
            throw new MCException(response.EndCode);
    }

    /// <summary>리모트 비밀번호 잠금 해제. 비밀번호를 보내서 PLC가 리모트 조작을 허용하도록 함</summary>
    /// <remarks>
    /// MC 프로토콜 명령 1630h. 비밀번호는 ASCII 문자열이며, 전송 형식:
    /// 비밀번호 길이(2B LE) + 비밀번호 ASCII(N바이트).
    /// 잠금 해제 후 PLC가 리모트 RUN/STOP 등 조작을 허용.
    /// </remarks>
    /// <param name="password">비밀번호. ASCII 문자열</param>
    public virtual void RemoteUnlock(String password)
    {
        if (password == null) throw new ArgumentNullException(nameof(password));
        if (password.Length == 0) throw new ArgumentException("Password cannot be empty", nameof(password));

        var raw = BuildPasswordRequestData(password);
        var msg = new MCMessage
        {
            Command = MCMessage.CMD_REMOTE_UNLOCK,
            SubCommand = 0x0000,
            RawRequestData = raw,
        };
        msg.NetworkNo = NetworkNo;
        msg.PCNo = PCNo;
        msg.DataFormat = DataFormat;
        msg.FrameType = FrameType;

        var response = SendCommand(msg);
        if (response.EndCode != 0)
            throw new MCException(response.EndCode);
    }

    /// <summary>리모트 비밀번호 잠금. 비밀번호를 보내서 PLC를 다시 잠금하고 리모트 조작을 차단</summary>
    /// <remarks>
    /// MC 프로토콜 명령 1631h. RemoteUnlock와 동일한 비밀번호 형식을 사용.
    /// 잠금 후 PLC가 리모트 RUN/STOP 등 조작을 거부.
    /// </remarks>
    /// <param name="password">비밀번호. ASCII 문자열</param>
    public virtual void RemoteLock(String password)
    {
        if (password == null) throw new ArgumentNullException(nameof(password));
        if (password.Length == 0) throw new ArgumentException("Password cannot be empty", nameof(password));

        var raw = BuildPasswordRequestData(password);
        var msg = new MCMessage
        {
            Command = MCMessage.CMD_REMOTE_LOCK,
            SubCommand = 0x0000,
            RawRequestData = raw,
        };
        msg.NetworkNo = NetworkNo;
        msg.PCNo = PCNo;
        msg.DataFormat = DataFormat;
        msg.FrameType = FrameType;

        var response = SendCommand(msg);
        if (response.EndCode != 0)
            throw new MCException(response.EndCode);
    }

    /// <summary>리모트 비밀번호 잠금/해제 요청 데이터 구성</summary>
    /// <param name="password">ASCII 비밀번호</param>
    /// <returns>요청 데이터: 비밀번호 길이(2B LE) + 비밀번호 ASCII</returns>
    private static Byte[] BuildPasswordRequestData(String password)
    {
        var pwdBytes = System.Text.Encoding.ASCII.GetBytes(password);
        if (pwdBytes.Length > 255) throw new ArgumentOutOfRangeException(nameof(password), "Password too long, max 255 bytes");

        using var ms = new MemoryStream();
        MCMessage.WriteUInt16LE(ms, (UInt16)pwdBytes.Length);
        ms.Write(pwdBytes, 0, pwdBytes.Length);
        return ms.ToArray();
    }

    /// <summary>모니터링 등록. 여러 워드/더블 워드 소프트 요소 주소를 등록하면, PLC가 주기적으로 해당 주소의 값을 갱신</summary>
    /// <remarks>
    /// MC 프로토콜 명령 0801h. 등록 후 <see cref="MonitorRead"/>를 호출하여 등록된 주소의 최신 값을 읽을 수 있다.
    /// 1회 최대 192개 워드 주소 + 192개 더블 워드 주소 (PLC 시리즈에 따라 다름).
    /// </remarks>
    /// <param name="wordDevices">워드 소프트 요소 목록 (소프트 요소 코드 + 시작 주소)</param>
    /// <param name="doubleWordDevices">더블 워드 소프트 요소 목록 (소프트 요소 코드 + 시작 주소)</param>
    public virtual void MonitorRegist(
        (DeviceCode code, Int32 addr)[] wordDevices,
        (DeviceCode code, Int32 addr)[] doubleWordDevices)
    {
        if (wordDevices == null) throw new ArgumentNullException(nameof(wordDevices));
        if (doubleWordDevices == null) throw new ArgumentNullException(nameof(doubleWordDevices));
        if (wordDevices.Length + doubleWordDevices.Length == 0) throw new ArgumentException("At least one monitor address is required");

        var raw = BuildMonitorRegistRequestData(wordDevices, doubleWordDevices);
        var msg = new MCMessage
        {
            Command = MCMessage.CMD_MONITOR_REGIST,
            SubCommand = 0x0000,
            RawRequestData = raw,
        };
        msg.NetworkNo = NetworkNo;
        msg.PCNo = PCNo;
        msg.DataFormat = DataFormat;
        msg.FrameType = FrameType;

        var response = SendCommand(msg);
        if (response.EndCode != 0)
            throw new MCException(response.EndCode);
    }

    /// <summary>모니터링 등록 요청 데이터 구성</summary>
    private static Byte[] BuildMonitorRegistRequestData(
        (DeviceCode code, Int32 addr)[] wordDevices,
        (DeviceCode code, Int32 addr)[] doubleWordDevices)
    {
        if (wordDevices.Length > 255) throw new ArgumentOutOfRangeException(nameof(wordDevices), "Word device count cannot exceed 255");
        if (doubleWordDevices.Length > 255) throw new ArgumentOutOfRangeException(nameof(doubleWordDevices), "Double word device count cannot exceed 255");

        using var ms = new MemoryStream();
        // 워드 디바이스 수 (1바이트)
        ms.WriteByte((Byte)wordDevices.Length);
        // 더블 워드 디바이스 수 (1바이트)
        ms.WriteByte((Byte)doubleWordDevices.Length);
        // 예약 (2바이트)
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);

        // 워드 디바이스 주소 목록: 주소(3B LE) + 코드(1B)
        foreach (var (code, addr) in wordDevices)
        {
            ms.WriteByte((Byte)(addr & 0xFF));
            ms.WriteByte((Byte)((addr >> 8) & 0xFF));
            ms.WriteByte((Byte)((addr >> 16) & 0xFF));
            ms.WriteByte((Byte)code);
        }

        // 더블 워드 디바이스 주소 목록: 주소(3B LE) + 코드(1B)
        foreach (var (code, addr) in doubleWordDevices)
        {
            ms.WriteByte((Byte)(addr & 0xFF));
            ms.WriteByte((Byte)((addr >> 8) & 0xFF));
            ms.WriteByte((Byte)((addr >> 16) & 0xFF));
            ms.WriteByte((Byte)code);
        }

        return ms.ToArray();
    }

    #endregion

    #region 저수준 통신

    /// <summary>명령 전송 및 응답 수신 (스레드 안전, 3E/4E 프레임)</summary>
    /// <param name="request">요청 메시지</param>
    /// <returns>응답 메시지</returns>
    internal protected virtual MCResponse SendCommand(MCMessage request)
    {
        lock (_lock)
        {
            EnsureConnect();

            // 4E 프레임 시리얼 번호 자동 할당
            if (request.FrameType.Is4E())
            {
                request.SerialNumber = ++_serialNumber;
            }

            var buf = request.ToBytes();
            using var span = Tracer?.NewSpan("mc:SendCommand", buf.ToHex("-"));

            Log?.Debug("{0}=> {1}", Address, buf.ToHex("-", 64));

            try
            {
                if (TransportMode == MCTransportMode.Serial)
                {
                    _serialPort.Write(buf, 0, buf.Length);
                    return ReceiveSerialResponse(span);
                }
                else if (TransportMode == MCTransportMode.Udp)
                {
                    _udp.Send(buf, buf.Length);

                    MCResponse response;
                    if (DataFormat.IsAscii())
                    {
                        response = ReceiveAsciiResponseUdp(span);
                    }
                    else
                    {
                        response = ReceiveBinaryResponseUdp(span);
                    }

                    return response;
                }
                else
                {
                    _stream.Write(buf, 0, buf.Length);

                    MCResponse response;
                    if (DataFormat.IsAscii())
                    {
                        response = ReceiveAsciiResponse(span);
                    }
                    else
                    {
                        response = ReceiveBinaryResponse(span);
                    }

                    return response;
                }
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);
                // 연결이 끊겼을 수 있음, 다음에 재연결
                Close();
                throw;
            }
        }
    }

    /// <summary>1E 프레임 명령 전송 및 응답 수신 (스레드 안전)</summary>
    /// <param name="request">1E 요청 메시지</param>
    /// <returns>1E 응답 메시지</returns>
    internal protected virtual MC1EResponse SendCommand1E(MC1EMessage request)
    {
        lock (_lock)
        {
            EnsureConnect();

            var buf = request.ToBytes();
            using var span = Tracer?.NewSpan("mc:SendCommand1E", buf.ToHex("-"));

            Log?.Debug("{0}=> {1}", Address, buf.ToHex("-", 64));

            try
            {
                if (TransportMode == MCTransportMode.Serial)
                {
                    _serialPort.Write(buf, 0, buf.Length);
                    return ReceiveSerialResponse1E(request, span);
                }
                else if (TransportMode == MCTransportMode.Udp)
                {
                    _udp.Send(buf, buf.Length);
                    var udpResult = _udp.Receive(ref _remoteEndPoint);
                    var udpResponse = new MC1EResponse();
                    udpResponse.Read(udpResult);
                    return udpResponse;
                }

                _stream.Write(buf, 0, buf.Length);

                // 1E 응답 고정 헤더: 서브헤드(1) + 종료 코드(1) = 2바이트
                var header = new Byte[MC1EResponse.FIXED_HEADER_LEN];
                ReadFully(_stream, header, 0, header.Length);

                // 응답 데이터로 나머지 길이 계산
                // 워드 읽기: 워드당 2바이트; 비트 읽기: 2포인트당 1바이트
                // 고정 헤더 이후가 곧 응답 데이터, 길이 필드 없음, 요청 시 count로 산출
                // 실제로 1E 프레임에도 데이터 길이 필드가 없어, 읽기/쓰기 타입과 count로 산출해야 함
                var response = new MC1EResponse();
                response.SubHeader = header[0];
                response.EndCode = header[1];

                if (response.EndCode == 0)
                {
                    // 요청의 조작 타입과 데이터 포인트 수로 나머지 데이터 길이 산출
                    var dataLen = CalcResponseDataLength(request);
                    if (dataLen > 0)
                    {
                        var data = new Byte[dataLen];
                        ReadFully(_stream, data, 0, dataLen);
                        response.RawData = data;
                    }
                }

                if (span != null) span.Tag += Environment.NewLine + response.ToBytes().ToHex("-", 64);
                Log?.Debug("{0}<= {1}", Address, response.ToBytes().ToHex("-", 64));

                return response;
            }
            catch (Exception ex)
            {
                span?.SetError(ex, null);
                Close();
                throw;
            }
        }
    }

    /// <summary>1E 프레임 응답 데이터 길이 계산</summary>
    private static Int32 CalcResponseDataLength(MC1EMessage request)
    {
        var isBit = request.SubHeader == MC1EMessage.SUB_READ_BIT ||
                    request.SubHeader == MC1EMessage.SUB_WRITE_BIT;
        var count = request.Count;

        if (isBit)
        {
            // 비트 모드: 2포인트당 1바이트
            return (count + 1) / 2;
        }
        else
        {
            // 워드 모드: 워드당 2바이트
            return count * 2;
        }
    }

    /// <summary>바이너리 모드 응답 수신</summary>
    private MCResponse ReceiveBinaryResponse(ISpan span)
    {
        // 먼저 서브헤드 2바이트를 읽어 프레임 타입 판단
        var subHeader = new Byte[2];
        ReadFully(_stream, subHeader, 0, 2);

        var is4E = (subHeader[0] == 0xD4 && subHeader[1] == 0x00);
        var headerLen = is4E ? MCResponse.FIXED_HEADER_4E_LEN : MCResponse.FIXED_HEADER_LEN;

        // 나머지 고정 헤더 읽기
        var remainingHeader = new Byte[headerLen - 2];
        ReadFully(_stream, remainingHeader, 0, remainingHeader.Length);

        // 완전한 고정 헤더 결합
        var header = new Byte[headerLen];
        Array.Copy(subHeader, 0, header, 0, 2);
        Array.Copy(remainingHeader, 0, header, 2, remainingHeader.Length);

        // 헤더에서 데이터 길이 읽기
        // 3E: 오프셋 7~8; 4E: 오프셋 11~12 (4바이트 시리얼 번호 추가)
        var dataLengthOffset = is4E ? 11 : 7;
        var dataLength = header[dataLengthOffset] | (header[dataLengthOffset + 1] << 8);

        // 가변 부분 읽기: 종료 코드(2) + 응답 데이터(N)
        var data = new Byte[dataLength];
        ReadFully(_stream, data, 0, dataLength);

        // 결합하여 해독
        var all = new Byte[header.Length + data.Length];
        Array.Copy(header, 0, all, 0, header.Length);
        Array.Copy(data, 0, all, header.Length, data.Length);

        if (span != null) span.Tag += Environment.NewLine + all.ToHex("-", 64);
        Log?.Debug("{0}<= {1}", Address, all.ToHex("-", 64));

        var response = new MCResponse();
        response.Read(new MemoryStream(all), null);
        return response;
    }

    /// <summary>시리얼 포트 바이너리/ASCII 모드 응답 수신</summary>
    private MCResponse ReceiveSerialResponse(ISpan span)
    {
        // 시리얼 포트는 메시지 경계가 없으므로, 일정 시간 대기 후 사용 가능한 바이트를 모두 읽음
        var totalTimeout = Timeout;
        var elapsed = 0;
        var waitBetween = 50;

        // 데이터 도달 대기
        while (elapsed < totalTimeout && _serialPort.BytesToRead < 2)
        {
            System.Threading.Thread.Sleep(waitBetween);
            elapsed += waitBetween;
        }

        if (_serialPort.BytesToRead == 0)
            throw new TimeoutException("MC serial response timeout");

        // 사용 가능한 바이트 읽기
        var available = _serialPort.BytesToRead;
        var result = new Byte[available];
        _serialPort.Read(result, 0, available);

        if (span != null) span.Tag += Environment.NewLine + result.ToHex("-", 64);
        Log?.Debug("{0}<= {1}", Address, result.ToHex("-", 64));

        var response = new MCResponse { DataFormat = DataFormat };
        response.Read(new MemoryStream(result), null);
        return response;
    }

    /// <summary>시리얼 포트 1E 프레임 응답 수신</summary>
    private MC1EResponse ReceiveSerialResponse1E(MC1EMessage request, ISpan span)
    {
        var totalTimeout = Timeout;
        var elapsed = 0;
        var waitBetween = 50;

        while (elapsed < totalTimeout && _serialPort.BytesToRead < MC1EResponse.FIXED_HEADER_LEN)
        {
            System.Threading.Thread.Sleep(waitBetween);
            elapsed += waitBetween;
        }

        if (_serialPort.BytesToRead == 0)
            throw new TimeoutException("MC 1E serial response timeout");

        var available = _serialPort.BytesToRead;
        var result = new Byte[available];
        _serialPort.Read(result, 0, available);

        if (span != null) span.Tag += Environment.NewLine + result.ToHex("-", 64);
        Log?.Debug("{0}<= {1}", Address, result.ToHex("-", 64));

        var response = new MC1EResponse();
        response.Read(result);
        return response;
    }

    /// <summary>UDP 바이너리 모드 응답 수신</summary>
    private MCResponse ReceiveBinaryResponseUdp(ISpan span)
    {
        var result = _udp.Receive(ref _remoteEndPoint);
        var response = new MCResponse();
        response.Read(new MemoryStream(result), null);
        if (span != null) span.Tag += Environment.NewLine + result.ToHex("-", 64);
        Log?.Debug("{0}<= {1}", Address, result.ToHex("-", 64));
        return response;
    }

    /// <summary>UDP ASCII 모드 응답 수신</summary>
    private MCResponse ReceiveAsciiResponseUdp(ISpan span)
    {
        var result = _udp.Receive(ref _remoteEndPoint);
        var response = new MCResponse { DataFormat = DataFormat };
        response.Read(new MemoryStream(result), null);
        if (span != null) span.Tag += Environment.NewLine + result.ToHex("-", 64);
        Log?.Debug("{0}<= {1}", Address, result.ToHex("-", 64));
        return response;
    }

    /// <summary>ASCII 모드 응답 수신</summary>
    private MCResponse ReceiveAsciiResponse(ISpan span)
    {
        // ASCII 모드: 각 바이트를 2개의 ASCII 16진 문자로 표현
        // 먼저 서브헤드 "D000" 또는 "D400" (4 ASCII 문자)을 읽어 프레임 타입 판단
        var subHeaderBuf = new Byte[4];
        ReadFully(_stream, subHeaderBuf, 0, 4);
        var subHeaderHex = System.Text.Encoding.ASCII.GetString(subHeaderBuf);
        var subHeader = subHeaderHex.ToHex();

        var is4E = (subHeader[0] == 0xD4 && subHeader[1] == 0x00);
        var headerAsciiLen = is4E ? MCResponse.FIXED_HEADER_4E_ASCII_LEN : MCResponse.FIXED_HEADER_ASCII_LEN;

        // 나머지 고정 헤더 읽기
        var remainingAsciiLen = headerAsciiLen - 4;
        var remainingBuf = new Byte[remainingAsciiLen];
        if (remainingAsciiLen > 0)
            ReadFully(_stream, remainingBuf, 0, remainingAsciiLen);

        var remainingHex = System.Text.Encoding.ASCII.GetString(remainingBuf);
        var remaining = remainingHex.ToHex();

        // 완전한 고정 헤더 결합 (바이너리)
        var header = new Byte[subHeader.Length + remaining.Length];
        Array.Copy(subHeader, 0, header, 0, subHeader.Length);
        Array.Copy(remaining, 0, header, subHeader.Length, remaining.Length);

        // 헤더에서 데이터 길이 읽기
        // 3E: 오프셋 7~8; 4E: 오프셋 11~12
        var dataLengthOffset = is4E ? 11 : 7;
        var dataLength = header[dataLengthOffset] | (header[dataLengthOffset + 1] << 8);

        // 가변 부분 읽기: 종료 코드(2) + 응답 데이터(N) → dataLength 바이트 → dataLength * 2 ASCII 문자
        var dataAsciiLen = dataLength * 2;
        var dataBuf = new Byte[dataAsciiLen];
        ReadFully(_stream, dataBuf, 0, dataAsciiLen);
        var dataHex = System.Text.Encoding.ASCII.GetString(dataBuf);
        var data = dataHex.ToHex();

        // 결합하여 해독
        var all = new Byte[header.Length + data.Length];
        Array.Copy(header, 0, all, 0, header.Length);
        Array.Copy(data, 0, all, header.Length, data.Length);

        if (span != null) span.Tag += Environment.NewLine + all.ToHex("-", 64);
        Log?.Debug("{0}<= {1}", Address, all.ToHex("-", 64));

        var response = new MCResponse { DataFormat = DataFormat };
        response.Read(new MemoryStream(all), null);
        return response;
    }

    private static void ReadFully(Stream stream, Byte[] buffer, Int32 offset, Int32 count)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var read = stream.Read(buffer, offset + totalRead, count - totalRead);
            if (read == 0) throw new IOException("TCP connection closed, cannot read MC protocol response");
            totalRead += read;
        }
    }

    #endregion

    #region 로그

    /// <summary>로그</summary>
    protected void WriteLog(String format, params Object[] args)
    {
        if (Log != null && Log != Logger.Null)
            Log.Info(format, args);
    }

    #endregion
}