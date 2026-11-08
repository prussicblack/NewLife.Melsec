using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NewLife.IoT.Drivers;
using NewLife.IoT.ThingModels;
using NewLife.Melsec.Drivers;
using NewLife.Melsec.Protocols;
using Xunit;

namespace XUnitTest;

/// <summary>MC 프로토콜 엔드투엔드 통합 테스트 (내장 목 TCP 서버)</summary>
public class MCIntegrationTests : IDisposable
{
    #region 목 TCP 서버

    private readonly TcpListener _listener;
    private readonly Int32 _port;
    private readonly CancellationTokenSource _cts = new();

    /// <summary>요청을 받을 때마다 호출되는 처리 함수. 응답 바이트를 반환</summary>
    private Func<Byte[], Byte[]> _handler;

    public MCIntegrationTests()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        StartAcceptLoop();
    }

    /// <summary>이번 요청의 응답 핸들러 설정</summary>
    private void SetHandler(Func<Byte[], Byte[]> handler) => _handler = handler;

    private void StartAcceptLoop()
    {
        Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_cts.Token);
                }
                catch
                {
                    break;
                }

                _ = Task.Run(() => HandleClient(client), _cts.Token);
            }
        }, _cts.Token);
    }

    private void HandleClient(TcpClient client)
    {
        using (client)
        using (var ns = client.GetStream())
        {
            try
            {
                while (client.Connected)
                {
                    // 요청 프레임 헤더 9바이트 읽기
                    var header = ReadFully(ns, MCResponse.FIXED_HEADER_LEN);
                    if (header == null) break;

                    // 바이트 7~8에서 데이터 길이 읽기 (실제로는 요청 프레임의 DataLength)
                    // 요청 프레임 형식 [7~8] = DataLength (모니터링 타이머(2)+명령(2)+서브명령(2)+주소(3)+코드(1)+포인트 수(2) = 12 + 쓰기 데이터)
                    var dataLength = header[7] | (header[8] << 8);
                    var data = ReadFully(ns, dataLength);
                    if (data == null) break;

                    var all = new Byte[header.Length + data.Length];
                    Array.Copy(header, 0, all, 0, header.Length);
                    Array.Copy(data, 0, all, header.Length, data.Length);

                    var handler = _handler;
                    if (handler == null) break;

                    var response = handler(all);
                    if (response == null) break;

                    ns.Write(response, 0, response.Length);
                    ns.Flush();
                }
            }
            catch
            {
                // 연결이 끊어졌으므로 정상 종료
            }
        }
    }

    private static Byte[] ReadFully(Stream stream, Int32 count)
    {
        var buf = new Byte[count];
        var total = 0;
        while (total < count)
        {
            var n = stream.Read(buf, total, count - total);
            if (n <= 0) return null;
            total += n;
        }
        return buf;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
    }

    #endregion

    #region MCProtocol 통합 테스트

    [Fact]
    [DisplayName("ReadWords D100×4 完整请求响应流程")]
    public void ReadWords_D100_4Words_EndToEnd()
    {
        UInt16[] expectedValues = [0x0011, 0x0022, 0x0033, 0x0044];

        SetHandler(request =>
        {
            // 요청 프레임 형식 검증
            Assert.Equal(0x50, request[0]);
            Assert.Equal(0x00, request[1]);

            // 4개의 워드 응답 반환
            return MCResponse.BuildWordResponse(expectedValues).ToBytes();
        });

        using var protocol = new MCProtocol { Address = $"127.0.0.1:{_port}", Timeout = 3000 };
        protocol.Open();

        var words = protocol.ReadWords(DeviceCode.D, 100, 4);

        Assert.Equal(4, words.Length);
        Assert.Equal(0x0011, words[0]);
        Assert.Equal(0x0022, words[1]);
        Assert.Equal(0x0033, words[2]);
        Assert.Equal(0x0044, words[3]);
    }

    [Fact]
    [DisplayName("ReadBits M200×8 完整请求响应流程")]
    public void ReadBits_M200_8Bits_EndToEnd()
    {
        Boolean[] expectedBits = [true, false, true, true, false, false, true, false];

        SetHandler(request =>
        {
            Assert.Equal(0x50, request[0]);
            return MCResponse.BuildBitResponse(expectedBits).ToBytes();
        });

        using var protocol = new MCProtocol { Address = $"127.0.0.1:{_port}", Timeout = 3000 };
        protocol.Open();

        var bits = protocol.ReadBits(DeviceCode.M, 200, 8);

        Assert.Equal(8, bits.Length);
        Assert.Equal(expectedBits, bits);
    }

    [Fact]
    [DisplayName("WriteWords D100 完整请求响应流程")]
    public void WriteWords_D100_EndToEnd()
    {
        UInt16[] valuesToWrite = [0x1234, 0xABCD];

        SetHandler(request =>
        {
            // 쓰기 명령인지 검증
            // 프레임 구조: header(9) + MonTimer(2) + Cmd(2) + SubCmd(2) + ...
            // offset 9 = MonTimer, 11 = Cmd low byte
            var cmdLow = request[11];
            var cmdHigh = request[12];
            var cmd = (UInt16)(cmdLow | (cmdHigh << 8));
            Assert.Equal(MCMessage.CMD_WRITE, cmd);

            return MCResponse.BuildWriteResponse().ToBytes();
        });

        using var protocol = new MCProtocol { Address = $"127.0.0.1:{_port}", Timeout = 3000 };
        protocol.Open();

        // 예외가 발생하지 않으면 통과
        protocol.WriteWords(DeviceCode.D, 100, valuesToWrite);
    }

    [Fact]
    [DisplayName("WriteBits M0 完整请求响应流程")]
    public void WriteBits_M0_EndToEnd()
    {
        Boolean[] bitsToWrite = [true, false, true];

        SetHandler(request =>
        {
            Assert.Equal(0x50, request[0]);
            return MCResponse.BuildWriteResponse().ToBytes();
        });

        using var protocol = new MCProtocol { Address = $"127.0.0.1:{_port}", Timeout = 3000 };
        protocol.Open();

        protocol.WriteBits(DeviceCode.M, 0, bitsToWrite);
    }

    [Fact]
    [DisplayName("服务端返回错误码时 ReadWords 抛出 MCException")]
    public void ReadWords_ErrorResponse_ThrowsMCException()
    {
        SetHandler(_ => MCResponse.BuildErrorResponse(0xC056).ToBytes());

        using var protocol = new MCProtocol { Address = $"127.0.0.1:{_port}", Timeout = 3000 };
        protocol.Open();

        var ex = Assert.Throws<MCException>(() => protocol.ReadWords(DeviceCode.D, 100, 4));
        Assert.Equal(0xC056, ex.EndCode);
    }

    [Fact]
    [DisplayName("断线后重连 再次 ReadWords 成功")]
    public void Reconnect_AfterDisconnect_ReadWordsSucceeds()
    {
        var callCount = 0;

        SetHandler(request =>
        {
            callCount++;
            return MCResponse.BuildWordResponse([0x0001]).ToBytes();
        });

        using var protocol = new MCProtocol { Address = $"127.0.0.1:{_port}", Timeout = 3000 };
        protocol.Open();

        // 첫 번째 읽기
        var words1 = protocol.ReadWords(DeviceCode.D, 0, 1);
        Assert.Equal((UInt16)0x0001, words1[0]);

        // 연결을 수동으로 닫아 통신 두절 시뮬레이션
        protocol.Close();
        Assert.Equal(1, callCount);

        // 다시 열기 (재연결 시뮬레이션)
        protocol.Open();

        var words2 = protocol.ReadWords(DeviceCode.D, 0, 1);
        Assert.Equal((UInt16)0x0001, words2[0]);
        Assert.Equal(2, callCount);
    }

    #endregion

    #region MCDriver E2E 테스트

    [Fact]
    [DisplayName("MCDriver Read 通过真实 TCP 连接读取字软元件")]
    public void MCDriver_Read_WordDevices_EndToEnd()
    {
        UInt16[] serverValues = [0x0100, 0x0200, 0x0300];

        SetHandler(_ => MCResponse.BuildWordResponse(serverValues).ToBytes());

        var driver = new MCDriver();
        var p = new MCParameter { Address = $"127.0.0.1:{_port}", Timeout = 3000 };
        var node = driver.Open(null, p);

        var points = new IPoint[]
        {
            new PointModel { Name = "D100", Address = "D100" },
            new PointModel { Name = "D101", Address = "D101" },
            new PointModel { Name = "D102", Address = "D102" },
        };

        var rs = driver.Read(node, points);

        Assert.Equal(3, rs.Points.Length);
        Assert.Equal((UInt16)0x0100, rs.GetValue("D100"));
        Assert.Equal((UInt16)0x0200, rs.GetValue("D101"));
        Assert.Equal((UInt16)0x0300, rs.GetValue("D102"));

        driver.Close(node);
    }

    [Fact]
    [DisplayName("MCDriver Write 通过真实 TCP 连接写字软元件")]
    public void MCDriver_Write_WordDevice_EndToEnd()
    {
        SetHandler(_ => MCResponse.BuildWriteResponse().ToBytes());

        var driver = new MCDriver();
        var p = new MCParameter { Address = $"127.0.0.1:{_port}", Timeout = 3000 };
        var node = driver.Open(null, p);

        var pt = new PointModel { Name = "D100", Address = "D100", Type = "short" };
        var result = driver.Write(node, pt, (Int32)0x1234);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.AffectedCount);

        driver.Close(node);
    }

    [Fact]
    [DisplayName("MCDriver 先读字D段再读位M段 两次请求均成功")]
    public void MCDriver_Read_MixedWordAndBit_EndToEnd()
    {
        // 서버가 요청 순서에 따라 다른 응답을 반환: 1회차에는 D 세그먼트 워드 데이터, 2회차에는 M 세그먼트 비트 데이터
        var callCount = 0;
        SetHandler(request =>
        {
            callCount++;
            // 서브명령으로 워드/비트 요청 구분 (offset 13 = SubCmd 하위 바이트)
            var subCmd = request[13] | (request[14] << 8);
            if (subCmd == MCMessage.SUBCMD_WORD)
                return MCResponse.BuildWordResponse([0xAAAA, 0xBBBB]).ToBytes();
            else
                return MCResponse.BuildBitResponse([true, false, true]).ToBytes();
        });

        var driver = new MCDriver();
        var p = new MCParameter { Address = $"127.0.0.1:{_port}", Timeout = 3000 };
        var node = driver.Open(null, p);

        var points = new IPoint[]
        {
            new PointModel { Name = "D0",  Address = "D0"  },
            new PointModel { Name = "D1",  Address = "D1"  },
            new PointModel { Name = "M0",  Address = "M0"  },
            new PointModel { Name = "M1",  Address = "M1"  },
            new PointModel { Name = "M2",  Address = "M2"  },
        };

        var rs = driver.Read(node, points);

        Assert.Equal(2, callCount);
        Assert.Equal((UInt16)0xAAAA, rs.GetValue("D0"));
        Assert.Equal((UInt16)0xBBBB, rs.GetValue("D1"));
        Assert.Equal(true,  rs.GetValue("M0"));
        Assert.Equal(false, rs.GetValue("M1"));
        Assert.Equal(true,  rs.GetValue("M2"));

        driver.Close(node);
    }

    [Fact]
    [DisplayName("WriteWords 服务端返回错误码时抛出 MCException")]
    public void MCDriver_WriteWords_ErrorResponse_ThrowsMCException()
    {
        SetHandler(_ => MCResponse.BuildErrorResponse(0xC059).ToBytes());

        using var protocol = new MCProtocol { Address = $"127.0.0.1:{_port}", Timeout = 3000 };
        protocol.Open();

        var ex = Assert.Throws<MCException>(() => protocol.WriteWords(DeviceCode.D, 0, [0x1234]));
        Assert.Equal(0xC059, ex.EndCode);
    }

    [Fact]
    [DisplayName("WriteBits 服务端返回错误码时抛出 MCException")]
    public void MCDriver_WriteBits_ErrorResponse_ThrowsMCException()
    {
        SetHandler(_ => MCResponse.BuildErrorResponse(0xC058).ToBytes());

        using var protocol = new MCProtocol { Address = $"127.0.0.1:{_port}", Timeout = 3000 };
        protocol.Open();

        var ex = Assert.Throws<MCException>(() => protocol.WriteBits(DeviceCode.M, 0, [true]));
        Assert.Equal(0xC058, ex.EndCode);
    }

    #endregion
}
