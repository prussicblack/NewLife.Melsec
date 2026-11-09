using System.ComponentModel;
using System.Diagnostics;
using NewLife.IoT;
using NewLife.IoT.Drivers;
using NewLife.IoT.ThingModels;
using NewLife.IoT.ThingSpecification;
using NewLife.Log;
using NewLife.Melsec.Protocols;
using NewLife.Reflection;
using NewLife.Serialization;

namespace NewLife.Melsec.Drivers;

/// <summary>
/// 미쓰비시 PLC 드라이버
/// </summary>
[Driver("MelsecFxLinks")]
[DisplayName("Mitsubishi FxLinks")]
public class FxLinksDriver : DriverBase
{
    /// <summary>링크</summary>
    public FxLinks Link { get; set; }

    /// <summary>
    /// 오픈된 채널 수
    /// </summary>
    private Int32 _nodes;

    /// <summary>
    /// 드라이버 매개변수 객체 생성. 해당 프로토콜의 매개변수 템플릿으로 Xml/Json 직렬화 가능
    /// </summary>
    /// <returns></returns>
    protected override IDriverParameter OnCreateParameter() => new FxLinksParameter
    {
        PortName = "COM1",
        Baudrate = 9600,
        Host = 1,
    };

    /// <summary>
    /// 포인트에서 주소 파싱
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public virtual UInt16 GetAddress(IPoint point)
    {
        if (point == null) throw new ArgumentException("Point info cannot be empty!");

        // 콜론 이후의 비트 필드 제거
        var addr = point.Address;
        var p = addr.IndexOf(':');
        if (p > 0) addr = addr[..p];

        return (UInt16)addr.ToInt();
    }

    /// <summary>
    /// 채널 오픈. 하나의 ModbusTcp 기기는 여러 채널로 분리되어 읽을 수 있으므로, Tcp 연결을 공유하고 서로 다른 노드로 구분해야 함
    /// </summary>
    /// <param name="device">채널</param>
    /// <param name="parameter">매개변수</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns></returns>
    public override Task<INode> OpenAsync(IDevice device, IDriverParameter? parameter, CancellationToken cancellationToken = default)
    {
        using var span = Tracer?.NewSpan("fxlinks:parameter", parameter.ToJson());

        var p = parameter as FxLinksParameter;
        if (p == null) throw new ArgumentException($"Invalid parameter: {parameter.ToJson()}");

        if (p.Baudrate <= 0) p.Baudrate = 9600;

        var node = new MelsecNode
        {
            Address = p.PortName,
            Host = p.Host,

            Driver = this,
            Device = device,
            Parameter = p,
        };

        // 인스턴스화
        if (Link == null)
        {
            lock (this)
            {
                if (Link == null)
                {
                    var link = new FxLinks
                    {
                        PortName = p.PortName,
                        Baudrate = p.Baudrate,
                        DataBits = p.DataBits,
                        Parity = p.Parity,
                        StopBits = p.StopBits,

                        Log = Log,
                        Tracer = Tracer,
                    };

                    if (p.Timeout > 0) link.Timeout = p.Timeout;

                    //if (Log != null && Log.Level <= LogLevel.Debug) link.Log = Log;

                    // 외부에서 채널이 지정된 경우 연결 오픈
                    if (device != null) link.Open();

                    Link = link;
                }
            }
        }

        Interlocked.Increment(ref _nodes);

        return TaskEx.FromResult(node as INode);
    }

