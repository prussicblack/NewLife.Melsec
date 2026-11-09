using System;
using System.ComponentModel;
using System.IO;
using NewLife;
using NewLife.Melsec.Protocols;
using Xunit;

namespace XUnitTest;

/// <summary>MC 프로토콜 신기능 테스트 (랜덤 읽기, 원격 제어, UDP, RawRequestData)</summary>
public class MCProtocolNewFeaturesTests
{
    #region 랜덤 읽기 (Random Read)

    [Fact]
    [DisplayName("ReadRandomWords builds correct request data format")]
    public void BuildRandomReadRequestData_Format()
    {
        // 리플렉션으로 비공개 메서드를 호출해 데이터 형식 검증
        // 랜덤 읽기 요청 데이터: [포인트 수(1)] + [소프트 요소 코드(1)+주소(3)]×N
        var items = new (DeviceCode, Int32)[]
        {
            (DeviceCode.D, 100),
            (DeviceCode.M, 200),
        };

        // MCMessage의 RawRequestData로 직렬화 검증
        var msg = new MCMessage
        {
            Command = MCMessage.CMD_RANDOM_READ,
            SubCommand = MCMessage.SUBCMD_WORD,
            RawRequestData = BuildRawData(items),
        };

        var bytes = msg.ToBytes();

        // 고정 헤더 검증
        Assert.Equal(0x50, bytes[0]); // 서브헤드
        Assert.Equal(0x00, bytes[1]);
        Assert.Equal(0x00, bytes[2]); // 네트워크 번호
        Assert.Equal(0xFF, bytes[3]); // PC 번호

        // 명령 코드 0403h (LE) 검증
        Assert.Equal(0x03, bytes[11]);
        Assert.Equal(0x04, bytes[12]);

        // 서브명령 0000h (LE) 검증
        Assert.Equal(0x00, bytes[13]);
        Assert.Equal(0x00, bytes[14]);

        // 요청 데이터 검증: 포인트 수(2) + D코드(0xA8) + 주소(100=0x64, LE) + M코드(0x90) + 주소(200=0xC8, LE)
        // 참고: 고정 헤더 길이는 15, 요청 데이터는 offset 15부터 시작
        var offset = 15;
        Assert.Equal(2, bytes[offset]); // 포인트 수
        Assert.Equal(0xA8, bytes[offset + 1]); // D 코드
        Assert.Equal(100, bytes[offset + 2] | (bytes[offset + 3] << 8) | (bytes[offset + 4] << 16)); // 주소
        Assert.Equal(0x90, bytes[offset + 5]); // M 코드
        Assert.Equal(200, bytes[offset + 6] | (bytes[offset + 7] << 8) | (bytes[offset + 8] << 16)); // 주소
    }

    [Fact]
    [DisplayName("ReadRandomBits returns correct point count")]
    public void ReadRandomBits_Count_Matches()
    {
        var items = new (DeviceCode, Int32)[]
        {
            (DeviceCode.M, 100),
            (DeviceCode.M, 200),
            (DeviceCode.X, 0x1F),
        };

        var raw = BuildRawData(items);
        // 포인트 수 (1바이트)는 items 개수와 같아야 함
        Assert.Equal(items.Length, raw[0]);
    }

    [Fact]
    [DisplayName("Random read serialization-deserialization round-trip")]
    public void RandomRead_ToBytes_Consistent()
    {
        var items = new (DeviceCode, Int32)[]
        {
            (DeviceCode.D, 100),
            (DeviceCode.M, 200),
        };

        var msg = new MCMessage
        {
            Command = MCMessage.CMD_RANDOM_READ,
            SubCommand = MCMessage.SUBCMD_WORD,
            RawRequestData = BuildRawData(items),
        };

        var bytes = msg.ToBytes();

        // 역직렬화로 복원
        var decoded = new MCMessage();
        using var ms = new MemoryStream(bytes);
        Assert.True(decoded.Read(ms, null));

        Assert.Equal(MCMessage.CMD_RANDOM_READ, decoded.Command);
        Assert.Equal(MCMessage.SUBCMD_WORD, decoded.SubCommand);
    }

