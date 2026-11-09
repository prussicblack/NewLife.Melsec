using System.ComponentModel;
using NewLife.IoT.Drivers;
using NewLife.Melsec.Protocols;

namespace NewLife.Melsec.Drivers;

/// <summary>미쓰비시 MC 프로토콜 드라이버 매개변수 (이더넷, 3E/1E 프레임 형식 및 바이너리/ASCII 모드 지원)</summary>
public class MCParameter : IDriverParameter, IDriverParameterKey
{
    /// <summary>PLC 주소. 형식: IP:포트, 예: 192.168.1.10:6000</summary>
    [Description("PLC address. Format: IP:port, e.g. 192.168.1.10:6000")]
    public String Address { get; set; }

    /// <summary>프레임 타입. Frame3E=3E 프레임 (기본, Q/iQ-R 시리즈), Frame1E=1E 프레임 (A 시리즈), Frame4E=4E 프레임 (iQ-R 시리즈, 시리얼 번호 포함)</summary>
    [Description("Frame type. Frame3E=3E frame (default, Q/iQ-R series), Frame1E=1E frame (A series), Frame4E=4E frame (iQ-R series, with sequence number)")]
    public MCFrameType FrameType { get; set; } = MCFrameType.Frame3E;

    /// <summary>네트워크 번호 (3E 프레임 전용). 보통 0x00 = 로컬 네트워크</summary>
    [Description("Network number. Usually 0")]
    public Byte NetworkNo { get; set; } = 0x00;

    /// <summary>데이터 형식 (3E 프레임 전용). Binary=바이너리 모드 (기본), Ascii=ASCII 16진 모드</summary>
    [Description("Data format. Binary=binary mode (default), Ascii=ASCII hex mode")]
    public MCDataFormat DataFormat { get; set; } = MCDataFormat.Binary;

    /// <summary>네트워크 타임아웃. 요청 후 응답 대기 타임아웃, 기본 5000ms</summary>
    [Description("Network timeout. Timeout waiting for response after sending a request, default 5000ms")]
    public Int32 Timeout { get; set; } = 5000;

    /// <summary>벌크 간격. 두 포인트 주소 차이가 이 값 이하이면 한 벌크로 병합, 기본 1</summary>
    [Description("Batch step. Points with an address difference less than or equal to this value are merged into one batch, default 1")]
    public Int32 BatchStep { get; set; } = 1;

    /// <summary>벌크 크기. 벌크당 최대 포인트 수, 0=제한 없음</summary>
    [Description("Batch size. Maximum number of points per batch, 0=unlimited")]
    public Int32 BatchSize { get; set; }

    /// <summary>벌크 지연. 인접한 벌크 요청 간 지연 시간, 단위 밀리초</summary>
    [Description("Batch delay. Delay between adjacent batch requests, in milliseconds")]
    public Int32 BatchDelay { get; set; }

    /// <summary>드라이버 매개변수 고유 식별자 가져오기 (Address로 서로 다른 PLC 구분)</summary>
    public String GetKey() => Address;
}