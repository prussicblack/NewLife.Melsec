using System;
using System.ComponentModel;
using System.IO;
using NewLife;
using NewLife.Melsec.Protocols;
using Xunit;

namespace XUnitTest;

/// <summary>MC 프로토콜 3E 프레임 ASCII 모드 직렬화/역직렬화 테스트</summary>
public class MCMessageAsciiTests
{
    #region BuildReadWord

    [Fact]
    [DisplayName("ASCII ReadWord request D100×4 encoding")]
    public void BuildReadWord_D100_4Words_Ascii()
    {
        // 먼저 바이너리 모드 메시지를 만들어 바이너리 바이트를 얻은 뒤, ASCII 모드가 그와 동일한지 검증
        var binaryMsg = MCMessage.BuildReadWord(DeviceCode.D, 100, 4);
        var expectedHexText = binaryMsg.ToBytes().ToHex();

        var asciiMsg = MCMessage.BuildReadWord(DeviceCode.D, 100, 4);
        asciiMsg.DataFormat = MCDataFormat.Ascii;
        var actual = asciiMsg.ToBytes();

        // ASCII 모드에서 ToBytes는 ASCII 16진수 텍스트의 바이트를 반환
        var actualText = System.Text.Encoding.ASCII.GetString(actual);
        Assert.Equal(expectedHexText, actualText);
    }

    [Fact]
    [DisplayName("ASCII ReadWord request round-trip")]
    public void BuildReadWord_RoundTrip_Ascii()
    {
        var original = MCMessage.BuildReadWord(DeviceCode.D, 100, 4);
        original.DataFormat = MCDataFormat.Ascii;
        var bytes = original.ToBytes();

        // 역직렬화 시 ASCII 모드 사용
        var decoded = new MCMessage { DataFormat = MCDataFormat.Ascii };
        var ok = decoded.Read(new MemoryStream(bytes), null);

        Assert.True(ok);
        Assert.Equal(original.Command, decoded.Command);
        Assert.Equal(original.SubCommand, decoded.SubCommand);
        Assert.Equal(original.DeviceCode, decoded.DeviceCode);
        Assert.Equal(original.StartAddress, decoded.StartAddress);
        Assert.Equal(original.Count, decoded.Count);
        Assert.Equal(original.NetworkNo, decoded.NetworkNo);
        Assert.Equal(original.PCNo, decoded.PCNo);
    }

    [Fact]
    [DisplayName("ASCII ReadWord request properties")]
    public void BuildReadWord_Properties_Ascii()
    {
        var msg = MCMessage.BuildReadWord(DeviceCode.D, 100, 4);
        msg.DataFormat = MCDataFormat.Ascii;

        Assert.Equal(MCMessage.CMD_READ, msg.Command);
        Assert.Equal(MCMessage.SUBCMD_WORD, msg.SubCommand);
        Assert.Equal(DeviceCode.D, msg.DeviceCode);
        Assert.Equal(100, msg.StartAddress);
        Assert.Equal(4, msg.Count);
        Assert.Null(msg.WriteData);
        Assert.Equal(MCDataFormat.Ascii, msg.DataFormat);
    }

    #endregion

    #region BuildReadBit

    [Fact]
    [DisplayName("ASCII ReadBit request M200×8 encoding")]
    public void BuildReadBit_M200_8Bits_Ascii()
    {
        var binaryMsg = MCMessage.BuildReadBit(DeviceCode.M, 200, 8);
        var expectedHexText = binaryMsg.ToBytes().ToHex();

        var asciiMsg = MCMessage.BuildReadBit(DeviceCode.M, 200, 8);
        asciiMsg.DataFormat = MCDataFormat.Ascii;
        var actual = asciiMsg.ToBytes();

        var actualText = System.Text.Encoding.ASCII.GetString(actual);
        Assert.Equal(expectedHexText, actualText);
    }

    [Fact]
    [DisplayName("ASCII ReadBit request round-trip")]
    public void BuildReadBit_RoundTrip_Ascii()
    {
        var original = MCMessage.BuildReadBit(DeviceCode.M, 200, 16);
        original.DataFormat = MCDataFormat.Ascii;
        var bytes = original.ToBytes();

        var decoded = new MCMessage { DataFormat = MCDataFormat.Ascii };
        Assert.True(decoded.Read(new MemoryStream(bytes), null));

        Assert.Equal(original.Command, decoded.Command);
        Assert.Equal(original.SubCommand, decoded.SubCommand);
        Assert.Equal(original.DeviceCode, decoded.DeviceCode);
        Assert.Equal(original.StartAddress, decoded.StartAddress);
        Assert.Equal(original.Count, decoded.Count);
    }

    #endregion

    #region BuildWriteWord

