using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NewLife;
using NewLife.IoT.Drivers;
using NewLife.IoT.ThingModels;
using NewLife.Melsec.Drivers;
using NewLife.Melsec.Protocols;
using NewLife.Security;
using Xunit;

namespace XUnitTest;

/// <summary>MCDriver 드라이버 계층 테스트 (Moq로 MCProtocol을 목 처리)</summary>
public class MCDriverTests
{
    #region Open / Close

    [Fact]
    [DisplayName("Open returns MelsecNode and Link is initialized")]
    public void Open_ReturnsMelsecNode()
    {
        var driver = new MCDriver();

        var p = new MCParameter { Address = "192.168.1.10:6000" };
        var node = driver.Open(null, p);

        var melsecNode = node as MelsecNode;
        Assert.NotNull(melsecNode);
        Assert.Null(melsecNode.Device);
        Assert.NotNull(driver.Link);
    }

    [Fact]
    [DisplayName("Open multiple times on the same address shares the same Link")]
    public void Open_MultipleNodes_ShareSameLink()
    {
        var driver = new MCDriver();

        var p = new MCParameter { Address = "192.168.1.10:6000" };
        var node1 = driver.Open(null, p) as MelsecNode;
        var link1 = driver.Link;

        var node2 = driver.Open(null, p) as MelsecNode;
        var link2 = driver.Link;

        Assert.NotEqual(node1, node2);
        Assert.Same(link1, link2);
        Assert.Equal(node1.Driver, node2.Driver);

        driver.Close(node1);
        driver.Close(node2);
    }

    [Fact]
    [DisplayName("Close releases Link when ref count drops to 0")]
    public void Close_RefCountZero_DisposesLink()
    {
        var driver = new MCDriver();

        var p = new MCParameter { Address = "192.168.1.10:6000" };
        var node1 = driver.Open(null, p);
        var node2 = driver.Open(null, p);

        var link = driver.Link;
        Assert.NotNull(link);

        driver.Close(node1);
        Assert.NotNull(driver.Link); // 노드가 하나 더 있음

        driver.Close(node2);
        Assert.Null(driver.Link);    // 마지막 노드가 닫힌 후 Link는 null이어야 함
    }

    [Fact]
    [DisplayName("CreateParameter empty string returns defaults")]
    public void CreateParameter_Null_ReturnsDefault()
    {
        var driver = new MCDriver();
        var p = driver.CreateParameter(null) as MCParameter;

        Assert.NotNull(p);
        Assert.Equal("192.168.1.10:6000", p.Address);
        Assert.Equal(5000, p.Timeout);
    }

    #endregion

    #region Read

    [Fact]
    [DisplayName("Read word devices D100-D104 returns correct result")]
    public void Read_WordRegisters_ReturnsReadResult()
    {
        var driver = new MCDriver();
        var p = new MCParameter { Address = "127.0.0.1:6000" };
        var node = driver.Open(null, p);

        // MCProtocol을 목 처리
        var mock = new Mock<MCProtocol> { CallBase = false };
        mock.Setup(e => e.ReadWords(DeviceCode.D, 100, It.IsAny<Int32>()))
            .Returns([0x0001, 0x0002, 0x0003, 0x0004, 0x0005]);
        driver.Link = mock.Object;

        var points = new List<IPoint>();
        for (var i = 0; i < 5; i++)
            points.Add(new PointModel { Name = "p" + i, Address = "D" + (100 + i) });

        var rs = driver.Read(node, [.. points]);

        Assert.NotNull(rs);
        Assert.Equal(5, rs.Points.Length);
        Assert.Equal((UInt16)0x0001, rs.GetValue("p0"));
        Assert.Equal((UInt16)0x0002, rs.GetValue("p1"));
        Assert.Equal((UInt16)0x0005, rs.GetValue("p4"));
    }

