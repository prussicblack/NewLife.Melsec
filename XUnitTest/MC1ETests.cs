using System;
using System.ComponentModel;
using NewLife;
using NewLife.Melsec.Protocols;
using Xunit;

namespace XUnitTest;

/// <summary>MC 프로토콜 1E 프레임 인코딩/디코딩 및 드라이버 테스트</summary>
public class MC1ETests
{
    #region MC1EMessage 요청 프레임 인코딩

    [Fact]
    [DisplayName("1E ReadWord request D100×4 encoding")]
    public void BuildReadWord_D100_4Words()
    {
        // A 시리즈 1E 프레임: D100부터 4개의 워드 읽기
        // D=0x40, addr=100=0x0064, count=4
        // 서브헤드(0x00) + PLC 번호(0xFF) + 모니터링 타이머(0A00) + 시작 주소(6400) + 소프트 요소 코드(40) + 포인트 수(0400)
        var expected = "00-FF-0A-00-64-00-40-04-00".ToHex();

        var msg = MC1EMessage.BuildReadWord(MC1EDeviceCode.D, 100, 4);
        var actual = msg.ToBytes();

        Assert.Equal(expected.ToHex("-"), actual.ToHex("-"));
    }

    [Fact]
    [DisplayName("1E ReadWord request field properties")]
    public void BuildReadWord_Properties()
    {
        var msg = MC1EMessage.BuildReadWord(MC1EDeviceCode.D, 100, 4);

        Assert.Equal(MC1EMessage.SUB_READ_WORD, msg.SubHeader);
        Assert.Equal(MC1EDeviceCode.D, msg.DeviceCode);
        Assert.Equal(100, msg.StartAddress);
        Assert.Equal(4, msg.Count);
        Assert.Null(msg.WriteData);
    }

    [Fact]
    [DisplayName("1E ReadWord request round-trip")]
    public void BuildReadWord_RoundTrip()
    {
        var original = MC1EMessage.BuildReadWord(MC1EDeviceCode.D, 100, 4);
        var bytes = original.ToBytes();

        var decoded = new MC1EMessage();
        Assert.True(decoded.Read(bytes));

        Assert.Equal(original.SubHeader, decoded.SubHeader);
        Assert.Equal(original.DeviceCode, decoded.DeviceCode);
        Assert.Equal(original.StartAddress, decoded.StartAddress);
        Assert.Equal(original.Count, decoded.Count);
        Assert.Equal(original.WriteData, decoded.WriteData);
    }

    [Fact]
    [DisplayName("1E ReadBit request M200×8 encoding")]
    public void BuildReadBit_M200_8Bits()
    {
        // M=0x10, addr=200=0x00C8, count=8
        // 서브헤드(0x01=비트 읽기) + PLC 번호(0xFF) + 모니터링 타이머(0A00) + 시작 주소(C800) + 소프트 요소 코드(10) + 포인트 수(0800)
        var expected = "01-FF-0A-00-C8-00-10-08-00".ToHex();

        var msg = MC1EMessage.BuildReadBit(MC1EDeviceCode.M, 200, 8);
        var actual = msg.ToBytes();

        Assert.Equal(expected.ToHex("-"), actual.ToHex("-"));
    }

    [Fact]
    [DisplayName("1E ReadBit request round-trip")]
    public void BuildReadBit_RoundTrip()
    {
        var original = MC1EMessage.BuildReadBit(MC1EDeviceCode.M, 200, 16);
        var bytes = original.ToBytes();

        var decoded = new MC1EMessage();
        Assert.True(decoded.Read(bytes));

        Assert.Equal(original.SubHeader, decoded.SubHeader);
        Assert.Equal(original.DeviceCode, decoded.DeviceCode);
        Assert.Equal(original.StartAddress, decoded.StartAddress);
        Assert.Equal(original.Count, decoded.Count);
    }

