namespace NewLife.Melsec.Protocols;

/// <summary>제어 코드</summary>
public enum ControlCodes : Byte
{
    /// <summary>문의(ENQ). 요청 전송</summary>
    ENQ = 05,

    /// <summary>텍스트 시작(STX). 읽기 작업 응답의 시작</summary>
    STX = 02,

    /// <summary>텍스트 종료(ETX). 읽기 작업 응답의 끝</summary>
    ETX = 03,

    /// <summary>전송 종료</summary>
    EOT = 04,

    /// <summary>확인(ACK). 쓰기 작업 응답</summary>
    ACK = 06,

    /// <summary>미확인(NAK)</summary>
    NAK = 0x15,
}