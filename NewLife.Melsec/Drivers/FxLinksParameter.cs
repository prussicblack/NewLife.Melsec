using System.ComponentModel;
using System.IO.Ports;
using NewLife.IoT.Drivers;

namespace NewLife.Melsec.Drivers;

/// <summary>미쓰비시 FxLinks 매개변수</summary>
public class FxLinksParameter : IDriverParameter, IDriverParameterKey
{
    /// <summary>시리얼 포트 이름</summary>
    [Description("串口名称")]
    public String PortName { get; set; }

    /// <summary>보우레이트. 기본 9600</summary>
    [Description("波特率")]
    public Int32 Baudrate { get; set; } = 9600;

    /// <summary>데이터 비트. 기본 7</summary>
    [Description("数据位")]
    public Int32 DataBits { get; set; } = 7;

    /// <summary>패리티. 기본값 Even (짝수 패리티)</summary>
    [Description("奇偶校验位")]
    public Parity Parity { get; set; } = Parity.Even;

    /// <summary>스톱 비트. 기본 One</summary>
    [Description("停止位")]
    public StopBits StopBits { get; set; } = StopBits.One;

    /// <summary>호스트/스테이션 번호</summary>
    [Description("主机/站号")]
    public Byte Host { get; set; }

    /// <summary>네트워크 타임아웃. 요청 후 응답 대기 타임아웃, 기본 3000ms</summary>
    [Description("网络超时。发起请求后等待响应的超时时间，默认3000ms")]
    public Int32 Timeout { get; set; } = 3000;

    /// <summary>벌크 간격. 두 포인트 주소가 이 값 이하이면 한 벌크로 묶음, 기본 1</summary>
    [Description("批间隔。两个点位地址小于等于该值时凑为一批，默认1")]
    public Int32 BatchStep { get; set; } = 1;

    /// <summary>벌크 크기. 벌크 요청 시 벌크당 최대 포인트 수</summary>
    [Description("批大小。凑批请求时，每批最多点位个数")]
    public Int32 BatchSize { get; set; }

    /// <summary>벌크 지연. 인접한 요청 간 지연 시간, 단위 밀리초</summary>
    [Description("批延迟。相邻请求之间的延迟时间，单位毫秒")]
    public Int32 BatchDelay { get; set; }

    /// <summary>드라이버 매개변수 고유 식별자 가져오기</summary>
    /// <returns></returns>
    public String GetKey() => PortName;
}