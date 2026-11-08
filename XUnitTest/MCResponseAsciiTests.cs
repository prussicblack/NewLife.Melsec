using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using NewLife;
using NewLife.Melsec.Protocols;
using Xunit;

namespace XUnitTest;

/// <summary>MC 프로토콜 3E 프레임 ASCII 모드 응답 파싱 테스트</summary>
public class MCResponseAsciiTests
{
    /// <summary>바이너리 프레임 바이트 배열을 ASCII 16진수 텍스트 바이트로 변환</summary>
    private static Byte[] BinaryToAsciiText(Byte[] binary) =>
        Encoding.ASCII.GetBytes(binary.ToHex());

    [Fact]
    [DisplayName("ASCII 读字成功响应解析")]
    public void Read_WordResponse_Success_Ascii()
    {
        // 바이너리 프레임: D0-00-00-FF-FF-03-00-0A-00-00-00-11-00-22-00-33-00-44-00
        // dataLength = 0x000A (10) = EndCode(2) + ResponseData(8)
        var binary = new Byte[]
        {
            0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x0A, 0x00,
            0x00, 0x00, 0x11, 0x00, 0x22, 0x00, 0x33, 0x00, 0x44, 0x00,
        };
        var data = BinaryToAsciiText(binary);

        var response = MCResponse.FromBytes(data, MCDataFormat.Ascii);

        Assert.Equal(0, response.EndCode);
        var words = response.GetWordData();
        Assert.Equal(4, words.Length);
        Assert.Equal((UInt16)0x0011, words[0]);
        Assert.Equal((UInt16)0x0022, words[1]);
        Assert.Equal((UInt16)0x0033, words[2]);
        Assert.Equal((UInt16)0x0044, words[3]);
    }

    [Fact]
    [DisplayName("ASCII 读位成功响应解析")]
    public void Read_BitResponse_Success_Ascii()
    {
        // 비트 응답 M0×8: [ON, OFF, ON, OFF, ON, OFF, ON, OFF]
        // PackBits로 바이트 패킹: [0x01, 0x01, 0x01, 0x01]
        // EndCode 2바이트 + ResponseData 4바이트 = dataLength=6
        var binary = new Byte[]
        {
            0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06, 0x00,
            0x00, 0x00, 0x01, 0x01, 0x01, 0x01,
        };
        var data = BinaryToAsciiText(binary);

        var response = MCResponse.FromBytes(data, MCDataFormat.Ascii);

        Assert.Equal(0, response.EndCode);
        var bits = response.GetBitData(8);
        Assert.Equal(8, bits.Length);
        Assert.True(bits[0]);   // ON
        Assert.False(bits[1]);  // OFF
        Assert.True(bits[2]);   // ON
        Assert.False(bits[3]);  // OFF
        Assert.True(bits[4]);   // ON
        Assert.False(bits[5]);  // OFF
        Assert.True(bits[6]);   // ON
        Assert.False(bits[7]);  // OFF
    }

    [Fact]
    [DisplayName("ASCII 错误响应解析")]
    public void Read_ErrorResponse_Ascii()
    {
        // 종료 코드 0x0050 = 통신 타임아웃 등 오류
        var binary = new Byte[]
        {
            0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00,
            0x50, 0x00,
        };
        var data = BinaryToAsciiText(binary);

        var response = MCResponse.FromBytes(data, MCDataFormat.Ascii);

        Assert.Equal(0x0050, response.EndCode);
        Assert.Null(response.RawData);
    }

    [Fact]
    [DisplayName("ASCII 写入成功响应解析")]
    public void Read_WriteResponse_Success_Ascii()
    {
        // 쓰기 응답은 ACK만 있고 데이터 없음: 종료 코드 0x0000
        var binary = new Byte[]
        {
            0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00,
            0x00, 0x00,
        };
        var data = BinaryToAsciiText(binary);

        var response = MCResponse.FromBytes(data, MCDataFormat.Ascii);

        Assert.Equal(0, response.EndCode);
        Assert.Null(response.RawData);
    }

    [Fact]
    [DisplayName("ASCII 响应 Round-trip")]
    public void WriteRead_RoundTrip_Ascii()
    {
        var original = new MCResponse
        {
            DataFormat = MCDataFormat.Ascii,
            EndCode = 0,
            RawData = new Byte[] { 0x11, 0x00, 0x22, 0x00, 0x33, 0x00, 0x44, 0x00 },
        };

        var bytes = original.ToBytes();

        var decoded = MCResponse.FromBytes(bytes, MCDataFormat.Ascii);

        Assert.Equal(original.EndCode, decoded.EndCode);
        Assert.Equal(original.RawData, decoded.RawData);
    }

    [Fact]
    [DisplayName("ASCII 模式 D000 子头校验")]
    public void Read_InvalidSubHeader_ReturnsFalse_Ascii()
    {
        // 잘못된 서브헤드 "E000" 사용
        var binary = new Byte[]
        {
            0xE0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        };
        var data = BinaryToAsciiText(binary);

        var response = new MCResponse { DataFormat = MCDataFormat.Ascii };
        var ok = response.Read(new MemoryStream(data), null);

        Assert.False(ok);
    }
}
