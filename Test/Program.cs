using NewLife;
using NewLife.IoT.Drivers;
using NewLife.IoT.ThingModels;
using NewLife.Log;
using NewLife.Melsec.Drivers;
using NewLife.Net;
using NewLife.Serialization;

XTrace.UseConsole();

// var ps = SerialTransport.GetPortNames();
// Console.WriteLine("시리얼 포트 선택:");

// for (var i = 0; i < ps.Length; i++)
// {
//     Console.WriteLine($"{i}、{ps[i]}");
// }

// var idx = Int32.Parse(Console.ReadLine());

// var cfg = SerialPortConfig.Current;

// var pt = new SerialTransport();
// pt.PortName = ps[idx];
// pt.BaudRate = cfg.BaudRate;
// pt.Parity = cfg.Parity;
// pt.DataBits = cfg.DataBits;
// pt.StopBits = cfg.StopBits;
// pt.Disconnected += Pt_Disconnected;
// pt.Received += Pt_Received;

// void Pt_Disconnected(Object? sender, EventArgs e)
// {
//     Console.WriteLine("연결 끊김");
//     pt.Disconnected -= Pt_Disconnected;
//     pt.Received -= Pt_Received;
//     pt.Close();
// }



// void Pt_Received(Object? sender, ReceivedEventArgs e)
// {
//     var data = e.Packet.ReadBytes();
//     Console.WriteLine(data.ToHex());
// }
// pt.Open();

// Console.WriteLine(ps[idx]);
// Console.WriteLine(pt.Serial.IsOpen);
// Console.ReadLine();

Console.WriteLine("Select protocol:");
Console.WriteLine("1. MCQna3E");
Console.WriteLine("2. FxLinks485");

var mode = Console.ReadLine();
var pm = new MelsecParameter();

if (mode == "1")
{
    Console.WriteLine("Default server address: 127.0.0.1:6000. Press Enter to connect with the default, or enter the server address:");
    var address = Console.ReadLine();

    if (address == null || address == "") address = "127.0.0.1:6000";

    pm.Protocol = Protocol.MCQna3E;
    pm.Address = address;
}
else if (mode == "2")
{
    var ps = SerialTransport.GetPortNames();
    Console.WriteLine("Select serial port:");

    for (var i = 0; i < ps.Length; i++)
    {
        Console.WriteLine($"{i}. {ps[i]}");
    }

    var idx = Int32.Parse(Console.ReadLine());

    pm.Protocol = Protocol.FxLinks485;
    pm.Baudrate = 9600;
    pm.PortName = ps[idx];
}

//var driver = new MelsecDriver();
var driver = new FxLinksDriver();

var node = driver.Open(null, pm);

// 두 개 채널 오픈 테스트
node = driver.Open(null, pm);

if (mode == "1") Console.WriteLine($"Connected => {pm.Address}!");
else Console.WriteLine($"Connected => {pm.PortName}!");


Console.WriteLine("Enter an integer, press q to exit:");


var str = Console.ReadLine();
var point = new Point
{
    Name = "test",
    Address = "D232",
    Type = "ushort",
    Length = 2
};

do
{
    // 쓰기
    var data = BitConverter.GetBytes(Int32.Parse(str));


    // var res = (OperateResult)driver.Write(node, point, Int16.Parse(str));

    // Console.WriteLine($"쓰기 결과: {res.ToJson()}");

    // point.Address = "Y" + str;
    // 읽기
    var dic = driver.Read(node, new[] { point });
    Console.WriteLine($"Read result 1: {dic.ToJson()}");

    var data1 = dic[point.Name] as Byte[];

    //Console.WriteLine($"읽기 결과: {BitConverter.ToInt32(data1)}");
    Console.WriteLine($"Read result: {data1.ToHex()}");
    Console.WriteLine($"");
    Console.WriteLine("Enter an integer, press q to exit:");

} while ((str = Console.ReadLine()) != "q");

// 연결 해제
driver.Close(node);
driver.Close(node);


public class Point : IPoint
{
    public String Name { get; set; }
    public String Address { get; set; }
    public String Type { get; set; }
    public Int32 Length { get; set; }
}