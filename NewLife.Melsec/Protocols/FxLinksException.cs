namespace NewLife.Melsec.Protocols;

/// <summary>FxLinks 예외</summary>
public class FxLinksException : Exception
{
    /// <summary>예외 코드</summary>
    public ErrorCodes ErrorCode { get; set; }

    /// <summary>
    /// 예외 인스턴스 생성
    /// </summary>
    /// <param name="errorCode"></param>
    /// <param name="message"></param>
    public FxLinksException(ErrorCodes errorCode, String message) : base(message) => ErrorCode = errorCode;
}