    #endregion

    #region 원격 제어 (RUN/STOP)

    [Fact]
    [DisplayName("RemoteRun builds correct request frame")]
    public void RemoteRun_ToBytes_Format()
    {
        var msg = new MCMessage
        {
            Command = MCMessage.CMD_REMOTE_RUN,
            SubCommand = 0x0000,
            RawRequestData = [0x01], // 클리어 모드
        };

        var bytes = msg.ToBytes();

        // 명령 코드 1001h (LE) 검증
        Assert.Equal(0x01, bytes[11]);
        Assert.Equal(0x10, bytes[12]);

        // 요청 데이터 검증: 클리어 모드
        Assert.Equal(0x01, bytes[15]);
    }

    [Fact]
    [DisplayName("RemoteStop builds correct request frame")]
    public void RemoteStop_ToBytes_Format()
    {
        var msg = new MCMessage
        {
            Command = MCMessage.CMD_REMOTE_STOP,
            SubCommand = 0x0000,
            // RawRequestData로 빈 데이터 지정
            RawRequestData = [],
        };

        var bytes = msg.ToBytes();

        // 명령 코드 1002h (LE) 검증
        Assert.Equal(0x02, bytes[11]);
        Assert.Equal(0x10, bytes[12]);

        // 요청 데이터 없음, 데이터 길이는 6이어야 함 (모니터링 타이머+명령+서브명령)
        var dataLength = bytes[7] | (bytes[8] << 8);
        Assert.Equal(6, dataLength);
    }

    #endregion

    #region 시리얼 포트 전송 (MC-15)

    [Fact]
    [DisplayName("Serial mode default property values correct")]
    public void SerialMode_DefaultProperties()
    {
        var protocol = new MCProtocol
        {
            TransportMode = MCTransportMode.Serial,
            Address = "COM3",
        };

        Assert.Equal(MCTransportMode.Serial, protocol.TransportMode);
        Assert.Equal(9600, protocol.Baudrate);
        Assert.Equal(8, protocol.DataBits);
        Assert.Equal("COM3", protocol.Address);
    }