    [Fact]
    [DisplayName("Read bit devices M200-M207 returns correct result")]
    public void Read_BitDevices_ReturnsReadResult()
    {
        var driver = new MCDriver();
        var p = new MCParameter { Address = "127.0.0.1:6000" };
        var node = driver.Open(null, p);

        // MCProtocol을 목 처리
        var mock = new Mock<MCProtocol> { CallBase = false };
        mock.Setup(e => e.ReadBits(DeviceCode.M, 200, It.IsAny<Int32>()))
            .Returns([true, false, true, true, false, false, true, false]);
        driver.Link = mock.Object;

        var points = new List<IPoint>();
        for (var i = 0; i < 8; i++)
            points.Add(new PointModel { Name = "p" + i, Address = "M" + (200 + i) });

        var rs = driver.Read(node, [.. points]);

        Assert.Equal(8, rs.Points.Length);
        Assert.Equal(true,  rs.GetValue("p0"));
        Assert.Equal(false, rs.GetValue("p1"));
        Assert.Equal(true,  rs.GetValue("p2"));
        Assert.Equal(true,  rs.GetValue("p3"));
    }

    [Fact]
    [DisplayName("Read empty point collection returns empty result")]
    public void Read_EmptyPoints_ReturnsEmpty()
    {
        var driver = new MCDriver();
        var p = new MCParameter { Address = "127.0.0.1:6000" };
        var node = driver.Open(null, p);

        var mock = new Mock<MCProtocol> { CallBase = false };
        driver.Link = mock.Object;

        var rs = driver.Read(node, []);
        Assert.NotNull(rs);
        Assert.Empty(rs.Points);
    }

    #endregion

    #region Write

