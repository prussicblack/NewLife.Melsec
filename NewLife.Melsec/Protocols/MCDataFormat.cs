namespace NewLife.Melsec.Protocols;

/// <summary>MC 프로토콜 데이터 형식</summary>
/// <remarks>
/// Mitsubishi MC 프로토콜은 두 가지 데이터 인코딩 형식을 지원한다:
/// <see cref="Binary"/> (바이너리 모드)와 <see cref="Ascii"/> (ASCII 16진수 모드).
/// 
/// ASCII 모드는 일반적으로 Q 시리즈의 일부 구형 장비나 텍스트 디버깅이 필요한 시나리오에서 사용된다.
/// </remarks>
public enum MCDataFormat
{
    /// <summary>바이너리 모드 (기본값). Little-Endian 바이트 순서, 프레임이 컴팩트하고 효율적</summary>
    Binary = 0,

    /// <summary>ASCII 16진수 모드. Big-Endian 16진수 문자열 표현, 호환성이 더 좋음</summary>
    Ascii = 1,
}

/// <summary>MCDataFormat 확장 메서드</summary>
internal static class MCDataFormatHelper
{
    /// <summary>ASCII 모드인지 판단</summary>
    public static Boolean IsAscii(this MCDataFormat format) => format == MCDataFormat.Ascii;
}