    [Fact]
    [DisplayName("ASCII WriteWord request D100 write 3 words encoding")]
    public void BuildWriteWord_D100_3Values_Ascii()
    {
        var binaryMsg = MCMessage.BuildWriteWord(DeviceCode.D, 100, new UInt16[] { 1, 2, 3 });
        var expectedHexText = binaryMsg.ToBytes().ToHex();

        var asciiMsg = MCMessage.BuildWriteWord(DeviceCode.D, 100, new UInt16[] { 1, 2, 3 });
        asciiMsg.DataFormat = MCDataFormat.Ascii;
        var actual = asciiMsg.ToBytes();

        var actualText = System.Text.Encoding.ASCII.GetString(actual);
        Assert.Equal(expectedHexText, actualText);
    }

    [Fact]
    [DisplayName("ASCII WriteWord request round-trip")]
    public void BuildWriteWord_RoundTrip_Ascii()
    {
        var original = MCMessage.BuildWriteWord(DeviceCode.D, 100, new UInt16[] { 1, 2, 3 });
        original.DataFormat = MCDataFormat.Ascii;
        var bytes = original.ToBytes();

        var decoded = new MCMessage { DataFormat = MCDataFormat.Ascii };
        Assert.True(decoded.Read(new MemoryStream(bytes), null));

        Assert.Equal(original.Command, decoded.Command);
        Assert.Equal(original.SubCommand, decoded.SubCommand);
        Assert.Equal(original.DeviceCode, decoded.DeviceCode);
        Assert.Equal(original.StartAddress, decoded.StartAddress);
        Assert.Equal(original.Count, decoded.Count);
        Assert.Equal(original.WriteData, decoded.WriteData);
    }

    #endregion

    #region BuildWriteBit

    [Fact]
    [DisplayName("ASCII WriteBit request encoding")]
    public void BuildWriteBit_Y0_4Bits_Ascii()
    {
        // Y0=0x9D, addr=0, count=4, values=[1,0,1,0]
        // 바이너리 비트 쓰기 데이터: [0x01, 0x01] (바이트당 하위 4비트=1번째 포인트, 상위 4비트=2번째 포인트, 2포인트/바이트)
        var msg = MCMessage.BuildWriteBit(DeviceCode.Y, 0, new UInt16[] { 1, 0, 1, 0 });
        msg.DataFormat = MCDataFormat.Ascii;
        var bytes = msg.ToBytes();

        // Round-trip 검증
        var decoded = new MCMessage { DataFormat = MCDataFormat.Ascii };
        Assert.True(decoded.Read(new MemoryStream(bytes), null));

        Assert.Equal(msg.Command, decoded.Command);
        Assert.Equal(msg.SubCommand, decoded.SubCommand);
        Assert.Equal(msg.DeviceCode, decoded.DeviceCode);
        Assert.Equal(msg.Count, decoded.Count);
        Assert.Equal(msg.WriteData, decoded.WriteData);
    }

    #endregion

    #region 경계 상황

    [Fact]
    [DisplayName("ASCII mode non-5000 sub-header returns false")]
    public void Read_InvalidSubHeader_ReturnsFalse_Ascii()
    {
        // 잘못된 서브헤드 "6000" 사용, 나머지 바이트는 유효 프레임으로 채움
        var hex = "600000FFFF03000C000A000104000000640000A80400";
        var invalidBytes = System.Text.Encoding.ASCII.GetBytes(hex);

        var msg = new MCMessage { DataFormat = MCDataFormat.Ascii };
        var ok = msg.Read(new MemoryStream(invalidBytes), null);

        Assert.False(ok);
    }

    [Fact]
    [DisplayName("ASCII and binary modes produce equivalent frame content")]
    public void AsciiAndBinary_EquivalentContent()
    {
        // ASCII와 바이너리 모드는 동일한 필드 값을 가져야 한다
        var binaryMsg = MCMessage.BuildReadWord(DeviceCode.D, 100, 4);
        var asciiMsg = MCMessage.BuildReadWord(DeviceCode.D, 100, 4);
        asciiMsg.DataFormat = MCDataFormat.Ascii;

        var binaryBytes = binaryMsg.ToBytes();
        var asciiBytes = asciiMsg.ToBytes();

        // ASCII 모드는 바이너리 모드보다 바이트 수가 많아야 한다
        Assert.True(asciiBytes.Length > binaryBytes.Length);

        // ASCII 모드는 ASCII 16진수 텍스트를 출력하므로, 텍스트를 복호화하면 바이너리 모드와 일치해야 한다
        var asciiHexString = System.Text.Encoding.ASCII.GetString(asciiBytes);
        var decodedFromAscii = asciiHexString.ToHex();
        Assert.Equal(binaryBytes, decodedFromAscii);
    }

    #endregion
}
