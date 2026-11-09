using System.ComponentModel;
using System.IO.Ports;
using NewLife.IoT.Drivers;

namespace NewLife.Melsec.Drivers;

/// <summary>미쓰비시 FxLinks 매개변수</summary>
public class FxLinksParameter : IDriverParameter, IDriverParameterKey
{
    /// <summary>시리얼 포트 이름</summary>
    [Description("Serial port name")]
    public String PortName { get; set; }

    /// <summary>보우레이트. 기본 9600</summary>
    [Description("Baud rate")]
    public Int32 Baudrate { get; set; } = 9600;

    /// <summary>데이터 비트. 기본 7</summary>
    [Description("Data bits")]
    public Int32 DataBits { get; set; } = 7;

    /// <summary>패리티. 기본값 Even (짝수 패리티)</summary>
    [Description("Parity bit")]
    public Parity Parity { get; set; } = Parity.Even;

    /// <summary>스톱 비트. 기본 One</summary>
    [Description("Stop bits")]
    public StopBits StopBits { get; set; } = StopBits.One;

    /// <summary>호스트/스테이션 번호</summary>
    [Description("Host/station number")]
    public Byte Host { get; set; }

    /// <summary>네트워크 타임아웃. 요청 후 응답 대기 타임아웃, 기본 3000ms</summary>
    [Description("Network timeout. Timeout waiting for response after sending a request, default 3000ms")]
    public Int32 Timeout { get; set; } = 3000;

    /// <summary>벌크 간격. 두 포인트 주소가 이 값 이하이면 한 벌크로 묶음, 기본 1</summary>
    [Description("Batch step. Points with an address difference less than or equal to this value are grouped into one batch, default 1")]
    public Int32 BatchStep { get; set; } = 1;

    /// <summary>벌크 크기. 벌크 요청 시 벌크당 최대 포인트 수</summary>
    [Description("Batch size. Maximum number of points per batch when batching")]
    public Int32 BatchSize { get; set; }

    /// <summary>벌크 지연. 인접한 요청 간 지연 시간, 단위 밀리초</summary>
    [Description("Batch delay. Delay between adjacent requests, in milliseconds")]
    public Int32 BatchDelay { get; set; }

    /// <summary>드라이버 매개변수 고유 식별자 가져오기</summary>
    /// <returns></returns>
    public String GetKey() => PortName;
}