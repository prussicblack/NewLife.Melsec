namespace NewLife.Melsec.Protocols;

/// <summary>MC 프로토콜 1E 프레임 응답 메시지 (A 시리즈 호환)</summary>
/// <remarks>
/// 1E 응답 프레임 형식:
/// [0]    서브헤드    1    0x00=읽기 성공, 0x80=읽기 예외; 쓰기 성공=0x00
/// [1]    종료 코드   1    0x00=성공
/// [2~]   응답 데이터 N    워드 읽기: 워드당 2바이트 LE; 비트 읽기: 바이트당 2포인트 (저 4비트=1번째, 고 4비트=2번째)
/// </remarks>
public class MC1EResponse
{
    #region 상수

    /// <summary>읽기 응답 성공 서브헤드</summary>
    public const Byte SUB_READ_SUCCESS = 0x00;

    /// <summary>읽기 응답 예외 서브헤드</summary>
    public const Byte SUB_READ_ERROR = 0x80;

    /// <summary>쓰기 응답 성공 서브헤드</summary>
    public const Byte SUB_WRITE_SUCCESS = 0x00;

    /// <summary>1E 프레임 응답 고정 헤더 길이 (서브헤드+종료 코드 = 2바이트)</summary>
    public const Int32 FIXED_HEADER_LEN = 2;

    #endregion

    #region 속성

    /// <summary>서브헤드. 읽기 응답은 0x00(성공) 또는 0x80(예외); 쓰기 응답은 0x00</summary>
    public Byte SubHeader { get; set; }

    /// <summary>종료 코드. 0x00=성공, 0이 아닌 값=오류</summary>
    public Byte EndCode { get; set; }

    /// <summary>응답 원시 데이터 (종료 코드 이후의 바이트)</summary>
    public Byte[] RawData { get; set; }

    #endregion

    #region 생성

    /// <summary>오버라이드. 친화적인 문자열</summary>
    public override String ToString()
    {
        if (EndCode != 0) return $"MC1E Response ERROR 0x{EndCode:X2}";
        return $"MC1E Response OK, DataLen={RawData?.Length ?? 0}";
    }

    #endregion

    #region 직렬화

    /// <summary>바이트 배열에서 응답 프레임 해독</summary>
    /// <param name="data">프레임 데이터</param>
    /// <returns>성공 여부</returns>
    public Boolean Read(Byte[] data)
    {
        if (data == null || data.Length < FIXED_HEADER_LEN) return false;

        var offset = 0;
        SubHeader = data[offset++];
        EndCode = data[offset++];

        var dataLen = data.Length - FIXED_HEADER_LEN;
        if (dataLen > 0)
        {
            RawData = new Byte[dataLen];
            Array.Copy(data, offset, RawData, 0, dataLen);
        }

        return true;
    }

    /// <summary>바이트 배열로 직렬화 (테스트용 서버 응답 시뮬레이션)</summary>
    public Byte[] ToBytes()
    {
        var dataLength = FIXED_HEADER_LEN + (RawData?.Length ?? 0);
        var buffer = new Byte[dataLength];
        var offset = 0;

        buffer[offset++] = SubHeader;
        buffer[offset++] = EndCode;

        if (RawData != null && RawData.Length > 0)
            Array.Copy(RawData, 0, buffer, offset, RawData.Length);

        return buffer;
    }

    #endregion

    #region 데이터 해독

    /// <summary>응답 데이터를 워드 소프트 요소 배열로 해독 (워드당 2바이트 LE)</summary>
    public UInt16[] GetWordData()
    {
        if (RawData == null || RawData.Length == 0) return [];
        var words = new UInt16[RawData.Length / 2];
        for (var i = 0; i < words.Length; i++)
            words[i] = (UInt16)(RawData[i * 2] | (RawData[i * 2 + 1] << 8));
        return words;
    }

    /// <summary>응답 데이터를 비트 소프트 요소 배열로 해독 (바이트당 2포인트, 저 4비트 = 1번째, 고 4비트 = 2번째)</summary>
    /// <param name="count">기대하는 포인트 수</param>
    public Boolean[] GetBitData(Int32 count)
    {
        if (RawData == null || RawData.Length == 0) return [];
        var bits = new Boolean[count];
        for (var i = 0; i < count; i++)
        {
            var byteIdx = i / 2;
            var shift = (i % 2) * 4;
            if (byteIdx < RawData.Length)
                bits[i] = ((RawData[byteIdx] >> shift) & 0x0F) != 0;
        }
        return bits;
    }

    #endregion

    #region 팩토리 메서드 (테스트 헬퍼)

    /// <summary>성공한 워드 읽기 응답 생성 (테스트용)</summary>
    /// <param name="values">워드 데이터</param>
    public static MC1EResponse BuildWordResponse(UInt16[] values)
    {
        var raw = MCMessage.PackWords(values);
        return new MC1EResponse { SubHeader = SUB_READ_SUCCESS, EndCode = 0, RawData = raw };
    }

    /// <summary>성공한 비트 읽기 응답 생성 (테스트용)</summary>
    /// <param name="values">비트 데이터 (true=ON, false=OFF)</param>
    public static MC1EResponse BuildBitResponse(Boolean[] values)
    {
        var ushorts = Array.ConvertAll(values, v => v ? (UInt16)1 : (UInt16)0);
        var raw = MCMessage.PackBits(ushorts);
        return new MC1EResponse { SubHeader = SUB_READ_SUCCESS, EndCode = 0, RawData = raw };
    }

    /// <summary>성공한 쓰기 응답 생성 (테스트용)</summary>
    public static MC1EResponse BuildWriteResponse() => new() { SubHeader = SUB_WRITE_SUCCESS, EndCode = 0 };

    /// <summary>오류 응답 생성 (테스트용)</summary>
    /// <param name="endCode">오류 코드</param>
    public static MC1EResponse BuildErrorResponse(Byte endCode) => new() { SubHeader = SUB_READ_ERROR, EndCode = endCode };

    #endregion
}