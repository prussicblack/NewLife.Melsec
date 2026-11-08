namespace NewLife.IoT.Drivers;

/// <summary>
/// 노드
/// </summary>
public class MelsecNode : INode
{
    /// <summary>호스트 주소</summary>
    public String Address { get; set; }

    /// <summary>스테이션 번호</summary>
    public Byte Host { get;set; }

    /// <summary>채널</summary>
    public IDriver Driver { get; set; }

    /// <summary>기기</summary>
    public IDevice Device { get; set; }

    /// <summary>매개변수</summary>
    public IDriverParameter Parameter { get; set; }

    /// <summary>연결 상태</summary>
    public Boolean IsConnected => true;
}