namespace NewLife.Melsec.Protocols;

/// <summary>에러 코드</summary>
public enum ErrorCodes
{
    ///// <summary>일반 에러</summary>
    //Normal = 0x00,

    ///// <summary>일반 에러</summary>
    //Normal2 = 0x01,

    /// <summary>합산 에러</summary>
    SumError = 0x02,

    /// <summary>통신 카운터 에러</summary>
    /// <remarks>Protocol error (the communication protocol does not conform to the format selected with D8120)</remarks>
    ProtocolError = 0x03,

    /// <summary>문자 영역 에러</summary>
    /// <remarks>Character area error (the character area is incorrectly defined, or the specified command is not available)</remarks>
    CharacterAreaError = 0x06,

    /// <summary>문자 에러</summary>
    /// <remarks>Character error (the data to be written to a device consists of ASCII codes other than hexadecimal codes)</remarks>
    CharacterError = 0x07,

    /// <summary>PLC 번호 에러</summary>
    /// <remarks>PLC number error (the PLC number is not set to “FF” or not available from this station)</remarks>
    PLCError = 0x0A,

    /// <summary>PLC 번호 에러</summary>
    /// <remarks>PLC number error (the PLC number is not set to “FF” or not available from this station)</remarks>
    PLCError2 = 0x10,

    /// <summary>리모트 에러</summary>
    /// <remarks>Remote error (remote run/stop is disabled)</remarks>
    RemoteError = 0x18,
}