    [Fact]
    [DisplayName("1E WriteWord request D100 write 3 words encoding")]
    public void BuildWriteWord_D100_3Values()
    {
        // 서브헤드(0x02=워드 쓰기) + 데이터 [01,00,02,00,03,00]
        var msg = MC1EMessage.BuildWriteWord(MC1EDeviceCode.D, 100, new UInt16[] { 1, 2, 3 });
        var bytes = msg.ToBytes();

        // Round-trip 검증
        var decoded = new MC1EMessage();
        Assert.True(decoded.Read(bytes));

        Assert.Equal(msg.SubHeader, decoded.SubHeader);
        Assert.Equal(msg.DeviceCode, decoded.DeviceCode);
        Assert.Equal(msg.StartAddress, decoded.StartAddress);
        Assert.Equal(msg.Count, decoded.Count);
        Assert.Equal(msg.WriteData, decoded.WriteData);
    }

    [Fact]
    [DisplayName("1E WriteBit request encoding")]
    public void BuildWriteBit_Y0_4Bits()
    {
        var msg = MC1EMessage.BuildWriteBit(MC1EDeviceCode.Y, 0, new UInt16[] { 1, 0, 1, 0 });
        var bytes = msg.ToBytes();

        var decoded = new MC1EMessage();
        Assert.True(decoded.Read(bytes));

        Assert.Equal(msg.SubHeader, decoded.SubHeader);
        Assert.Equal(msg.DeviceCode, decoded.DeviceCode);
        Assert.Equal(msg.Count, decoded.Count);
        Assert.Equal(msg.WriteData, decoded.WriteData);
    }

    #endregion

    #region MC1EResponse 응답 파싱

    [Fact]
    [DisplayName("1E ReadWord success response parsing")]
    public void ReadWordResponse_Success()
    {
        // 응답: 서브헤드(0x00) + 종료 코드(0x00) + 데이터(1100 2200 3300 4400)
        var data = "00-00-11-00-22-00-33-00-44-00".ToHex();

        var response = new MC1EResponse();
        Assert.True(response.Read(data));

        Assert.Equal(0x00, response.SubHeader);
        Assert.Equal(0, response.EndCode);
        var words = response.GetWordData();
        Assert.Equal(4, words.Length);
        Assert.Equal((UInt16)0x0011, words[0]);
        Assert.Equal((UInt16)0x0022, words[1]);
        Assert.Equal((UInt16)0x0033, words[2]);
        Assert.Equal((UInt16)0x0044, words[3]);
    }

    [Fact]
    [DisplayName("1E ReadBit success response parsing")]
    public void ReadBitResponse_Success()
    {
        // 비트 응답 M0×8: [ON, OFF, ON, OFF, ON, OFF, ON, OFF]
        // PackBits → [0x01, 0x01, 0x01, 0x01]
        var data = "00-00-01-01-01-01".ToHex();

        var response = new MC1EResponse();
        Assert.True(response.Read(data));

        Assert.Equal(0, response.EndCode);
        var bits = response.GetBitData(8);
        Assert.Equal(8, bits.Length);
        Assert.True(bits[0]);
        Assert.False(bits[1]);
        Assert.True(bits[2]);
        Assert.False(bits[3]);
        Assert.True(bits[4]);
        Assert.False(bits[5]);
        Assert.True(bits[6]);
        Assert.False(bits[7]);
    }

    [Fact]
    [DisplayName("1E error response parsing")]
    public void Read_ErrorResponse()
    {
        // 서브헤드 0x80=읽기 오류, 종료 코드 0x50=오류
        var data = "80-50".ToHex();

        var response = new MC1EResponse();
        Assert.True(response.Read(data));

        Assert.Equal(0x80, response.SubHeader);
        Assert.Equal(0x50, response.EndCode);
        Assert.Null(response.RawData);
    }

