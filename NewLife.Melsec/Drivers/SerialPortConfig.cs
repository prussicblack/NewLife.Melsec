using System;
using System.ComponentModel;
using System.IO.Ports;
using System.Text;
using System.Xml.Serialization;
using NewLife.Xml;

namespace NewLife.Melsec.Drivers
{
    /// <summary>시리얼 포트 구성</summary>
    [XmlConfigFile("Config\\Serial.config")]
    public class SerialPortConfig : XmlConfig<SerialPortConfig>
    {
        /// <summary>시리얼 포트 이름</summary>
        [Description("串口名")]
        public String PortName { get; set; } = "COM1";

        /// <summary>보우레이트</summary>
        [Description("波特率")]
        public Int32 BaudRate { get; set; } = 9600;

        /// <summary>데이터 비트</summary>
        [Description("数据位")]
        public Int32 DataBits { get; set; } = 8;

        /// <summary>스톱 비트</summary>
        [Description("停止位 None/One/Two/OnePointFive")]
        public StopBits StopBits { get; set; } = StopBits.One;

        /// <summary>패리티</summary>
        [Description("奇偶校验 None/Odd/Even/Mark/Space")]
        public Parity Parity { get; set; } = Parity.None;

        /// <summary>텍스트 인코딩</summary>
        [XmlIgnore]
        public Encoding Encoding { get; set; } = Encoding.UTF8;

        /// <summary>인코딩</summary>
        [Description("编码 gb2312/us-ascii/utf-8")]
        public String WebEncoding { get { return Encoding.WebName; } set { Encoding = Encoding.GetEncoding(value); } }

        /// <summary>16진수 표시</summary>
        [Description("十六进制显示")]
        public Boolean HexShow { get; set; }

        /// <summary>16진수 자동 줄바꿈</summary>
        [Description("十六进制自动换行")]
        public Boolean HexNewLine { get; set; }

        /// <summary>16진수 전송</summary>
        [Description("十六进制发送")]
        public Boolean HexSend { get; set; }

        /// <summary>마지막 업데이트 시각</summary>
        [Description("最后更新时间")]
        public DateTime LastUpdate { get; set; }

        /// <summary>확장 데이터</summary>
        [Description("扩展数据")]
        public String Extend { get; set; } = "";
        /// <summary>DtrEnable</summary>
        [Description("DtrEnable")]
        public Boolean DtrEnable { get; set; } = false;
        /// <summary>RtsEnable</summary>
        [Description("RtsEnable")]
        public Boolean RtsEnable { get; set; } = false;
        /// <summary>BreakState</summary>
        [Description("BreakState")]
        public Boolean BreakState { get; set; } = false;
    }
}