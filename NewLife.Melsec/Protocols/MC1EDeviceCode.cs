namespace NewLife.Melsec.Protocols;

/// <summary>MC 프로토콜 1E 프레임 소프트 요소 코드 (A 시리즈 호환)</summary>
/// <remarks>
/// 1E 프레임은 3E 프레임과 다른 소프트 요소 코드 체계를 사용한다.
/// 3E 프레임 코드는 <see cref="DeviceCode"/> enum 참조.
/// </remarks>
public enum MC1EDeviceCode : Byte
{
    /// <summary>보조 릴레이 (비트)</summary>
    M = 0x10,

    /// <summary>입력 릴레이 (비트, 16진 번호)</summary>
    X = 0x1C,

    /// <summary>출력 릴레이 (비트, 16진 번호)</summary>
    Y = 0x1D,

    /// <summary>래치 릴레이 (비트)</summary>
    L = 0x12,

    /// <summary>알람 릴레이 (비트)</summary>
    F = 0x13,

    /// <summary>엣지 릴레이 (비트)</summary>
    V = 0x14,

    /// <summary>데이터 레지스터 (워드)</summary>
    D = 0x40,

    /// <summary>링크 레지스터 (워드, 16진 번호)</summary>
    W = 0x44,

    /// <summary>파일 레지스터 (워드)</summary>
    R = 0x48,

    /// <summary>타이머 코일 (비트)</summary>
    TC = 0x31,

    /// <summary>타이머 현재값 (워드)</summary>
    TS = 0x30,

    /// <summary>카운터 코일 (비트)</summary>
    CC = 0x35,

    /// <summary>카운터 현재값 (워드)</summary>
    CS = 0x34,

    /// <summary>특수 릴레이 (비트)</summary>
    SM = 0x91,

    /// <summary>특수 레지스터 (워드)</summary>
    SD = 0xA9,
}

/// <summary>MC1EDeviceCode 유틸리티 메서드</summary>
public static class MC1EDeviceCodeHelper
{
    /// <summary>비트 소프트 요소(Bit Device)인지 판단</summary>
    public static Boolean IsBitDevice(MC1EDeviceCode code) => code switch
    {
        MC1EDeviceCode.M or MC1EDeviceCode.X or MC1EDeviceCode.Y or
        MC1EDeviceCode.L or MC1EDeviceCode.F or MC1EDeviceCode.V or
        MC1EDeviceCode.TC or MC1EDeviceCode.CC or MC1EDeviceCode.SM => true,
        _ => false,
    };

    /// <summary>3E 프레임 DeviceCode를 1E 프레임 MC1EDeviceCode로 매핑</summary>
    /// <param name="code">3E 프레임 소프트 요소 코드</param>
    /// <returns>1E 프레임 소프트 요소 코드</returns>
    public static MC1EDeviceCode From3E(DeviceCode code) => code switch
    {
        DeviceCode.M => MC1EDeviceCode.M,
        DeviceCode.X => MC1EDeviceCode.X,
        DeviceCode.Y => MC1EDeviceCode.Y,
        DeviceCode.L => MC1EDeviceCode.L,
        DeviceCode.F => MC1EDeviceCode.F,
        DeviceCode.V => MC1EDeviceCode.V,
        DeviceCode.D => MC1EDeviceCode.D,
        DeviceCode.W => MC1EDeviceCode.W,
        DeviceCode.R => MC1EDeviceCode.R,
        DeviceCode.TC => MC1EDeviceCode.TC,
        DeviceCode.TS => MC1EDeviceCode.TS,
        DeviceCode.CC => MC1EDeviceCode.CC,
        DeviceCode.CS => MC1EDeviceCode.CS,
        DeviceCode.SM => MC1EDeviceCode.SM,
        DeviceCode.SD => MC1EDeviceCode.SD,
        _ => throw new NotSupportedException($"不支持的软元件代码 0x{(Byte)code:X2}，仅 A 系列兼容子集可用"),
    };

    /// <summary>1E 프레임 MC1EDeviceCode를 3E 프레임 DeviceCode로 역매핑</summary>
    public static DeviceCode To3E(MC1EDeviceCode code) => code switch
    {
        MC1EDeviceCode.M => DeviceCode.M,
        MC1EDeviceCode.X => DeviceCode.X,
        MC1EDeviceCode.Y => DeviceCode.Y,
        MC1EDeviceCode.L => DeviceCode.L,
        MC1EDeviceCode.F => DeviceCode.F,
        MC1EDeviceCode.V => DeviceCode.V,
        MC1EDeviceCode.D => DeviceCode.D,
        MC1EDeviceCode.W => DeviceCode.W,
        MC1EDeviceCode.R => DeviceCode.R,
        MC1EDeviceCode.TC => DeviceCode.TC,
        MC1EDeviceCode.TS => DeviceCode.TS,
        MC1EDeviceCode.CC => DeviceCode.CC,
        MC1EDeviceCode.CS => DeviceCode.CS,
        MC1EDeviceCode.SM => DeviceCode.SM,
        MC1EDeviceCode.SD => DeviceCode.SD,
        _ => throw new NotSupportedException($"不支持的 1E 软元件代码 0x{(Byte)code:X2}"),
    };
}