using System.ComponentModel;
using System.Diagnostics;
using NewLife.IoT;
using NewLife.IoT.Drivers;
using NewLife.IoT.ThingModels;
using NewLife.IoT.ThingSpecification;
using NewLife.Melsec.Protocols;
using NewLife.Reflection;
using NewLife.Serialization;

namespace NewLife.Melsec.Drivers;

/// <summary>미쓰비시 PLC MC 프로토콜 드라이버 (이더넷 3E 프레임, 바이너리 모드)</summary>
/// <remarks>
/// TCP 상시 연결로 미쓰비시 Q 시리즈, iQ-R 시리즈 및 이더넷 포트가 있는 FX3U/FX5U PLC와 통신한다.
/// IDriver 인터페이스를 구현하여 NewLife.IoT 플랫폼 및 ZeroIoT/IoTEdge 게이트웨이에 시ーム리스하게 연동.
/// </remarks>
[Driver("MelsecMC")]
[DisplayName("三菱MC以太网")]
public class MCDriver : DriverBase
{
    #region 속성

    /// <summary>MC 프로토콜 링크</summary>
    public MCProtocol Link { get; set; }

    private Int32 _nodes;

    #endregion

    #region IDriver 인터페이스

    /// <summary>드라이버 매개변수 객체 생성</summary>
    protected override IDriverParameter OnCreateParameter() => new MCParameter
    {
        Address = "192.168.1.10:6000",
        Timeout = 5000,
    };

    /// <summary>채널 오픈. 하나의 물리 PLC에 여러 로직 기기(노드)를 마운트할 수 있으며, 동일한 TCP 연결을 공유</summary>
    /// <param name="device">로직 기기</param>
    /// <param name="parameter">매개변수</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>노드 객체</returns>
    public override Task<INode> OpenAsync(IDevice device, IDriverParameter? parameter, CancellationToken cancellationToken = default)
    {
        using var span = Tracer?.NewSpan("mc:Open", parameter.ToJson());

        var p = parameter as MCParameter;
        if (p == null) throw new ArgumentException($"参数不合法：{parameter.ToJson()}");

        var node = new MelsecNode
        {
            Address = p.Address,
            Host = p.NetworkNo,

            Driver = this,
            Device = device,
            Parameter = p,
        };

        if (Link == null)
        {
            lock (this)
            {
                if (Link == null)
                {
                    var link = new MCProtocol
                    {
                        Address = p.Address,
                        FrameType = p.FrameType,
                        NetworkNo = p.NetworkNo,
                        DataFormat = p.DataFormat,
                        Log = Log,
                        Tracer = Tracer,
                    };

                    if (p.Timeout > 0) link.Timeout = p.Timeout;

                    if (device != null) link.Open();

                    Link = link;
                }
            }
        }

        Interlocked.Increment(ref _nodes);

        return TaskEx.FromResult(node as INode);
    }

    /// <summary>채널 닫기</summary>
    /// <param name="node">노드 객체</param>
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

