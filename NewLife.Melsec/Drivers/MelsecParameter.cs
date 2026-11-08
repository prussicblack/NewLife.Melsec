using System.ComponentModel;
using NewLife.IoT.Drivers;

namespace NewLife.Melsec.Drivers;

/// <summary>Melsec 매개변수</summary>
public class MelsecParameter : IDriverParameter
{
    /// <summary>주소. 예: 127.0.0.1:6000</summary>
    [Description("地址。例如 127.0.0.1:6000")]
    public String Address { get; set; }

    /// <summary>데이터 형식. ABCD/BADC/CDAB/DCBA</summary>
    [Description("数据格式。ABCD/BADC/CDAB/DCBA")]
    public String DataFormat { get; set; }

    /// <summary>통신 프로토콜. MCQna3E/FxLinks485</summary>
    [Description("通信协议。MCQna3E/FxLinks485")]
    public Protocol Protocol { get; set; } = Protocol.MCQna3E;

    /// <summary>시리얼 포트 이름</summary>
    [Description("串口名称")]
    public String PortName { get; set; }

    /// <summary>보우레이트</summary>
    [Description("波特率")]
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