    /// <summary>
    /// 기기 드라이버 닫기
    /// </summary>
    /// <param name="node"></param>
    /// <param name="cancellationToken">취소 토큰</param>
    public override Task CloseAsync(INode node, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Decrement(ref _nodes) <= 0)
        {
            Link.TryDispose();
            Link = null;
        }
        return TaskEx.CompletedTask;
    }

    /// <summary>
    /// 데이터 읽기
    /// </summary>
    /// <param name="node">노드 객체. 스테이션 번호 등의 정보를 저장할 수 있으며, 드라이버에서만 인식</param>
    /// <param name="points">포인트 집합. Address 속성 주소 예: D100, C100, W100, H100</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns></returns>
    public override Task<ReadResult> ReadAsync(INode node, IPoint[] points, CancellationToken cancellationToken = default)
    {
        if (points == null || points.Length == 0)
            return TaskEx.FromResult(new ReadResult { IsSuccess = true, Points = [], Values = [] });

        var n = node as MelsecNode;
        var p = node.Parameter as FxLinksParameter;

        // 여러 조각을 조합하여 읽기 횟수 감소
        var list = BuildSegments(points, p);

        // 잠금으로 충돌 방지
        lock (Link)
        {
            // 세그먼트별 전체 읽기
            for (var i = 0; i < list.Count; i++)
            {
                var seg = list[i];

                // 항목 중 하나에서 읽기 오류가 발생하면 그냥 건너뛰고 다른 벌크에 영향을 주지 않도록 함
                try
                {
                    // 포인트 주소 타입에 따라 다른 조작을 사용. 워드 읽기로 비트 저장 영역을 읽을 수도 있지만 혼동을 유발하기 쉽고 해독이 어려워서 지원하지 않음
                    if (seg.Code.EqualIgnoreCase("X", "Y", "M"))
                        seg.Bits = Link.ReadBit(n.Host, seg.Code + seg.Address, (Byte)seg.Count);
                    else if (seg.Code.EqualIgnoreCase("D"))
                        seg.Values = Link.ReadWord(n.Host, seg.Code + seg.Address, (Byte)seg.Count);
                    else
                        throw new NotSupportedException($"{seg.Code}{seg.Address} is unkown address");
                }
                catch (Exception ex)
                {
                    Log?.Error(ex.ToString());
                }

                // 읽기 시 약간 지연
                if (i < list.Count - 1 && p.BatchDelay > 0) Thread.Sleep(p.BatchDelay);
            }
        }

        // 데이터 분할
        var dic = Dispatch(points, list);

        var resultPoints = new IPoint[dic.Count];
        var resultValues = new Object?[dic.Count];
        var idx = 0;
        foreach (var kv in dic)
        {
            var pt = points.FirstOrDefault(e => e.Name == kv.Key);
            resultPoints[idx] = pt;
            resultValues[idx] = kv.Value;
            idx++;
        }

        return TaskEx.FromResult(new ReadResult
        {
            IsSuccess = true,
            Points = resultPoints,
            Values = resultValues,
        });
    }

    internal IList<Segment> BuildSegments(IList<IPoint> points, FxLinksParameter p)
    {
        // 여러 조각을 조합하여 읽기 횟수 감소
        var list = new List<Segment>();
        foreach (var point in points)
        {
            var cmd = point.Address[..1];
            var addr = point.Address[1..].ToInt();

            list.Add(new Segment
            {
                Code = cmd,
                Address = addr,
                Count = 1
            });
        }
        list = list.OrderBy(e => e.Code).ThenBy(e => e.Address).ThenByDescending(e => e.Count).ToList();

        var step = p.BatchStep > 1 ? p.BatchStep : 1;
        var k = 1;
        var rs = new List<Segment>();
        var prv = list[0];
        rs.Add(prv);
        for (var i = 1; i < list.Count; i++)
        {
            var cur = list[i];

            // 이전 세그먼트의 끝이 현재 세그먼트의 시작에 닿으면 병합 가능
            var flag = prv.Address + prv.Count + step > cur.Address;
            //// 비트 저장 영역을 읽는 경우, 간격이 8 미만이면 모두 병합 가능
            //if (!flag && cur.Code.EqualIgnoreCase("X", "Y", "M"))
            //{
            //    flag = prv.Address + prv.Count + 8 > cur.Address;
            //}

            // 이전 세그먼트의 끝이 현재 세그먼트의 시작에 닿으면 병합 가능
            if (flag && prv.Code == cur.Code)
            {
                if (p.BatchSize <= 0 || k < p.BatchSize)
                {
                    // 주의: 앞뒤가 겹칠 수도 있고, 앞 영역이 뒤보다 클 수도 있음
                    var size = cur.Address + cur.Count - prv.Address;
                    if (size > prv.Count) prv.Count = size;

                    // 연속 병합 수 누적
                    k++;
                }
                else
                {
                    rs.Add(cur);

                    prv = cur;
                    k = 1;
                }
            }
            else
            {
                rs.Add(cur);

                prv = cur;
                k = 1;
            }
        }

        return rs;
    }

    internal IDictionary<String, Object> Dispatch(IPoint[] points, IList<Segment> segments)
    {
        var dic = new Dictionary<String, Object>();
        if (segments == null || segments.Count == 0) return dic;

        foreach (var point in points)
        {
            var cmd = point.Address[..1];
            var addr = point.Address[1..].ToInt();

            // 조각 찾기
            var seg = segments.FirstOrDefault(e => e.Code == cmd && e.Address <= addr && addr < e.Address + e.Count);
            if (seg != null)
            {
                var code = seg.Code;
                if (seg.Values != null)
                {
                    // 데이터 완전성 검증
                    var offset = addr - seg.Address;
                    if (seg.Values.Length > offset)
                        dic[point.Name] = seg.Values[offset];
                }
                else if (seg.Bits != null)
                {
                    // 데이터 완전성 검증
                    var offset = addr - seg.Address;
                    if (seg.Bits.Length > offset)
                        dic[point.Name] = seg.Bits[offset];
                }
                //else
                //    throw new NotSupportedException($"{code} 분할 불가");
            }
        }
        return dic;
    }

    [DebuggerDisplay("{Code}({Address}, {Count})")]
    internal class Segment
    {
        public String Code { get; set; }
        public Int32 Address { get; set; }
        public Int32 Count { get; set; }
        public Byte[] Bits { get; set; }
        public UInt16[] Values { get; set; }
    }

    /// <summary>
    /// 데이터 쓰기
    /// </summary>
    /// <param name="node">노드 객체. 스테이션 번호 등의 정보를 저장할 수 있으며, 드라이버에서만 인식</param>
    /// <param name="requests">쓰기 요청 배열, 각 항목에 대상 포인트와 값 포함</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns></returns>
    public override Task<WriteResult> WriteAsync(INode node, WriteRequest[] requests, CancellationToken cancellationToken = default)
    {
        var successCount = 0;
        foreach (var request in requests)
        {
            var point = request.Point;
            var value = request.Value;

            if (value == null || point == null || point.Address.IsNullOrEmpty()) continue;

            var n = node as MelsecNode;

            UInt16[] vs;
            if (value is Byte[] buf)
            {
                vs = new UInt16[(Int32)Math.Ceiling(buf.Length / 2d)];
                for (var i = 0; i < vs.Length; i++)
                {
                    vs[i] = buf.ToUInt16(i * 2, false);
                }
            }
            else
            {
                if (point.Address.StartsWithIgnoreCase("X", "Y", "M"))
                    vs = ConvertToBit(value, point, n.Device?.Specification);
                else
                    vs = ConvertToWord(value, point, n.Device?.Specification);

                if (vs == null) throw new NotSupportedException($"Point [{point.Name}] does not support data [{value}]");
            }

            // 잠금으로 충돌 방지
            lock (Link)
            {
                // 포인트 주소 접두사에 따라 어떤 쓰기 메서드를 사용할지 결정. 사물 모델에서 포인트가 올바르게 구성되어야 함
                if (point.Address.StartsWithIgnoreCase("X", "Y", "M"))
                    Link.WriteBit(n.Host, point.Address, vs);
                else if (point.Address.StartsWithIgnoreCase("D"))
                    Link.WriteWord(n.Host, point.Address, vs);
                else
                    Link.Write(point.Address[..1], n.Host, point.Address, vs);
            }

            successCount++;
        }

        return TaskEx.FromResult(WriteResult.SuccessBatch(successCount));
    }

    /// <summary>원시 데이터를 코일로 변환</summary>
    /// <param name="data"></param>
    /// <param name="point"></param>
    /// <param name="spec"></param>
    /// <returns></returns>
    protected virtual UInt16[] ConvertToBit(Object data, IPoint point, ThingSpec spec)
    {
        var type = TypeHelper.GetNetType(point);
        if (type == null)
        {
            // 사물 속성 정의 찾기
            var pi = spec?.Properties?.FirstOrDefault(e => e.Id.EqualIgnoreCase(point.Name));
            type = TypeHelper.GetNetType(pi?.DataType?.Type);
        }
        if (type == null) return null;

        DefaultSpan.Current?.AppendTag("ConvertToBit->" + type.FullName);

        switch (type.GetTypeCode())
        {
            case TypeCode.Boolean:
            case TypeCode.Byte:
            case TypeCode.SByte:
                return data.ToBoolean() ? new[] { (UInt16)0x01 } : new[] { (UInt16)0x00 };
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
                return data.ToInt() > 0 ? new[] { (UInt16)0x01 } : new[] { (UInt16)0x00 };
            case TypeCode.Int64:
            case TypeCode.UInt64:
                return data.ToLong() > 0 ? new[] { (UInt16)0x01 } : new[] { (UInt16)0x00 };
            default:
                return data.ToBoolean() ? new[] { (UInt16)0x01 } : new[] { (UInt16)0x00 };
        }
    }

    /// <summary>원시 데이터를 레지스터 배열로 변환</summary>
    /// <param name="data"></param>
    /// <param name="point"></param>
    /// <param name="spec"></param>
    /// <returns></returns>
    protected virtual UInt16[] ConvertToWord(Object data, IPoint point, ThingSpec spec)
    {
        var type = TypeHelper.GetNetType(point);
        if (type == null)
        {
            // 사물 속성 정의 찾기
            var pi = spec?.Properties?.FirstOrDefault(e => e.Id.EqualIgnoreCase(point.Name));
            type = TypeHelper.GetNetType(pi?.DataType?.Type);
        }
        if (type == null) return null;

        DefaultSpan.Current?.AppendTag("ConvertToWord->" + type.FullName);

        switch (type.GetTypeCode())
        {
            case TypeCode.Boolean:
            case TypeCode.Byte:
            case TypeCode.SByte:
                return data.ToBoolean() ? new[] { (UInt16)0x01 } : new[] { (UInt16)0x00 };
            case TypeCode.Int16:
            case TypeCode.UInt16:
                return new[] { (UInt16)data.ToInt() };
            case TypeCode.Int32:
            case TypeCode.UInt32:
                {
                    var n = data.ToInt();
                    return new[] { (UInt16)(n >> 16), (UInt16)(n & 0xFFFF) };
                }
            case TypeCode.Int64:
            case TypeCode.UInt64:
                {
                    var n = data.ToLong();
                    return new[] { (UInt16)(n >> 48), (UInt16)(n >> 32), (UInt16)(n >> 16), (UInt16)(n & 0xFFFF) };
                }
            case TypeCode.Single:
                {
                    var d = (Single)data.ToDouble();
                    //var n = BitConverter.SingleToInt32Bits(d);
                    var n = (UInt32)d;
                    return new[] { (UInt16)(n >> 16), (UInt16)(n & 0xFFFF) };
                }
            case TypeCode.Double:
                {
                    var d = (Double)data.ToDouble();
                    //var n = BitConverter.DoubleToInt64Bits(d);
                    var n = (UInt64)d;
                    return new[] { (UInt16)(n >> 48), (UInt16)(n >> 32), (UInt16)(n >> 16), (UInt16)(n & 0xFFFF) };
                }
            case TypeCode.Decimal:
                {
                    var d = data.ToDecimal();
                    var n = (UInt64)d;
                    return new[] { (UInt16)(n >> 48), (UInt16)(n >> 32), (UInt16)(n >> 16), (UInt16)(n & 0xFFFF) };
                }
            //case TypeCode.String:
            //    break;
            default:
                return null;
        }
    }
}