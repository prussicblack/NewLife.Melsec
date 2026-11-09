using System.Diagnostics;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.Data;
using NewLife.Log;

[assembly: InternalsVisibleTo("XUnitTest, PublicKey=00240000048000001401000006020000002400005253413100080000010001000d41eb3bdab5c2150958b46c95632b7e4dcb0af77ed8637bd8543875bc2443d01273143bb46655a48a92efa76251adc63ccca6d0e9cef2e0ce93e32b5043bea179a6c710981be4a71703a03e10960643f7df091f499cf60183ef0e4e4e2eebf26e25cea0eebf87c8a6d7f8130c283fc3f747cb90623f0aaa619825e3fcd82f267a0f4bfd26c9f2a6b5a62a6b180b4f6d1d091fce6bd60a9aa9aa5b815b833b44e0f2e58b28a354cb20f52f31bb3b3a7c54f515426537e41f9c20c07e51f9cab8abc311daac19a41bd473a51c7386f014edf1863901a5c29addc89da2f2659c9c1e95affd6997396b9680e317c493e974a813186da277ff9c1d1b30e33cb5a2f6")]

namespace NewLife.Melsec.Protocols;

/// <summary>미쓰비시 PLC 컴퓨터 링크 프로토콜</summary>
public class FxLinks : DisposeBase
{
    #region 속성
    /// <summary>이름</summary>
    public String Name { get; set; }

    /// <summary>포트</summary>
    public String PortName { get; set; }

    /// <summary>보레이트</summary>
    public Int32 Baudrate { get; set; } = 9600;

    /// <summary>데이터 비트 수. 기본 7</summary>
    public Int32 DataBits { get; set; } = 7;

    /// <summary>패리티(홀짝 체크). 기본 Even 짝수 체크</summary>
    public Parity Parity { get; set; } = Parity.Even;

    /// <summary>스톱 비트. 기본 One</summary>
    public StopBits StopBits { get; set; } = StopBits.One;

    /// <summary>버퍼 크기. 기본 256</summary>
    public Int32 BufferSize { get; set; } = 256;

    /// <summary>네트워크 타임아웃. 요청 전송 후 응답 대기 시간, 기본 3000ms</summary>
    public Int32 Timeout { get; set; } = 3000;

    /// <summary>성능 트래커</summary>
    public ITracer Tracer { get; set; }

    private SerialPort _port;
    #endregion

    #region 생성
    /// <summary>인스턴스 생성</summary>
    public FxLinks() => Name = GetType().Name;

