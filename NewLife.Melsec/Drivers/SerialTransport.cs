using System.IO.Ports;
using NewLife.Data;
using NewLife.Log;
using NewLife.Threading;

namespace NewLife.Net;

/// <summary>시리얼 포트 전송</summary>
/// <example>
/// 표준 예제:
/// <code>
/// var st = new SerialTransport();
/// st.PortName = "COM65";  // 통신 포트
/// st.FrameSize = 16;      // 데이터 프레임 크기
/// 
/// st.Received += (s, e) =>
/// {
///     Console.WriteLine("수신 {0}", e.ToHex());
/// };
/// // 비동기 조작 시작
/// st.Open();
/// 
/// //var buf = "01080000801A".ToHex();
/// var buf = "0111C02C".ToHex();
/// for (int i = 0; i &lt; 100; i++)
/// {
///     Console.WriteLine("송신 {0}", buf.ToHex());
///     st.Send(buf);
/// 
///     Thread.Sleep(1000);
/// }
/// </code>
/// </example>
public class SerialTransport : DisposeBase, ITransport
{
    #region 속성
    private SerialPort _Serial;
    /// <summary>시리얼 포트 객체</summary>
    public SerialPort Serial
    {
        get { return _Serial; }
        set
        {
            _Serial = value;
            if (_Serial != null)
            {
                PortName = _Serial.PortName;
                BaudRate = _Serial.BaudRate;
                Parity = _Serial.Parity;
                DataBits = _Serial.DataBits;
                StopBits = _Serial.StopBits;
            }
        }
    }

    /// <summary>포트 이름. 기본 COM1</summary>
    public String PortName { get; set; } = "COM1";

    /// <summary>보우레이트. 기본 115200</summary>
    public Int32 BaudRate { get; set; } = 115200;

    /// <summary>패리티. 기본 None</summary>
    public Parity Parity { get; set; } = Parity.None;

    /// <summary>데이터 비트. 기본 8</summary>
    public Int32 DataBits { get; set; } = 8;

    /// <summary>스톱 비트. 기본 One</summary>
    public StopBits StopBits { get; set; } = StopBits.One;

    /// <summary>타임아웃. 이 시간 동안 데이터가 수신되지 않으면 다른 프레임으로 간주. 기본 10ms</summary>
    public Int32 Timeout { get; set; } = 10;

    private String _Description;
    /// <summary>설명 정보</summary>
    public String Description
    {
        get
        {
            if (_Description == null)
            {
                var dic = GetNames();
                if (!dic.TryGetValue(PortName, out _Description))
                    _Description = "";
            }
            return _Description;
        }
    }

    ///// <summary>패킷 병합 처리 인터페이스</summary>
    //public IPacket Packet { get; set; }

    /// <summary>바이트 타임아웃. 데이터 패킷 간격, 기본 20ms</summary>
    public Int32 ByteTimeout { get; set; } = 20;
    #endregion

    #region 생성
    /// <summary>시리얼 포트 전송</summary>
    public SerialTransport()
    {
        // 일정 주기마다 시리얼 포드가 닫혔는지 확인. 시리얼 포트가 더 이상 없으면 해당 전송 포트도 닫음
        timer = new TimerX(CheckDisconnect, null, 3000, 3000) { Async = true };
    }

    /// <summary>소멸</summary>
    /// <param name="disposing"></param>
    //protected override void Dispose(Boolean disposing)
    //{
    //    base.Dispose(disposing);

    //    try
    //    {
    //        if (Serial != null) Close();
    //        if (timer != null) timer.Dispose();
    //    }
    //    catch { }
    //}
    #endregion

    #region 메서드
    /// <summary>생성 보장</summary>
    public virtual void EnsureCreate()
    {
        if (Serial == null)
        {
            Serial = new SerialPort(PortName, BaudRate, Parity, DataBits, StopBits);

            _Description = null;
        }
    }

    /// <summary>오픈</summary>
    public virtual Boolean Open()
    {
        EnsureCreate();

        if (!Serial.IsOpen)
        {
            Serial.Open();
            if (Received != null) Serial.DataReceived += DataReceived;
        }

        return true;
    }

    /// <summary>닫기</summary>
    public virtual Boolean Close()
    {
        // 닫을 때 반드시 초기화. 그렇지 않으면 속성을 변경 후 다시 열어도 속성이 바뀌지 않음
        var sp = Serial;
        if (sp != null)
        {
            Serial = null;
            if (Received != null) sp.DataReceived -= DataReceived;
            if (sp.IsOpen) sp.Close();

            OnDisconnect();
        }

        return true;
    }
    #endregion

    #region 전송
    /// <summary>데이터 쓰기</summary>
    /// <param name="pk">데이터 패킷</param>
    public virtual Int32 Send(IPacket pk)
    {
        if (!Open()) return -1;

        WriteLog("Send:{0}", pk.ToHex());

        var sp = Serial;
        lock (sp)
        {
            if (pk.TryGetArray(out var seg))
                Serial.Write(seg.Array, seg.Offset, seg.Count);
            else
                Serial.Write(pk.ReadBytes(), 0, pk.Total);
        }

        return pk.Total;
    }