    /// <summary>데이터 읽기</summary>
    /// <param name="node">노드 객체</param>
    /// <param name="points">포인트 집합. 주소 형식 예: D100, M200, X1F, Y2A, B100</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>읽기 결과</returns>
    public override Task<ReadResult> ReadAsync(INode node, IPoint[] points, CancellationToken cancellationToken = default)
    {
        if (points == null || points.Length == 0)
            return TaskEx.FromResult(new ReadResult { IsSuccess = true, Points = [], Values = [] });

        var n = node as MelsecNode;
        var p = node.Parameter as MCParameter;

        var list = BuildSegments(points, p);

        lock (Link)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var seg = list[i];
                try
                {
                    if (DeviceCodeHelper.IsBitDevice(seg.Code))
                        seg.Bits = Link.ReadBits(seg.Code, seg.StartAddress, seg.Count);
                    else
                        seg.Words = Link.ReadWords(seg.Code, seg.StartAddress, seg.Count);
                }
                catch (Exception ex)
                {
                    Log?.Error(ex.ToString());
                }

                if (i < list.Count - 1 && p.BatchDelay > 0) Thread.Sleep(p.BatchDelay);
            }
        }

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

    /// <summary>데이터 쓰기</summary>
    /// <param name="node">노드 객체</param>
    /// <param name="requests">쓰기 요청 배열, 각 항목에 대상 포인트와 값 포함</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>벌크 쓰기 결과</returns>
    public override Task<WriteResult> WriteAsync(INode node, WriteRequest[] requests, CancellationToken cancellationToken = default)
    {
        var successCount = 0;
        foreach (var request in requests)
        {
            var point = request.Point;
            var value = request.Value;

            if (value == null || point == null || point.Address.IsNullOrEmpty()) continue;

            var n = node as MelsecNode;
            var (devCode, addr) = DeviceCodeHelper.ParseAddress(point.Address);

            lock (Link)
            {
                if (DeviceCodeHelper.IsBitDevice(devCode))
                {
                    var bitVal = value.ToBoolean();
                    Link.WriteBits(devCode, addr, [bitVal]);
                }
                else
                {
                    var words = ConvertToWords(value, point, n.Device?.Specification);
                    if (words == null) throw new NotSupportedException($"点位[{point.Name}]不支持数据[{value}]");
                    Link.WriteWords(devCode, addr, words);
                }
            }

            successCount++;
        }

        return TaskEx.FromResult(WriteResult.SuccessBatch(successCount));
    }

    #endregion

    #region 벌크 읽기 최적화

    /// <summary>벌크 읽기 세그먼트 구성 (소프트 요소 코드별 그룹핑 + 주소 정렬 + 구간 병합)</summary>
    /// <param name="points">포인트 집합</param>
    /// <param name="p">드라이버 매개변수</param>
    internal IList<MCSegment> BuildSegments(IList<IPoint> points, MCParameter p)
    {
        var list = new List<MCSegment>(points.Count);
        foreach (var point in points)
        {
            var (code, address) = DeviceCodeHelper.ParseAddress(point.Address);
            list.Add(new MCSegment { Code = code, StartAddress = address, Count = 1 });
        }

        // 소프트 요소 코드 + 주소 기준 정렬
        list = [.. list.OrderBy(e => e.Code).ThenBy(e => e.StartAddress)];

        var step = p.BatchStep > 1 ? p.BatchStep : 1;
        var k = 1;
        var rs = new List<MCSegment>();
        var prv = list[0];
        rs.Add(prv);

        for (var i = 1; i < list.Count; i++)
        {
            var cur = list[i];
            var canMerge = prv.Code == cur.Code &&
                           prv.StartAddress + prv.Count + step > cur.StartAddress;

            if (canMerge)
            {
                if (p.BatchSize <= 0 || k < p.BatchSize)
                {
                    var newSize = cur.StartAddress + cur.Count - prv.StartAddress;
                    if (newSize > prv.Count) prv.Count = newSize;
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

    /// <summary>병합된 벌크 읽기 결과를 각 포인트에 분배</summary>
    /// <param name="points">포인트 집합</param>
    /// <param name="segments">세그먼트 결과</param>
    internal IDictionary<String, Object> Dispatch(IPoint[] points, IList<MCSegment> segments)
    {
        var dic = new Dictionary<String, Object>();
        if (segments == null || segments.Count == 0) return dic;

        foreach (var point in points)
        {
            var (code, address) = DeviceCodeHelper.ParseAddress(point.Address);
            var seg = segments.FirstOrDefault(e =>
                e.Code == code &&
                e.StartAddress <= address && address < e.StartAddress + e.Count);

            if (seg == null) continue;

            var offset = address - seg.StartAddress;
            if (seg.Words != null && seg.Words.Length > offset)
                dic[point.Name] = seg.Words[offset];
            else if (seg.Bits != null && seg.Bits.Length > offset)
                dic[point.Name] = seg.Bits[offset];
        }

        return dic;
    }

    #endregion

    #region 데이터 변환

    /// <summary>포인트 값을 워드 소프트 요소 배열로 변환 (Little-Endian)</summary>
    protected virtual UInt16[] ConvertToWords(Object data, IPoint point, ThingSpec spec)
    {
        var type = TypeHelper.GetNetType(point);
        if (type == null)
        {
            var pi = spec?.Properties?.FirstOrDefault(e => e.Id.EqualIgnoreCase(point.Name));
            type = TypeHelper.GetNetType(pi?.DataType?.Type);
        }
        if (type == null) return null;

        switch (type.GetTypeCode())
        {
            case TypeCode.Boolean:
                return data.ToBoolean() ? [(UInt16)1] : [(UInt16)0];
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
                return [(UInt16)(data.ToInt() & 0xFFFF)];
            case TypeCode.Int32:
            case TypeCode.UInt32:
            {
                var v = (UInt32)data.ToInt();
                return [(UInt16)(v & 0xFFFF), (UInt16)(v >> 16)];
            }
            case TypeCode.Int64:
            case TypeCode.UInt64:
            {
                var v = (UInt64)data.ToLong();
                return [(UInt16)(v & 0xFFFF), (UInt16)((v >> 16) & 0xFFFF),
                        (UInt16)((v >> 32) & 0xFFFF), (UInt16)(v >> 48)];
            }
            case TypeCode.Single:
            {
                var bytes = BitConverter.GetBytes((Single)Convert.ChangeType(data, typeof(Single)));
                return [(UInt16)(bytes[0] | (bytes[1] << 8)), (UInt16)(bytes[2] | (bytes[3] << 8))];
            }
            case TypeCode.Double:
            {
                var bytes = BitConverter.GetBytes((Double)Convert.ChangeType(data, typeof(Double)));
                return [
                    (UInt16)(bytes[0] | (bytes[1] << 8)), (UInt16)(bytes[2] | (bytes[3] << 8)),
                    (UInt16)(bytes[4] | (bytes[5] << 8)), (UInt16)(bytes[6] | (bytes[7] << 8))];
            }
            default:
                return [(UInt16)(data.ToInt() & 0xFFFF)];
        }
    }

    #endregion

    #region 내부 타입

    /// <summary>MC 벌크 읽기 세그먼트</summary>
    [DebuggerDisplay("{Code}(start={StartAddress}, count={Count})")]
    public class MCSegment
    {
        /// <summary>소프트 요소 코드</summary>
        public DeviceCode Code { get; set; }

        /// <summary>시작 주소</summary>
        public Int32 StartAddress { get; set; }

        /// <summary>포인트 수</summary>
        public Int32 Count { get; set; }

        /// <summary>워드 읽기 결과</summary>
        public UInt16[] Words { get; set; }

        /// <summary>비트 읽기 결과</summary>
        public Boolean[] Bits { get; set; }
    }

    #endregion

    #region 태그 이름 접근 (Tag)

    /// <summary>태그 이름 → 주소 매핑 사전. Key=태그 이름, Value=주소 문자열 (예: D100, M200)</summary>
    public IDictionary<String, String> TagMap { get; set; } = new Dictionary<String, String>();

    /// <summary>태그 매핑 설정</summary>
    /// <param name="tag">태그 이름</param>
    /// <param name="address">주소 문자열 (예: D100, M200)</param>
    public void SetTag(String tag, String address)
    {
        TagMap[tag] = address;
    }

    /// <summary>태그 매핑 벌크 설정</summary>
    /// <param name="tags">태그 이름 → 주소 매핑</param>
    public void SetTags(IDictionary<String, String> tags)
    {
        foreach (var kv in tags)
        {
            TagMap[kv.Key] = kv.Value;
        }
    }

    /// <summary>태그 이름으로 데이터 읽기</summary>
    /// <param name="node">노드 객체</param>
    /// <param name="tag">태그 이름</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>읽기 결과</returns>
    public async Task<ReadResult> ReadTagAsync(INode node, String tag, CancellationToken cancellationToken = default)
    {
        if (!TagMap.TryGetValue(tag, out var address))
            throw new ArgumentException($"标签 [{tag}] 未在 TagMap 中定义", nameof(tag));

        IPoint point = new PointModel
        {
            Name = tag,
            Address = address,
        };

        return await ReadAsync(node, [point], cancellationToken);
    }

    /// <summary>태그 이름으로 데이터 쓰기</summary>
    /// <param name="node">노드 객체</param>
    /// <param name="tag">태그 이름</param>
    /// <param name="value">값</param>
    /// <param name="cancellationToken">취소 토큰</param>
    public async Task WriteTagAsync(INode node, String tag, Object value, CancellationToken cancellationToken = default)
    {
        if (!TagMap.TryGetValue(tag, out var address))
            throw new ArgumentException($"标签 [{tag}] 未在 TagMap 中定义", nameof(tag));

        IPoint point = new PointModel
        {
            Name = tag,
            Address = address,
        };

        await WriteAsync(node, [new WriteRequest { Point = point, Value = value }], cancellationToken);
    }

    #endregion
}