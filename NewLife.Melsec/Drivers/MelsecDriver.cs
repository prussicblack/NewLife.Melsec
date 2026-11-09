using System.ComponentModel;
using System.IO.Ports;
using HslCommunication.Core;
using HslCommunication.Profinet.Melsec;
using NewLife.IoT;
using NewLife.IoT.Drivers;
using NewLife.IoT.ThingModels;
using NewLife.Log;
using NewLife.Serialization;

namespace NewLife.Melsec.Drivers;

/// <summary>
/// 미쓰비시 PLC 드라이버
/// </summary>
[Driver("MelsecPLC")]
[DisplayName("Mitsubishi PLC")]
public class MelsecDriver : DriverBase
{
    private IReadWriteNet _plcNet;

    /// <summary>
    /// 오픈된 채널 수
    /// </summary>
    private Int32 _nodes;

    /// <summary>
    /// 드라이버 매개변수 객체 생성. 해당 프로토콜의 매개변수 템플릿으로 Xml/Json 직렬화 가능
    /// </summary>
    /// <returns></returns>
    public override IDriverParameter GetDefaultParameter() => new MelsecParameter
    {
        Address = "127.0.0.1:6000",
        DataFormat = "CDAB",
        Protocol = Protocol.MCQna3E
    };

    /// <summary>
    /// 포인트에서 주소 파싱
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public virtual String GetAddress(IPoint point)
    {
        if (point == null) throw new ArgumentException("Point info cannot be empty!");

        // 콜론 이후의 비트 필드 제거
        var addr = point.Address;
        var p = addr.IndexOf(':');
        if (p > 0) addr = addr.Substring(0, p);

        return addr;
    }

    /// <summary>
    /// 채널 오픈. 하나의 ModbusTcp 기기는 여러 채널로 분리되어 읽을 수 있으므로, Tcp 연결을 공유하고 서로 다른 노드로 구분해야 함
    /// </summary>
    /// <param name="device">채널</param>
    /// <param name="parameters">매개변수</param>
    /// <returns></returns>
    public override INode Open(IDevice device, IDictionary<String, Object> parameters)
    {
        var pm = JsonHelper.Convert<MelsecParameter>(parameters);

        if (pm == null) throw new ArgumentException($"Invalid parameter: {parameters.ToJson()}");

        MelsecNode node;
        String ipAddress;
        Int32 port;

        if (pm.Protocol == Protocol.MCQna3E)
        {
            var address = pm.Address;
            if (address.IsNullOrEmpty()) throw new ArgumentException("Address is not specified in parameter");

            var p = address.IndexOf(':');
            if (p < 0) throw new ArgumentException($"Invalid address format in parameter: {address}");



            node = new MelsecNode
            {
                Address = address,

                Driver = this,
                Device = device,
                Parameter = pm,
            };
        }
        else
        {
            if (pm.PortName.IsNullOrEmpty()) throw new ArgumentException("Serial port name is not specified in parameter");

            node = new MelsecNode
            {
                Address = pm.PortName,

                Driver = this,
                Device = device,
                Parameter = pm,
            };
        }



        if (_plcNet == null)
        {
            lock (this)
            {
                if (_plcNet == null)
                {
                    if (pm.Protocol == Protocol.MCQna3E)
                    {
                        var address = pm.Address;
                        var p = address.IndexOf(':');

                        ipAddress = address.Substring(0, p);
                        port = address.Substring(p + 1).ToInt();

                        //MelsecA3CNet
                        var plcNet = new MelsecMcNet
                        {
                            ConnectTimeOut = 3000,
                            IpAddress = ipAddress,
                            Port = port,
                        };

                        _plcNet = plcNet;

                        if (!pm.DataFormat.IsNullOrEmpty() && Enum.TryParse<DataFormat>(pm.DataFormat, out var format))
                        {
                            plcNet.ByteTransform.DataFormat = format;
                        }

                        var connect = plcNet.ConnectServer();

                        if (!connect.IsSuccess) throw new Exception($"Connection failed: {connect.Message}");
                    }
                    else
                    {
                        var melsecSerial = new MelsecFxLinks();
                        _plcNet = melsecSerial;

                        var baudRate = 9600;
                        var dataBits = 7;
                        var stopBits = 1;
                        var parity = 2;
                        byte station = 0;

                        melsecSerial.SerialPortInni(sp =>
                        {
                            sp.PortName = pm.PortName;
                            sp.BaudRate = pm.Baudrate;
                            sp.DataBits = 7;// dataBits;
                            sp.StopBits = StopBits.One;
                            sp.Parity = Parity.Even;
                        });
                        melsecSerial.Station = station;
                        melsecSerial.WaittingTime = 0;
                        melsecSerial.SumCheck = true;
                        melsecSerial.Format = 1;

                        var connect = melsecSerial.Open();
                        if (!connect.IsSuccess) throw new Exception($"Connection failed: {connect.Message}");
                    }

                }
            }
        }

        Interlocked.Increment(ref _nodes);

        return node;
    }

    /// <summary>
    /// 기기 드라이버 닫기
    /// </summary>
    /// <param name="node"></param>
    public override void Close(INode node)
    {
        if (Interlocked.Decrement(ref _nodes) <= 0)
        {
            if (_plcNet is MelsecMcNet plcNet) plcNet?.ConnectClose();
            _plcNet.TryDispose();
            _plcNet = null;
        }
    }

    /// <summary>
    /// 데이터 읽기
    /// </summary>
    /// <param name="node">노드 객체. 스테이션 번호 등의 정보를 저장할 수 있으며, 드라이버에서만 인식</param>
    /// <param name="points">포인트 집합. Address 속성 주소 예: D100, C100, W100, H100</param>
    /// <returns></returns>
    public override IDictionary<String, Object> Read(INode node, IPoint[] points)
    {
        var dic = new Dictionary<String, Object>();

        if (points == null || points.Length == 0) return dic;

        foreach (var point in points)
        {
            var name = point.Name;
            var addr = GetAddress(point);
            var length = point.Length;
            var data = _plcNet.Read(addr, (UInt16)(length / 2));

            if (!data.IsSuccess)
            {
                var r = _plcNet.ReadUInt16(addr);
                XTrace.WriteLine($"Read data: {addr}={r.ToJson()}");
            }
            if (!data.IsSuccess) throw new Exception($"Failed to read data: {data.ToJson()}");



            dic[name] = data.Content;
        }

        return dic;
    }

    /// <summary>
    /// 데이터 쓰기
    /// </summary>
    /// <param name="node">노드 객체. 스테이션 번호 등의 정보를 저장할 수 있으며, 드라이버에서만 인식</param>
    /// <param name="point">포인트. Address 속성 주소 예: D100, C100, W100, H100</param>
    /// <param name="value">데이터</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public override Object Write(INode node, IPoint point, Object value)
    {
        var addr = GetAddress(point);
        var res = value switch
        {
            Int32 v1 => _plcNet.Write(addr, v1),
            String v2 => _plcNet.Write(addr, v2),
            Boolean v3 => _plcNet.Write(addr, v3),
            Byte[] v4 => _plcNet.Write(addr, v4),
            Byte v5 => _plcNet.Write(addr, v5),
            _ => throw new ArgumentException("Writing this data type is not supported yet!"),
        };
        return res;
    }