    /// <summary>데이터 비동기 전송 및 응답 대기</summary>
    /// <param name="pk"></param>
    /// <returns></returns>
    public virtual async Task<IOwnerPacket> SendAsync(IPacket pk)
    {
        if (!Open()) return null;

        //if (Packet == null) Packet = new PacketProvider();

        //var task = Packet.Add(pk, null, Timeout);

        _Source = new TaskCompletionSource<IOwnerPacket>();

        if (pk != null)
        {
            WriteLog("SendAsync:{0}", pk.ToHex());

            // 데이터 전송
            if (pk.TryGetArray(out var seg))
                Serial.Write(seg.Array, seg.Offset, seg.Count);
            else
                Serial.Write(pk.ReadBytes(), 0, pk.Total);
        }

        return await _Source.Task;
    }

    /// <summary>데이터 수신</summary>
    /// <returns></returns>
    public virtual IOwnerPacket Receive()
    {
        if (!Open()) return null;

        var task = SendAsync(null);
        if (Timeout > 0 && !task.Wait(Timeout)) return null;

        return task.Result;
    }
    #endregion

    #region 비동기 수신
    void DataReceived(Object sender, SerialDataReceivedEventArgs e)
    {
        // 전송자는 반드시 일정 간격을 유지해야 하며, 각 메시지가 너무 크면 패킷 병합/분리 때문에 오류가 발생할 수 있음
        try
        {
            var sp = sender as SerialPort;
            WaitMore();
            if (sp.BytesToRead > 0)
            {
                //var buf = new Byte[sp.BytesToRead];
                var pk = new OwnerPacket(sp.BytesToRead);

                var count = sp.Read(pk.Buffer, 0, sp.BytesToRead);
                //if (count != buf.Length) buf = buf.ReadBytes(0, count);
                //var ms = new MemoryStream(buf, 0, count, false);
                //var pk = new Packet(buf, 0, count);
                pk.Resize(count);

                ProcessReceive(pk);
            }
        }
        catch (Exception ex)
        {
            //WriteLog("Error " + ex.Message);
            if (Log != null) Log.Error("DataReceived Error {0}", ex.Message);
        }
    }

    void WaitMore()
    {
        var sp = Serial;

        var ms = ByteTimeout;
        var end = DateTime.Now.AddMilliseconds(ms);
        var count = sp.BytesToRead;
        while (sp.IsOpen && end > DateTime.Now)
        {
            //Thread.SpinWait(1);
            Thread.Sleep(ms);
            if (count != sp.BytesToRead)
            {
                end = DateTime.Now.AddMilliseconds(ms);
                count = sp.BytesToRead;
            }
        }
    }

    void ProcessReceive(IOwnerPacket pk)
    {
        try
        {
            //if (Packet == null)
            OnReceive(pk);
            //else
            //{
            //    // 패킷 분해, 여러 패킷을 여러 번 처리 프로그램에 호출
            //    foreach (var msg in Packet.Parse(pk))
            //    {
            //        OnReceive(msg);
            //    }
            //}
        }
        catch (Exception ex)
        {
            if (!ex.IsDisposed()) Log.Error("{0}.OnReceive {1}", PortName, ex.Message);
        }
    }

    private TaskCompletionSource<IOwnerPacket> _Source;
    /// <summary>수신된 데이터 처리. 기본은 동기 수신 위임과 매칭</summary>
    /// <param name="pk"></param>
    internal virtual void OnReceive(IOwnerPacket pk)
    {
        //// 동기 매칭
        //if (Packet != null && Packet.Match(pk, null)) return;

        if (_Source != null)
        {
            _Source.SetResult(pk);
            _Source = null;
            return;
        }

        // 이벤트 발생
        Received?.Invoke(this, new ReceivedEventArgs { Packet = pk });
    }

    /// <summary>데이터 도착 이벤트</summary>
    public event EventHandler<ReceivedEventArgs> Received;
    #endregion

    #region 시리얼 포트 연결 끊김 자동 감지
    /// <summary>연결 끊김 시 발생. 사람이 끊은 경우도 있고, 시리얼 포트 링크가 끊긴 경우도 있음</summary>
    public event EventHandler Disconnected;

    Boolean isInEvent;
    void OnDisconnect()
    {
        if (Disconnected != null)
        {
            // 이벤트 내부 여부 판단. 외부에서 끊김 이벤트 중에 Close를 호출하여 무한 루프를 일으키지 않도록 함
            if (!isInEvent)
            {
                isInEvent = true;

                Disconnected(this, EventArgs.Empty);

                isInEvent = false;
            }
        }
    }

