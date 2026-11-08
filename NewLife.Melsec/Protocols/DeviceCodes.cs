namespace NewLife.Melsec.Protocols;

/// <summary>MC 프로토콜 소프트 요소(디바이스) 코드, 바이너리 모드</summary>
public enum DeviceCode : Byte
{
    /// <summary>특수 릴레이 (비트)</summary>
    SM = 0x91,

    /// <summary>특수 레지스터 (워드)</summary>
    SD = 0xA9,

    /// <summary>입력 릴레이 (비트, 16진 번호)</summary>
    X = 0x9C,

    /// <summary>출력 릴레이 (비트, 16진 번호)</summary>
    Y = 0x9D,

    /// <summary>직접 입력 (비트, 16진 번호)</summary>
    DX = 0xA2,

    /// <summary>직접 출력 (비트, 16진 번호)</summary>
    DY = 0xA3,

    /// <summary>보조 릴레이 (비트)</summary>
    M = 0x90,

    /// <summary>래치 릴레이 (비트)</summary>
    L = 0x92,

    /// <summary>알람 릴레이 (비트)</summary>
    F = 0x93,

    /// <summary>엣지 릴레이 (비트)</summary>
    V = 0x94,

    /// <summary>링크 릴레이 (비트, 16진 번호)</summary>
    B = 0xA0,

    /// <summary>타이머 코일 (비트)</summary>
    TC = 0xC1,

    /// <summary>타이머 현재값 (워드)</summary>
    TS = 0xC0,

    /// <summary>카운터 코일 (비트)</summary>
    CC = 0xC5,

    /// <summary>카운터 현재값 (워드)</summary>
    CS = 0xC4,

    /// <summary>데이터 레지스터 (워드)</summary>
    D = 0xA8,

    /// <summary>링크 레지스터 (워드, 16진 번호)</summary>
    W = 0xB4,

    /// <summary>파일 레지스터 (워드)</summary>
    R = 0xAF,

    /// <summary>확장 파일 레지스터 (워드)</summary>
    ZR = 0xB0,
}

/// <summary>DeviceCode 유틸리티 메서드</summary>
public static class DeviceCodeHelper
{
    /// <summary>비트 소프트 요소(Bit Device)인지 판단</summary>
    /// <param name="code">소프트 요소 코드</param>
    /// <returns>true=비트 소프트 요소, false=워드 소프트 요소</returns>
    public static Boolean IsBitDevice(DeviceCode code) => code switch
    {
        DeviceCode.SM or DeviceCode.X or DeviceCode.Y or DeviceCode.DX or
        DeviceCode.DY or DeviceCode.M or DeviceCode.L or DeviceCode.F or
        DeviceCode.V or DeviceCode.B or DeviceCode.TC or DeviceCode.CC => true,
        _ => false,
    };

    /// <summary>MC 프로토콜 주소 문자열을 분석하여 소프트 요소 코드와 주소 번호를 반환</summary>
    /// <remarks>
    /// 주소 형식: D100, M200, X1F, Y2A, B100, TC5, ZR10000 등.
    /// X/Y/B/W/DX/DY는 16진 번호 사용, 나머지는 10진 번호 사용.
    /// 콜론 뒤의 비트 영역 (예: D100:0)은 자동 무시됨.
    /// </remarks>
    /// <param name="address">주소 문자열</param>
    /// <returns>소프트 요소 코드와 주소 번호의 튜플</returns>
    public static (DeviceCode Code, Int32 Address) ParseAddress(String address)
    {
        if (address.IsNullOrEmpty()) throw new ArgumentNullException(nameof(address));

        // 콜론 뒤의 비트 영역 제거, 예: "D100:0" → "D100"
        var colonPos = address.IndexOf(':');
        if (colonPos > 0) address = address[..colonPos];

        address = address.ToUpperInvariant().Trim();

        // 먼저 2자 접두어 시도 (ZR, SM, SD, DX, DY, TC, TS, CC, CS)
        if (address.Length >= 3)
        {
            var prefix2 = address[..2];
            switch (prefix2)
            {
                case "SM": return (DeviceCode.SM, ParseNum(address[2..], false));
                case "SD": return (DeviceCode.SD, ParseNum(address[2..], false));
                case "DX": return (DeviceCode.DX, ParseNum(address[2..], true));
                case "DY": return (DeviceCode.DY, ParseNum(address[2..], true));
                case "TC": return (DeviceCode.TC, ParseNum(address[2..], false));
                case "TS": return (DeviceCode.TS, ParseNum(address[2..], false));
                case "CC": return (DeviceCode.CC, ParseNum(address[2..], false));
                case "CS": return (DeviceCode.CS, ParseNum(address[2..], false));
                case "ZR": return (DeviceCode.ZR, ParseNum(address[2..], false));
            }
        }

        // 1자 접두어
        if (address.Length >= 2)
        {
            var prefix1 = address[..1];
            switch (prefix1)
            {
                case "X": return (DeviceCode.X, ParseNum(address[1..], true));
                case "Y": return (DeviceCode.Y, ParseNum(address[1..], true));
                case "M": return (DeviceCode.M, ParseNum(address[1..], false));
                case "L": return (DeviceCode.L, ParseNum(address[1..], false));
                case "F": return (DeviceCode.F, ParseNum(address[1..], false));
                case "V": return (DeviceCode.V, ParseNum(address[1..], false));
                case "B": return (DeviceCode.B, ParseNum(address[1..], true));
                case "D": return (DeviceCode.D, ParseNum(address[1..], false));
                case "W": return (DeviceCode.W, ParseNum(address[1..], true));
                case "R": return (DeviceCode.R, ParseNum(address[1..], false));
            }
        }

        throw new NotSupportedException($"不支持的MC协议软元件地址格式：{address}");
    }

    private static Int32 ParseNum(String s, Boolean isHex) =>
        isHex ? Convert.ToInt32(s, 16) : Int32.Parse(s);
}