    /// <summary>소멸</summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        Close();
    }
    #endregion

    #region 메서드
    /// <summary>커넥션 열기</summary>
    public void Open()
    {
        if (_port == null)
        {
            var p = new SerialPort(PortName, Baudrate)
            {
                DataBits = DataBits,
                Parity = Parity,
                StopBits = StopBits,

                ReadTimeout = Timeout,
                WriteTimeout = Timeout
            };
            //if (DataBits > 0) p.DataBits = DataBits;
            //if (Parity > 0) p.Parity = Parity;
            //if (StopBits > 0) p.StopBits = StopBits;
            p.Open();
            _port = p;

            WriteLog("FxLinks.Open {0} Baudrate={1} DataBits={2} Parity={3} StopBits={4}", PortName, Baudrate, p.DataBits, p.Parity, p.StopBits);
        }
    }

    /// <summary>커넥션 닫기</summary>
    public void Close()
    {
        _port.TryDispose();
        _port = null;
    }

    /// <summary>명령 전송 및 응답 수신</summary>
    /// <param name="command">기능 코드</param>
    /// <param name="host">호스트. 보통 1</param>
    /// <param name="address">주소. 예: 0x0002</param>
    /// <param name="data">데이터</param>
    /// <returns>응답 메시지의 페이로드 부분 반환</returns>
    public virtual FxLinksResponse SendCommand(String command, Byte host, String address, String data)
    {
        var msg = new FxLinksMessage
        {
            Code = ControlCodes.ENQ,
            Station = host,
            PLC = 0xFF,
            Command = command,
            Address = address,

            Payload = data
        };

        var rs = SendCommand(msg);

        return rs;
    }

    /// <summary>메시지 전송 및 응답 수신</summary>
    /// <param name="message">Modbus 메시지</param>
    /// <returns></returns>
    internal protected virtual FxLinksResponse SendCommand(FxLinksMessage message)
    {
        Open();

        // 버퍼 초기화
        _port.DiscardInBuffer();

        Log?.Debug("=> {0}", message);

        var cmd = message.ToPacket();
        var buf = cmd.ToArray();

        using var span = Tracer?.NewSpan("fxlinks:SendCommand", buf.ToHex("-"));

        Log?.Debug("{0}=> {1} ({2})", PortName, buf.ToHex("-"), FxLinksMessage.GetHex(buf));

        _port.Write(buf, 0, buf.Length);

        //Thread.Sleep(ByteTimeout);

        // 시리얼 포트는 속도가 느리므로 데이터 수신 완료 대기
        WaitMore(_port, 1 + 1 + 2);

        //using var span = Tracer?.NewSpan("fxlinks:ReceiveCommand");
        buf = new Byte[BufferSize];
        try
        {
            var count = _port.Read(buf, 0, buf.Length);
            IPacket pk = new ArrayPacket(buf, 0, count);
            Log?.Debug("{0}<= {1} ({2})", PortName, pk.ToHex(32, "-"), FxLinksMessage.GetHex(pk.ReadBytes()));

            if (span != null) span.Tag += Environment.NewLine + pk.ToHex(64, "-");

            var len = pk.Total - 2;
            if (len < 2) return null;

            var rs = message.CreateReply();
            if (!rs.Read(pk.GetStream(), message)) return null;

            // 체크섬 검증
            if (rs.CheckSum != rs.CheckSum2) WriteLog("CheckSum Error {0:X2}!={1:X2} !", rs.CheckSum, rs.CheckSum2);

            Log?.Debug("<= {0}", rs);

            // 기능 코드 확인
            if (rs.Code == ControlCodes.NAK)
            {
                var str = rs.Payload;
                var code = str != null && str.Length >= 2 ? str.ToByte(0) : 0;
                throw new FxLinksException((ErrorCodes)code, $"{message} occure error");
            }

            return rs;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            if (ex is TimeoutException) return null;
            throw;
        }
    }

    private void WaitMore(SerialPort sp, Int32 minLength)
    {
        var count = sp.BytesToRead;
        if (count >= minLength) return;

        var ms = Timeout;
        var sw = Stopwatch.StartNew();
        while (sp.IsOpen && sw.ElapsedMilliseconds < ms)
        {
            //Thread.SpinWait(1);
            Thread.Sleep(10);
            if (count != sp.BytesToRead)
            {
                count = sp.BytesToRead;
                if (count >= minLength) break;

                sw.Restart();
            }
        }
    }
    #endregion

    #region 읽기
    /// <summary>기능 코드별 읽기. IoT 표준 라이브러리용</summary>
    /// <param name="command">기능 코드</param>
    /// <param name="host">호스트</param>
    /// <param name="address">논리 주소</param>
    /// <param name="count">개수. 레지스터 개수 또는 코일 개수</param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public virtual Object Read(String command, Byte host, String address, Byte count)
    {
        switch (command)
        {
            case "BR": return ReadBit(host, address, count);
            case "WR": return ReadWord(host, address, count);
            default:
                break;
        }

        throw new NotSupportedException($"FxLinksRead does not support [{command}]");
    }

    /// <summary>비트 단위 읽기, BR</summary>
    /// <param name="host">호스트. 보통 1</param>
    /// <param name="address">주소. 예: 0x0002</param>
    /// <param name="count">코일 수. 보통 8의 배수 요구</param>
    /// <returns>코일 상태 바이트 배열</returns>
    public virtual Byte[] ReadBit(Byte host, String address, Byte count)
    {
        using var span = Tracer?.NewSpan("fxlinks:ReadBit", $"host={host} address={address} count={count}");
        try
        {
            var rs = SendCommand("BR", host, address, count.ToHexChars());
            if (rs == null || rs.Payload.IsNullOrEmpty()) return null;

            var result = rs.Payload.ToBytes();

            span?.AppendTag(result.Join(","));

            return result;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>워드 단위 읽기, WR</summary>
    /// <param name="host">호스트. 보통 1</param>
    /// <param name="address">주소. 예: 0x0002</param>
    /// <param name="count">입력 수. 보통 8의 배수 요구</param>
    /// <returns>입력 상태 바이트 배열</returns>
    public virtual UInt16[] ReadWord(Byte host, String address, Byte count)
    {
        using var span = Tracer?.NewSpan("fxlinks:ReadWord", $"host={host} address={address} count={count}");
        try
        {
            var rs = SendCommand("WR", host, address, count.ToHexChars());
            if (rs == null || rs.Payload.IsNullOrEmpty()) return null;

            var str = rs.Payload;
            var us = new UInt16[str.Length / 4];
            for (var i = 0; i < us.Length; i++)
            {
                us[i] = str.Substring(i * 4, 4).ToHex().ToUInt16(0, false);
            }

            span?.AppendTag(us);

            return us;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }
    #endregion

    #region 쓰기
    /// <summary>기능 코드별 쓰기. IoT 표준 라이브러리용</summary>
    /// <param name="command">기능 코드</param>
    /// <param name="host">호스트</param>
    /// <param name="address">논리 주소</param>
    /// <param name="values">쓰기할 수치</param>
    /// <returns></returns>
    public virtual Object Write(String command, Byte host, String address, UInt16[] values)
    {
        switch (command)
        {
            case "BW": return WriteBit(host, address, values);
            case "WW": return WriteWord(host, address, values);
            default:
                break;
        }

        throw new NotSupportedException($"FxLinksWrite does not support [{command}]");
    }

    /// <summary>비트 단위 쓰기, BW</summary>
    /// <param name="host">호스트. 보통 1</param>
    /// <param name="address">주소. 예: 0x0002</param>
    /// <param name="values">출력값. 보통 0xFF00/0x0000</param>
    /// <returns>출력값</returns>
    public Int32 WriteBit(Byte host, String address, params UInt16[] values)
    {
        using var span = Tracer?.NewSpan("fxlinks:WriteBit", $"host={host} address={address} value=({values.Join(",")})");
        try
        {
            // 1바이트(2자)의 포인트 수
            // 이후 각 포인트당 1자
            var sb = new StringBuilder();
            sb.Append(((Byte)values.Length).ToHexChars());
            for (var i = 0; i < values.Length; i++)
            {
                sb.Append(values[i] != 0 ? '1' : '0');
            }

            var rs = SendCommand("BW", host, address, sb.ToString());
            if (rs == null) return -1;
            if (rs.Code == ControlCodes.NAK) throw new Exception($"WriteBit({address}, {values.Join(",")}) get {rs.Code}");
            if (rs.Code != ControlCodes.ACK) return -1;

            return values.Length;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>워드 단위 쓰기, WW</summary>
    /// <param name="host">호스트. 보통 1</param>
    /// <param name="address">주소. 예: 0x0002</param>
    /// <param name="values">수치</param>
    /// <returns>레지스터 값</returns>
    public Int32 WriteWord(Byte host, String address, params UInt16[] values)
    {
        using var span = Tracer?.NewSpan("fxlinks:WriteWord", $"host={host} address={address} value=({values.Join(",")})");
        try
        {
            // 1바이트(2자)의 포인트 수
            // 이후 각 포인트당 4자
            var sb = new StringBuilder();
            sb.Append(((Byte)values.Length).ToHexChars());
            for (var i = 0; i < values.Length; i++)
            {
                sb.Append(values[i].ToString("X4"));
            }

            var rs = SendCommand("WW", host, address, sb.ToString());
            if (rs == null) return -1;
            if (rs.Code == ControlCodes.NAK) throw new Exception($"WriteWord({address}, {values.Join(",")}) get {rs.Code}");
            if (rs.Code != ControlCodes.ACK) return -1;

            return values.Length;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }
    #endregion

    #region 로그
    /// <summary>로그</summary>
    public ILog Log { get; set; }

    /// <summary>로그 기록</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion
}