    TimerX timer;
    /// <summary>시리얼 포트가 끊겼는지 확인</summary>
    /// <remarks>
    /// FX 시리얼 포트 비동기 조작은 심각한 누수 결함이 있음. 외부 하드웨어가 장시간 끊겨 있으면
    /// SerialPort.IsOpen이 감지하지 못하고, 메모리를 무한정 점유한다.
    /// </remarks>
    /// <param name="state"></param>
    void CheckDisconnect(Object state)
    {
        if (String.IsNullOrEmpty(PortName) || Serial == null || !Serial.IsOpen) return;

        // 포트가 더 이상 없으면 연결을 끊자
        if (!SerialPort.GetPortNames().Contains(PortName))
        {
            WriteLog("Serial port {0} no longer exists, closing!", PortName);

            //OnDisconnect();
            Close();
        }
    }
    #endregion

    #region 헬퍼
    /// <summary>설명이 있는 시리얼 포트 이름 가져오기. 없으면 빈 배열 반환</summary>
    /// <returns></returns>
    public static String[] GetPortNames()
    {
        var list = new List<String>();
        foreach (var item in GetNames())
        {
            list.Add(item.Key);
        }
        return list.ToArray();
    }

    /// <summary>시리얼 포트 목록, 이름과 설명 가져오기</summary>
    /// <returns></returns>
    public static Dictionary<String, String> GetNames()
    {
        var dic = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        //#if NC30
        foreach (var item in SerialPort.GetPortNames())
        {
            dic.Add(item, "");
        }
        //#else
        //            using (var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM", false))
        //            using (var usb = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB", false))
        //            {
        //                if (key != null)
        //                {
        //                    foreach (var item in key.GetValueNames())
        //                    {
        //                        var name = key.GetValue(item) + "";
        //                        var des = "";

        //                        // USB 시리얼 포트 열거 시도
        //                        foreach (var vid in usb.GetSubKeyNames())
        //                        {
        //                            var usbvid = usb.OpenSubKey(vid);
        //                            foreach (var elm in usbvid.GetSubKeyNames())
        //                            {
        //                                var sub = usbvid.OpenSubKey(elm);
        //                                //if (sub.GetValue("Class") + "" == "Ports")
        //                                {
        //                                    var FriendlyName = sub.GetValue("FriendlyName") + "";
        //                                    if (FriendlyName.Contains($"({name})"))
        //                                    {
        //                                        des = FriendlyName.TrimEnd($"({name})").Trim();
        //                                        break;
        //                                    }
        //                                }
        //                            }
        //                            if (!des.IsNullOrEmpty()) break;
        //                        }

        //                        // 마지막으로 장치 매핑의 시리얼 포트 이름 선택
        //                        if (des.IsNullOrEmpty())
        //                        {
        //                            des = item;
        //                            var p = item.LastIndexOf('\\');
        //                            if (p >= 0) des = des.Substring(p + 1);
        //                        }

        //                        //dic.Add(name, des);
        //                        // 특정 머신에서 시리얼 포트 중복 발견
        //                        dic[name] = des;
        //                    }
        //                }
        //            }
        //#endif

        return dic;
    }

    /// <summary>시리얼 포트 목록에서 시리얼 포트 선택, 자동 선택 키워드 지원</summary>
    /// <param name="keyWord">시리얼 포트 이름 또는 설명자의 키워드</param>
    /// <returns></returns>
    public static SerialTransport Choose(String keyWord = null)
    {
        var ns = GetNames();
        if (ns.Count == 0)
        {
            Console.WriteLine("No available serial ports!");
            return null;
        }

        var name = "";
        var des = "";

        Console.WriteLine("Available serial ports:");
        Console.ForegroundColor = ConsoleColor.Green;
        foreach (var item in ns)
        {
            if (item.Value == "Serial0") continue;

            if (keyWord != null && (item.Key.EqualIgnoreCase(keyWord) || item.Value.Contains(keyWord)))
            {
                name = item.Key;
                des = item.Value;
            }

            //Console.WriteLine(item);
            Console.WriteLine("{0,5}({1})", item.Key, item.Value);
        }
        // 자동 선택이 없으면 마지막 항목을 기본으로
        if (name.IsNullOrEmpty())
        {
            var item = ns.Last();
            name = item.Key;
            des = item.Value;
        }
        while (true)
        {
            Console.ResetColor();
            Console.Write("Enter serial port name (default ");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("{0}", name);
            Console.ResetColor();
            Console.Write("): ");

            var str = Console.ReadLine();
            if (str.IsNullOrEmpty()) break;

            // 유효한 시리얼 포트 이름만 입력 가능
            if (ns.ContainsKey(str))
            {
                name = str;
                des = ns[str];
                break;
            }
        }

        Console.WriteLine();
        Console.Write("Opening serial port ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("{0}({1})", name, des);

        Console.ResetColor();

        var sp = new SerialTransport
        {
            PortName = name
        };

        return sp;
    }
    #endregion

    #region 로그
    /// <summary>로그 객체</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>로그 출력</summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public void WriteLog(String format, params Object[] args)
    {
        if (Log != null && Log.Enable) Log.Info(format, args);
    }

    /// <summary>오버라이드</summary>
    /// <returns></returns>
    public override String ToString()
    {
        if (!String.IsNullOrEmpty(PortName))
            return PortName;
        else
            return "(SerialPort)";
    }


    #endregion
}