    [Fact]
    [DisplayName("1E write success response parsing")]
    public void Read_WriteResponse_Success()
    {
        // 서브헤드 0x00=쓰기 성공, 종료 코드 0x00
        var data = "00-00".ToHex();

        var response = new MC1EResponse();
        Assert.True(response.Read(data));

        Assert.Equal(0, response.SubHeader);
        Assert.Equal(0, response.EndCode);
        Assert.Null(response.RawData);
    }

    [Fact]
    [DisplayName("1E response round-trip")]
    public void WriteRead_RoundTrip()
    {
        var original = new MC1EResponse
        {
            SubHeader = MC1EResponse.SUB_READ_SUCCESS,
            EndCode = 0,
            RawData = new Byte[] { 0x11, 0x00, 0x22, 0x00, 0x33, 0x00, 0x44, 0x00 },
        };

        var bytes = original.ToBytes();
        var decoded = new MC1EResponse();
        Assert.True(decoded.Read(bytes));

        Assert.Equal(original.EndCode, decoded.EndCode);
        Assert.Equal(original.RawData, decoded.RawData);
    }

    #endregion

    #region 디바이스 코드 매핑

    [Fact]
    [DisplayName("DeviceCode 3E→1E mapping verification")]
    public void DeviceCode_Mapping_3E_to_1E()
    {
        Assert.Equal(MC1EDeviceCode.D, MC1EDeviceCodeHelper.From3E(DeviceCode.D));
        Assert.Equal(MC1EDeviceCode.M, MC1EDeviceCodeHelper.From3E(DeviceCode.M));
        Assert.Equal(MC1EDeviceCode.X, MC1EDeviceCodeHelper.From3E(DeviceCode.X));
        Assert.Equal(MC1EDeviceCode.Y, MC1EDeviceCodeHelper.From3E(DeviceCode.Y));
    }

    [Fact]
    [DisplayName("DeviceCode 1E→3E mapping verification")]
    public void DeviceCode_Mapping_1E_to_3E()
    {
        Assert.Equal(DeviceCode.D, MC1EDeviceCodeHelper.To3E(MC1EDeviceCode.D));
        Assert.Equal(DeviceCode.M, MC1EDeviceCodeHelper.To3E(MC1EDeviceCode.M));
    }

    [Fact]
    [DisplayName("IsBitDevice check")]
    public void IsBitDevice_Check()
    {
        Assert.True(MC1EDeviceCodeHelper.IsBitDevice(MC1EDeviceCode.M));
        Assert.True(MC1EDeviceCodeHelper.IsBitDevice(MC1EDeviceCode.X));
        Assert.True(MC1EDeviceCodeHelper.IsBitDevice(MC1EDeviceCode.Y));
        Assert.False(MC1EDeviceCodeHelper.IsBitDevice(MC1EDeviceCode.D));
        Assert.False(MC1EDeviceCodeHelper.IsBitDevice(MC1EDeviceCode.W));
    }

    #endregion

    #region 경계 상황

    [Fact]
    [DisplayName("1E null data returns false")]
    public void Read_NullData_ReturnsFalse()
    {
        var msg = new MC1EMessage();
        Assert.False(msg.Read(null));

        var response = new MC1EResponse();
        Assert.False(response.Read(null));
    }

    [Fact]
    [DisplayName("1E short data returns false")]
    public void Read_ShortData_ReturnsFalse()
    {
        var msg = new MC1EMessage();
        Assert.False(msg.Read(new Byte[] { 0x00 })); // 바이트가 1개뿐이라 최소 헤드 길이에 부족
    }

    [Fact]
    [DisplayName("1E address 16-bit limit verification")]
    public void Address_Max16Bit()
    {
        var msg = MC1EMessage.BuildReadWord(MC1EDeviceCode.D, 0xFFFF, 1);
        var bytes = msg.ToBytes();

        var decoded = new MC1EMessage();
        Assert.True(decoded.Read(bytes));
        Assert.Equal(0xFFFF, decoded.StartAddress);
    }

    #endregion
}
