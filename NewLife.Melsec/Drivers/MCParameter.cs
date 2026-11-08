using System.ComponentModel;
using NewLife.IoT.Drivers;
using NewLife.Melsec.Protocols;

namespace NewLife.Melsec.Drivers;

/// <summary>미쓰비시 MC 프로토콜 드라이버 매개변수 (이더넷, 3E/1E 프레임 형식 및 바이너리/ASCII 모드 지원)</summary>
public class MCParameter : IDriverParameter, IDriverParameterKey
{
    /// <summary>PLC 주소. 형식: IP:포트, 예: 192.168.1.10:6000</summary>
    [Description("PLC地址。格式：IP:端口，如 192.168.1.10:6000")]
    public String Address { get; set; }

    /// <summary>프레임 타입. Frame3E=3E 프레임 (기본, Q/iQ-R 시리즈), Frame1E=1E 프레임 (A 시리즈), Frame4E=4E 프레임 (iQ-R 시리즈, 시리얼 번호 포함)</summary>
    [Description("帧类型。Frame3E=3E帧（默认，Q/iQ-R系列），Frame1E=1E帧（A系列），Frame4E=4E帧（iQ-R系列，含序列号）")]
    public MCFrameType FrameType { get; set; } = MCFrameType.Frame3E;

    /// <summary>네트워크 번호 (3E 프레임 전용). 보통 0x00 = 로컬 네트워크</summary>
    [Description("网络号。通常 0")]
    public Byte NetworkNo { get; set; } = 0x00;

    /// <summary>데이터 형식 (3E 프레임 전용). Binary=바이너리 모드 (기본), Ascii=ASCII 16진 모드</summary>
    [Description("数据格式。Binary=二进制模式（默认），Ascii=ASCII十六进制模式")]
    public MCDataFormat DataFormat { get; set; } = MCDataFormat.Binary;

    /// <summary>네트워크 타임아웃. 요청 후 응답 대기 타임아웃, 기본 5000ms</summary>
    [Description("网络超时。发起请求后等待响应的超时时间，默认 5000ms")]
    public Int32 Timeout { get; set; } = 5000;

    /// <summary>벌크 간격. 두 포인트 주소 차이가 이 값 이하이면 한 벌크로 병합, 기본 1</summary>
    [Description("批间隔。两个点位地址差小于等于该值时合并为一批，默认 1")]
    public Int32 BatchStep { get; set; } = 1;

    /// <summary>벌크 크기. 벌크당 최대 포인트 수, 0=제한 없음</summary>
    [Description("批大小。每批最多包含的点位个数，0=不限制")]
    public Int32 BatchSize { get; set; }

    /// <summary>벌크 지연. 인접한 벌크 요청 간 지연 시간, 단위 밀리초</summary>
    [Description("批延迟。相邻批次请求之间的延迟时间，单位毫秒")]
    public Int32 BatchDelay { get; set; }

    /// <summary>드라이버 매개변수 고유 식별자 가져오기 (Address로 서로 다른 PLC 구분)</summary>
    public String GetKey() => Address;
}