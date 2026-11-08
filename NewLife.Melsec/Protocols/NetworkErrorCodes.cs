namespace NewLife.Melsec.Protocols;

/// <summary>에러 코드</summary>
public enum NetworkErrorCodes
{
    /// <summary>명령 타임아웃</summary>
    /// <remarks>After master station send request to save station, no answer passing comms time-out.</remarks>
    CommsTimeout = 0x01,

    /// <summary>스테이션 번호 에러</summary>
    /// <remarks>Station No. is not agreement between the master station and the slave station.</remarks>
    StationError = 0x02,

    /// <summary>통신 카운터 에러</summary>
    /// <remarks>Communication counter is not agreement between the master station and the slave station.</remarks>
    CommsCounterError = 0x03,

    /// <summary>통신 형식 에러</summary>
    /// <remarks>Communication format is not right from slave station.</remarks>
    CommsFormatError = 0x04,

    /// <summary>마스터 통신 타임아웃</summary>
    /// <remarks>After slave station send answer to master station, master station do not send request to next slave station.</remarks>
    MasterCommsTimeoutError = 0x11,

    /// <summary>마스터 통신 형식 에러</summary>
    /// <remarks>Communication format is not right from master station.</remarks>
    MasterCommsFormatError = 0x14,

    /// <summary>슬레이브 존재하지 않음</summary>
    /// <remarks>The station No. is not in this network.</remarks>
    NoSlave = 0x21,

    /// <summary>스테이션 번호 에러</summary>
    /// <remarks>Station No. is not agreement between the master station and the slave station.</remarks>
    StationError2 = 0x22,

    /// <summary>통신 카운터 에러</summary>
    /// <remarks>Communication counter is not agreement between the master station and the slave station.</remarks>
    CommsCounterError2 = 0x23,

    /// <summary>통신 파라미터 미수신</summary>
    /// <remarks>When slave station receive request from master station before communication parameter.</remarks>
    NotReceiveCommsParameter = 0x31,
}