    [Fact]
    [DisplayName("Write word device D100 calls WriteWords and succeeds")]
    public void Write_WordRegister_CallsWriteWords()
    {
        var driver = new MCDriver();
        var p = new MCParameter { Address = "127.0.0.1:6000" };
        var node = driver.Open(null, p);

        var mock = new Mock<MCProtocol> { CallBase = false };
        mock.Setup(e => e.WriteWords(DeviceCode.D, 100, It.IsAny<UInt16[]>()));
        driver.Link = mock.Object;

        var pt = new PointModel { Name = "D100Test", Address = "D100", Type = "short" };
        var result = driver.Write(node, pt, (Int32)0x1234);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.AffectedCount);
        mock.Verify(e => e.WriteWords(DeviceCode.D, 100, It.IsAny<UInt16[]>()), Times.Once);
    }

    [Fact]
    [DisplayName("Write bit device M0 ON calls WriteBits and succeeds")]
    public void Write_BitDevice_CallsWriteBits()
    {
        var driver = new MCDriver();
        var p = new MCParameter { Address = "127.0.0.1:6000" };
        var node = driver.Open(null, p);

        var mock = new Mock<MCProtocol> { CallBase = false };
        mock.Setup(e => e.WriteBits(DeviceCode.M, 0, It.IsAny<Boolean[]>()));
        driver.Link = mock.Object;

        var pt = new PointModel { Name = "M0Test", Address = "M0" };
        var result = driver.Write(node, pt, true);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.AffectedCount);
        mock.Verify(e => e.WriteBits(DeviceCode.M, 0, It.IsAny<Boolean[]>()), Times.Once);
    }

    #endregion

    #region BuildSegments

    [Fact]
    [DisplayName("BuildSegments merges adjacent word addresses into one segment")]
    public void BuildSegments_AdjacentWordAddresses_MergeToOne()
    {
        var driver = new MCDriver();

        var points = Enumerable.Range(0, 5)
            .Select(i => (IPoint)new PointModel { Name = "p" + i, Address = "D" + (100 + i) })
            .ToList();

        var segs = driver.BuildSegments(points, new MCParameter());
        var seg0 = Assert.Single(segs);
        Assert.Equal(DeviceCode.D, seg0.Code);
        Assert.Equal(100, seg0.StartAddress);
        Assert.Equal(5, seg0.Count);
    }

    [Fact]
    [DisplayName("BuildSegments does not merge different device codes")]
    public void BuildSegments_DifferentDeviceCodes_NotMerged()
    {
        var driver = new MCDriver();

        var points = new List<IPoint>
        {
            new PointModel { Name = "d0", Address = "D100" },
            new PointModel { Name = "d1", Address = "D101" },
            new PointModel { Name = "m0", Address = "M0" },
            new PointModel { Name = "m1", Address = "M1" },
        };

        var segs = driver.BuildSegments(points, new MCParameter());
        Assert.Equal(2, segs.Count);
    }

    [Fact]
    [DisplayName("BuildSegments splits into multiple batches by BatchSize")]
    public void BuildSegments_BatchSize_SplitsSegments()
    {
        var driver = new MCDriver();

        var points = Enumerable.Range(0, 10)
            .Select(i => (IPoint)new PointModel { Name = "p" + i, Address = "D" + (100 + i) })
            .ToList();

        // BatchSize=4: 처음 4개 1배치, 5~8개 1배치, 마지막 2개 1배치
        var segs = driver.BuildSegments(points, new MCParameter { BatchSize = 4 });
        Assert.Equal(3, segs.Count);
        Assert.Equal(4, segs[0].Count);
        Assert.Equal(104, segs[1].StartAddress);
        Assert.Equal(4, segs[1].Count);
        Assert.Equal(108, segs[2].StartAddress);
        Assert.Equal(2, segs[2].Count);
    }

    [Fact]
    [DisplayName("BuildSegments does not merge non-contiguous addresses")]
    public void BuildSegments_NonAdjacentAddresses_NotMerged()
    {
        var driver = new MCDriver();

        var points = new List<IPoint>
        {
            new PointModel { Name = "p0", Address = "D100" },
            new PointModel { Name = "p1", Address = "D200" },
        };

        var segs = driver.BuildSegments(points, new MCParameter());
        Assert.Equal(2, segs.Count);
        Assert.Equal(100, segs[0].StartAddress);
        Assert.Equal(200, segs[1].StartAddress);
    }

    [Fact]
    [DisplayName("BuildSegments BatchStep=4 merges when gap is less than 4")]
    public void BuildSegments_BatchStep4_MergesWithinStep()
    {
        var driver = new MCDriver();

        var points = new List<IPoint>
        {
            new PointModel { Name = "p0", Address = "M0" },
            new PointModel { Name = "p1", Address = "M2" },
            new PointModel { Name = "p2", Address = "M4" },
            new PointModel { Name = "p3", Address = "M8" },
        };

        var segs = driver.BuildSegments(points, new MCParameter { BatchStep = 4 });
        var seg0 = Assert.Single(segs);
        Assert.Equal(0, seg0.StartAddress);
        Assert.Equal(9, seg0.Count); // 0 to 8 = 9 points span
    }

    #endregion

    #region Dispatch

    [Fact]
    [DisplayName("Dispatch word segment data correctly to each point")]
    public void Dispatch_WordSegment_CorrectlyMappedToPoints()
    {
        var driver = new MCDriver();

        var points = new IPoint[]
        {
            new PointModel { Name = "d100", Address = "D100" },
            new PointModel { Name = "d101", Address = "D101" },
            new PointModel { Name = "d102", Address = "D102" },
        };

        var seg = new MCDriver.MCSegment
        {
            Code = DeviceCode.D,
            StartAddress = 100,
            Count = 3,
            Words = [0x1111, 0x2222, 0x3333],
        };

        var dic = driver.Dispatch(points, [seg]);

        Assert.Equal((UInt16)0x1111, dic["d100"]);
        Assert.Equal((UInt16)0x2222, dic["d101"]);
        Assert.Equal((UInt16)0x3333, dic["d102"]);
    }

    [Fact]
    [DisplayName("Dispatch bit segment data correctly to each point")]
    public void Dispatch_BitSegment_CorrectlyMappedToPoints()
    {
        var driver = new MCDriver();

        var points = new IPoint[]
        {
            new PointModel { Name = "m0", Address = "M0" },
            new PointModel { Name = "m1", Address = "M1" },
            new PointModel { Name = "m2", Address = "M2" },
        };

        var seg = new MCDriver.MCSegment
        {
            Code = DeviceCode.M,
            StartAddress = 0,
            Count = 3,
            Bits = [true, false, true],
        };

        var dic = driver.Dispatch(points, [seg]);

        Assert.Equal(true, dic["m0"]);
        Assert.Equal(false, dic["m1"]);
        Assert.Equal(true, dic["m2"]);
    }

    [Fact]
    [DisplayName("Write Boolean true to bit device M0 calls WriteBits with correct value")]
    public void Write_BitDevice_Boolean_True_CallsWriteBits()
    {
        var driver = new MCDriver();
        var p = new MCParameter { Address = "127.0.0.1:6000" };
        var node = driver.Open(null, p);

        var mock = new Mock<MCProtocol> { CallBase = false };
        mock.Setup(e => e.WriteBits(DeviceCode.M, 0, It.IsAny<Boolean[]>()));
        driver.Link = mock.Object;

        var pt = new PointModel { Name = "M0Bit", Address = "M0" };
        var result = driver.Write(node, pt, true);

        Assert.True(result.IsSuccess);
        mock.Verify(e => e.WriteBits(DeviceCode.M, 0, It.Is<Boolean[]>(b => b.Length == 1 && b[0] == true)), Times.Once);
    }

    [Fact]
    [DisplayName("Read: other segments still return when one segment throws")]
    public void Read_SegmentThrows_OtherSegmentsStillReturn()
    {
        var driver = new MCDriver();
        var p = new MCParameter { Address = "127.0.0.1:6000" };
        var node = driver.Open(null, p);

        var mock = new Mock<MCProtocol> { CallBase = false };
        // D 세그먼트에서 예외 발생
        mock.Setup(e => e.ReadWords(DeviceCode.D, It.IsAny<Int32>(), It.IsAny<Int32>()))
            .Throws(new InvalidOperationException("D segment simulation failed"));
        // M 세그먼트는 정상
        mock.Setup(e => e.ReadBits(DeviceCode.M, 0, It.IsAny<Int32>()))
            .Returns([true, false]);
        driver.Link = mock.Object;

        var points = new List<IPoint>
        {
            new PointModel { Name = "d100", Address = "D100" },
            new PointModel { Name = "m0",   Address = "M0"   },
            new PointModel { Name = "m1",   Address = "M1"   },
        };

        // 예외가 발생하면 안 됨
        var rs = driver.Read(node, [.. points]);

        // D100은 예외로 값이 없고, M0/M1은 정상적으로 읽혀야 함
        Assert.Null(rs.GetValue("d100"));
        Assert.Equal(true,  rs.GetValue("m0"));
        Assert.Equal(false, rs.GetValue("m1"));
    }

    #endregion

    #region 태그 이름 접근 (Tag)

    [Fact]
    [DisplayName("SetTag stores tag mapping")]
    public void SetTag_StoresMapping()
    {
        var driver = new MCDriver();
        driver.SetTag("温度", "D100");
        driver.SetTag("压力", "D200");

        Assert.Equal("D100", driver.TagMap["温度"]);
        Assert.Equal("D200", driver.TagMap["压力"]);
        Assert.Equal(2, driver.TagMap.Count);
    }

    [Fact]
    [DisplayName("SetTags bulk tag mapping")]
    public void SetTags_BulkMapping()
    {
        var driver = new MCDriver();
        driver.SetTags(new Dictionary<String, String>
        {
            ["温度"] = "D100",
            ["压力"] = "D200",
            ["速度"] = "D300",
        });

        Assert.Equal(3, driver.TagMap.Count);
        Assert.Equal("D100", driver.TagMap["温度"]);
        Assert.Equal("D300", driver.TagMap["速度"]);
    }

    [Fact]
    [DisplayName("ReadTagAsync undefined tag throws")]
    public async Task ReadTagAsync_UndefinedTag_Throws()
    {
        var driver = new MCDriver();
        var p = new MCParameter { Address = "127.0.0.1:6000" };
        var node = driver.Open(null, p);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            driver.ReadTagAsync(node, "不存在"));
        Assert.Contains("is not defined in TagMap", ex.Message);
    }

    [Fact]
    [DisplayName("ReadTagAsync defined tag initiates read")]
    public async Task ReadTagAsync_DefinedTag_CallsRead()
    {
        var driver = new MCDriver();
        var p = new MCParameter { Address = "127.0.0.1:6000" };
        var node = driver.Open(null, p);

        var mock = new Mock<MCProtocol> { CallBase = false };
        mock.Setup(e => e.ReadWords(DeviceCode.D, 100, It.IsAny<Int32>()))
            .Returns([0x1234]);
        driver.Link = mock.Object;

        driver.SetTag("温度", "D100");
        var rs = await driver.ReadTagAsync(node, "温度");

        Assert.NotNull(rs);
        Assert.True(rs.IsSuccess);
        Assert.Equal((UInt16)0x1234, rs.GetValue("温度"));
    }

    #endregion
}
