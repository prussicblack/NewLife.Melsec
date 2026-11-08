namespace NewLife.Melsec.Protocols;

/// <summary>SLMP 프로토콜 소프트 요소 코드 (MC 3E 프레임과 호환)</summary>
/// <remarks>
/// SLMP(Seamless Message Protocol)의 소프트 요소 코드 체계는 MC 프로토콜 3E 프레임과 완전히 호환된다.
/// 이 enum은 명시적인 SLMP 명명을 제공하여 코드 의도를 명확히 표현한다.
/// 실제 값은 <see cref="DeviceCode"/>와 동일하며, <see cref="SLMPDeviceCodeHelper"/>로 변환할 수 있다.
/// </remarks>
public enum SLMPDeviceCode : Byte
{
    /// <summary>보조 릴레이 (비트)</summary>
    M = 0x90,

    /// <summary>입력 릴레이 (비트, 16진 번호)</summary>
    X = 0x9C,

    /// <summary>출력 릴레이 (비트, 16진 번호)</summary>
    Y = 0x9D,

    /// <summary>데이터 레지스터 (워드)</summary>
    D = 0xA8,

    /// <summary>링크 레지스터 (워드, 16진 번호)</summary>
    W = 0xB4,

    /// <summary>파일 레지스터 (워드)</summary>
    R = 0xAF,

    /// <summary>확장 파일 레지스터 (워드)</summary>
    ZR = 0xB0,

    /// <summary>링크 릴레이 (비트, 16진 번호)</summary>
    B = 0xA0,

    /// <summary>특수 릴레이 (비트)</summary>
    SM = 0x91,

    /// <summary>특수 레지스터 (워드)</summary>
    SD = 0xA9,

    /// <summary>래치 릴레이 (비트)</summary>
    L = 0x92,

    /// <summary>타이머 코일 (비트)</summary>
    TC = 0xC1,

    /// <summary>타이머 현재값 (워드)</summary>
    TS = 0xC0,

    /// <summary>카운터 코일 (비트)</summary>
    CC = 0xC5,

    /// <summary>카운터 현재값 (워드)</summary>
    CS = 0xC4,
}

/// <summary>SLMPDeviceCode 유틸리티 메서드</summary>
public static class SLMPDeviceCodeHelper
{
    /// <summary>비트 소프트 요소인지 판단</summary>
    public static Boolean IsBitDevice(SLMPDeviceCode code) =>
        DeviceCodeHelper.IsBitDevice((DeviceCode)code);

    /// <summary>MC 3E DeviceCode로 변환 (실제 값은 동일, 타입 캐스팅만 수행)</summary>
    public static DeviceCode ToMC3E(SLMPDeviceCode code) => (DeviceCode)code;

    /// <summary>MC 3E DeviceCode에서 변환</summary>
    public static SLMPDeviceCode FromMC3E(DeviceCode code) => (SLMPDeviceCode)code;
}