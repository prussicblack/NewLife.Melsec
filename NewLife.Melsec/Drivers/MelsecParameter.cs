using System.ComponentModel;
using NewLife.IoT.Drivers;

namespace NewLife.Melsec.Drivers;

/// <summary>Melsec 매개변수</summary>
public class MelsecParameter : IDriverParameter
{
    /// <summary>주소. 예: 127.0.0.1:6000</summary>
    [Description("Address. e.g. 127.0.0.1:6000")]
    public String Address { get; set; }

    /// <summary>데이터 형식. ABCD/BADC/CDAB/DCBA</summary>
    [Description("Data format. ABCD/BADC/CDAB/DCBA")]
    public String DataFormat { get; set; }

    /// <summary>통신 프로토콜. MCQna3E/FxLinks485</summary>
    [Description("Protocol. MCQna3E/FxLinks485")]
    public Protocol Protocol { get; set; } = Protocol.MCQna3E;

    /// <summary>시리얼 포트 이름</summary>
    [Description("Serial port name")]
    public String PortName { get; set; }

    /// <summary>보우레이트</summary>
    [Description("Baud rate")]
    public Int32 Baudrate { get; set; } = 9600;
}

public enum Protocol
{
    /// <summary>
    /// MC 프로토콜 Qna-3E 모드
    /// </summary>
    MCQna3E,

    /// <summary>
    /// FX 시리즈 컴퓨터 링크
    /// </summary>
    FxLinks485
}