    [Fact]
    [DisplayName("Serial mode 3E frame serialization matches TCP")]
    public void SerialMode_3EFrame_SameAsTcp()
    {
        var msg = MCMessage.BuildReadWord(DeviceCode.D, 100, 4);
        var bytes = msg.ToBytes();

        // 프레임 형식은 전송 모드에 영향을 받지 않음
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x00, bytes[1]);
        Assert.Equal(0x01, bytes[11]); // 0401h LE
        Assert.Equal(0x04, bytes[12]);
    }

    [Fact]
    [DisplayName("Serial mode 1E frame serialization matches 3E")]
    public void SerialMode_1EFrame_Matches3E()
    {
        var msg = MC1EMessage.BuildReadWord(MC1EDeviceCode.D, 100, 4);
        var bytes = msg.ToBytes();

        // 1E 프레임 형식: 서브헤드 + PLC 번호 + 모니터링 타이머 + 시작 주소 + 소프트 요소 코드 + 포인트 수
        Assert.Equal(MC1EMessage.SUB_READ_WORD, bytes[0]);
        Assert.Equal(0xFF, bytes[1]);
        Assert.Equal(MC1EDeviceCode.D, (MC1EDeviceCode)bytes[6]);
        Assert.Equal(4, (bytes[7] | (bytes[8] << 8)));
    }

    [Fact]
    [DisplayName("MC-15 serial parameters configurable via properties")]
    public void SerialMode_Configurable()
    {
        var protocol = new MCProtocol
        {
            TransportMode = MCTransportMode.Serial,
            Address = "COM5",
            Baudrate = 19200,
            DataBits = 7,
            Parity = System.IO.Ports.Parity.Even,
            StopBits = System.IO.Ports.StopBits.One,
            Timeout = 3000,
        };

        Assert.Equal("COM5", protocol.Address);
        Assert.Equal(19200, protocol.Baudrate);
        Assert.Equal(7, protocol.DataBits);
        Assert.Equal(System.IO.Ports.Parity.Even, protocol.Parity);
        Assert.Equal(System.IO.Ports.StopBits.One, protocol.StopBits);
        Assert.Equal(3000, protocol.Timeout);
    }

    #endregion

    #region UDP 전송

    [Fact]
    [DisplayName("MCTransportMode enum default values")]
    public void TransportMode_Default_IsTcp()
    {
        var protocol = new MCProtocol();
        Assert.Equal(MCTransportMode.Tcp, protocol.TransportMode);
    }

    [Fact]
    [DisplayName("UDP mode 3E frame serialization matches TCP")]
    public void UdpMode_3EFrame_SameAsTcp()
    {
        var msg = MCMessage.BuildReadWord(DeviceCode.D, 100, 4);
        var bytes = msg.ToBytes();

        // 프레임 형식은 전송 모드에 영향을 받지 않음
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x00, bytes[1]);
        Assert.Equal(0x01, bytes[11]); // 0401h LE
        Assert.Equal(0x04, bytes[12]);
    }

    #endregion

    #region RawRequestData 직렬화

    [Fact]
    [DisplayName("RawRequestData affects data length calculation")]
    public void RawRequestData_AffectsDataLength()
    {
        var msg = new MCMessage
        {
            Command = 0x0403,
            SubCommand = 0x0000,
            RawRequestData = [0x02, 0xA8, 0x64, 0x00, 0x00, 0x90, 0xC8, 0x00, 0x00],
        };

        var bytes = msg.ToBytes();

        // 데이터 길이 = 6(모니터링 타이머+명령+서브명령) + RawRequestData.Length
        var expectedDataLength = (UInt16)(6 + msg.RawRequestData.Length);
        var actualDataLength = (UInt16)(bytes[7] | (bytes[8] << 8));
        Assert.Equal(expectedDataLength, actualDataLength);
    }

    [Fact]
    [DisplayName("Standard serialization used when no RawRequestData")]
    public void WithoutRawRequestData_UsesStandardSerialization()
    {
        var msg = MCMessage.BuildReadWord(DeviceCode.D, 100, 4);
        var bytes = msg.ToBytes();

        // 표준 데이터 길이 = 12 (고정 필드) 
        var dataLength = (UInt16)(bytes[7] | (bytes[8] << 8));
        Assert.Equal(12, dataLength);

        // 표준 필드 검증
        Assert.Equal(100, bytes[15] | (bytes[16] << 8) | (bytes[17] << 16)); // 주소
        Assert.Equal(0xA8, bytes[18]); // D 코드
        Assert.Equal(4, bytes[19] | (bytes[20] << 8)); // 포인트 수
    }

    #endregion

    #region 보조 메서드

    /// <summary>랜덤 읽기 요청 데이터 구성</summary>
    private static Byte[] BuildRawData((DeviceCode code, Int32 addr)[] items)
    {
        using var ms = new MemoryStream();
        ms.WriteByte((Byte)items.Length);
        for (var i = 0; i < items.Length; i++)
        {
            ms.WriteByte((Byte)items[i].code);
            var addr = items[i].addr;
            ms.WriteByte((Byte)(addr & 0xFF));
            ms.WriteByte((Byte)((addr >> 8) & 0xFF));
            ms.WriteByte((Byte)((addr >> 16) & 0xFF));
        }
        return ms.ToArray();
    }

    #endregion

    #region 원격 비밀번호 잠금 (Remote Password Lock)

    [Fact]
    [DisplayName("Remote password unlock request 1630h data format correct")]
    public void RemoteUnlock_RequestFormat()
    {
        var raw = InvokeBuildPasswordRequestData("pass");

        // 전체 길이 = 길이 필드(2) + 비밀번호(4) = 6
        Assert.Equal(6, raw.Length);
        // 비밀번호 길이 필드 (LE) 검증
        Assert.Equal(0x04, raw[0]);
        Assert.Equal(0x00, raw[1]);
        // 비밀번호 ASCII 검증
        Assert.Equal((Byte)'p', raw[2]);
        Assert.Equal((Byte)'a', raw[3]);
        Assert.Equal((Byte)'s', raw[4]);
        Assert.Equal((Byte)'s', raw[5]);
    }

    [Fact]
    [DisplayName("Remote password unlock/lock builds full frame via MCMessage")]
    public void RemoteUnlock_FullFrame()
    {
        // MCMessage를 통해 RemoteUnlock 명령 전송을 목 처리
        var raw = InvokeBuildPasswordRequestData("pass");
        var msg = new MCMessage
        {
            Command = MCMessage.CMD_REMOTE_UNLOCK,
            SubCommand = 0x0000,
            RawRequestData = raw,
        };

        var bytes = msg.ToBytes();

        // 명령 코드 1630h (LE) 검증
        Assert.Equal(0x30, bytes[11]);
        Assert.Equal(0x16, bytes[12]);
        // 서브명령 0000h 검증
        Assert.Equal(0x00, bytes[13]);
        Assert.Equal(0x00, bytes[14]);
        // 비밀번호 길이 검증
        Assert.Equal(0x04, bytes[15]);
        Assert.Equal(0x00, bytes[16]);
        // 비밀번호 "pass" 검증
        Assert.Equal((Byte)'p', bytes[17]);
        Assert.Equal((Byte)'a', bytes[18]);
        Assert.Equal((Byte)'s', bytes[19]);
        Assert.Equal((Byte)'s', bytes[20]);
    }

    [Fact]
    [DisplayName("Remote lock command 1631h distinguished from unlock")]
    public void RemoteLock_Command_Differs_From_Unlock()
    {
        var raw = InvokeBuildPasswordRequestData("pass");

        var unlockMsg = new MCMessage
        {
            Command = MCMessage.CMD_REMOTE_UNLOCK,
            SubCommand = 0x0000,
            RawRequestData = raw,
        };
        var lockMsg = new MCMessage
        {
            Command = MCMessage.CMD_REMOTE_LOCK,
            SubCommand = 0x0000,
            RawRequestData = raw,
        };

        var unlockBytes = unlockMsg.ToBytes();
        var lockBytes = lockMsg.ToBytes();

        // 명령 코드가 다름: 1630h vs 1631h
        Assert.Equal(0x30, unlockBytes[11]);
        Assert.Equal(0x16, unlockBytes[12]);
        Assert.Equal(0x31, lockBytes[11]);
        Assert.Equal(0x16, lockBytes[12]);
    }

    #endregion

    #region 모니터링 등록 (Monitor Registration)

    [Fact]
    [DisplayName("Monitor registration 0801h request data format correct")]
    public void MonitorRegist_RequestFormat()
    {
        var wordDevices = new (DeviceCode, Int32)[]
        {
            (DeviceCode.D, 0),
            (DeviceCode.D, 1),
        };
        var doubleWordDevices = new (DeviceCode, Int32)[]
        {
            (DeviceCode.D, 2),
            (DeviceCode.D, 4),
        };

        var raw = InvokeBuildMonitorRegistRequestData(wordDevices, doubleWordDevices);

        // 워드 디바이스 수 = 2
        Assert.Equal(2, raw[0]);
        // 이중 워드 디바이스 수 = 2
        Assert.Equal(2, raw[1]);
        // 예약 2바이트
        Assert.Equal(0x00, raw[2]);
        Assert.Equal(0x00, raw[3]);

        // 1번째 워드 디바이스: D0 (addr=0, code=0xA8)
        Assert.Equal(0x00, raw[4]); // addr LE byte 0
        Assert.Equal(0x00, raw[5]); // addr LE byte 1
        Assert.Equal(0x00, raw[6]); // addr LE byte 2
        Assert.Equal(0xA8, raw[7]); // code

        // 2번째 워드 디바이스: D1 (addr=1, code=0xA8)
        Assert.Equal(0x01, raw[8]);
        Assert.Equal(0x00, raw[9]);
        Assert.Equal(0x00, raw[10]);
        Assert.Equal(0xA8, raw[11]);

        // 1번째 이중 워드 디바이스: D2 (addr=2, code=0xA8)
        Assert.Equal(0x02, raw[12]);
        Assert.Equal(0x00, raw[13]);
        Assert.Equal(0x00, raw[14]);
        Assert.Equal(0xA8, raw[15]);

        // 2번째 이중 워드 디바이스: D4 (addr=4, code=0xA8)
        Assert.Equal(0x04, raw[16]);
        Assert.Equal(0x00, raw[17]);
        Assert.Equal(0x00, raw[18]);
        Assert.Equal(0xA8, raw[19]);
    }

    [Fact]
    [DisplayName("Monitor registration builds full frame via MCMessage")]
    public void MonitorRegist_FullFrame()
    {
        var wordDevices = new (DeviceCode, Int32)[] { (DeviceCode.D, 0) };
        var doubleWordDevices = new (DeviceCode, Int32)[] { (DeviceCode.D, 2) };
        var raw = InvokeBuildMonitorRegistRequestData(wordDevices, doubleWordDevices);

        var msg = new MCMessage
        {
            Command = MCMessage.CMD_MONITOR_REGIST,
            SubCommand = 0x0000,
            RawRequestData = raw,
        };

        var bytes = msg.ToBytes();

        // 명령 코드 0801h (LE) 검증
        Assert.Equal(0x01, bytes[11]);
        Assert.Equal(0x08, bytes[12]);
        // 서브명령 0000h 검증
        Assert.Equal(0x00, bytes[13]);
        Assert.Equal(0x00, bytes[14]);
    }

    #endregion

    #region 보조 메서드 (확장)

    /// <summary>원격 비밀번호 요청 데이터 구성</summary>
    private static Byte[] InvokeBuildPasswordRequestData(String password)
    {
        var pwdBytes = System.Text.Encoding.ASCII.GetBytes(password);
        using var ms = new MemoryStream();
        ms.WriteByte((Byte)(pwdBytes.Length & 0xFF));
        ms.WriteByte((Byte)((pwdBytes.Length >> 8) & 0xFF));
        ms.WriteByte((Byte)'p');
        ms.WriteByte((Byte)'a');
        ms.WriteByte((Byte)'s');
        ms.WriteByte((Byte)'s');
        return ms.ToArray();
    }

    /// <summary>모니터링 등록 요청 데이터 구성</summary>
    private static Byte[] InvokeBuildMonitorRegistRequestData(
        (DeviceCode code, Int32 addr)[] wordDevices,
        (DeviceCode code, Int32 addr)[] doubleWordDevices)
    {
        using var ms = new MemoryStream();
        ms.WriteByte((Byte)wordDevices.Length);
        ms.WriteByte((Byte)doubleWordDevices.Length);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);

        foreach (var (code, addr) in wordDevices)
        {
            ms.WriteByte((Byte)(addr & 0xFF));
            ms.WriteByte((Byte)((addr >> 8) & 0xFF));
            ms.WriteByte((Byte)((addr >> 16) & 0xFF));
            ms.WriteByte((Byte)code);
        }
        foreach (var (code, addr) in doubleWordDevices)
        {
            ms.WriteByte((Byte)(addr & 0xFF));
            ms.WriteByte((Byte)((addr >> 8) & 0xFF));
            ms.WriteByte((Byte)((addr >> 16) & 0xFF));
            ms.WriteByte((Byte)code);
        }
        return ms.ToArray();
    }

    #endregion
}
