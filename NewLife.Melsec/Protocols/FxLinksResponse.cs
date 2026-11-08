using System.Runtime.Serialization;
using System.Text;
using NewLife.Data;
using NewLife.Serialization;
namespace NewLife.Melsec.Protocols;

/// <summary>三菱 FxLinks 응답</summary>
/// <remarks>
/// 기능 코드:
/// ENQ 05
/// STX 02
/// ETX 03
/// NAK H15
/// </remarks>
public class FxLinksResponse : IAccessor
{
    #region 속성
    /// <summary>제어 코드. 02/03/05/15</summary>
    public ControlCodes Code { get; set; }

    /// <summary>스테이션 번호</summary>
    public Byte Station { get; set; }

    /// <summary>PLC 번호</summary>
    public Byte PLC { get; set; }

    /// <summary>운영 코드. 데이터 파싱 전에 설정, 데이터 파싱 방식 판단에만 사용</summary>
    public String Command { get; set; }

    /// <summary>페이로드 데이터</summary>
    public String Payload { get; set; }

    /// <summary>체크섬. 읽어온 값</summary>
    public Byte CheckSum { get; set; }

    /// <summary>체크섬. 계산한 값</summary>
    public Byte CheckSum2 { get; set; }
    #endregion

    #region 생성
    /// <summary>오버라이드. 친화적인 문자열</summary>
    /// <returns></returns>
    public override String ToString()
    {
        //if (Code == ControlCodes.STX)
        //    return $"{Command} ({Payload?.ToHex()})";
        //else
        return $"{Code} ({Payload})";
    }
    #endregion

    #region 메서드
    /// <summary>읽기</summary>
    /// <param name="stream">데이터 스트림</param>
    /// <param name="context">컨텍스트</param>
    /// <returns></returns>
    public virtual Boolean Read(Stream stream, Object context)
    {
        Code = (ControlCodes)stream.ReadByte();
        switch (Code)
        {
            case ControlCodes.STX:
                {
                    // 05FF0001\03B5
                    // STX-05FF0-ETX-24
                    var p = stream.Position;
                    var hex = stream.ReadBytes(4).ToStr();

                    Station = hex.ToByte(0);
                    PLC = hex.ToByte(2);

                    var retain = (Int32)(stream.Length - stream.Position);
                    if (retain < 3) return false;

                    var len = retain - 3;
                    if (len > 0)
                    {
                        Payload = stream.ReadBytes(len).ToStr();
                        //var str = stream.ReadBytes(len).ToStr();
                        //// 비트 읽기 시 각 포인트는 1자, 워드 읽기 시 각 포인트는 4자
                        //if (len == 1)
                        //    Payload = new Byte[] { Convert.ToByte(str, 16) };
                        //else if (Command == "BR")
                        //    Payload = str.ToArray().Select(e => Convert.ToByte(e + "", 16)).ToArray();
                        //else
                        //    Payload = str.ToHex();
                    }

                    var b = (ControlCodes)stream.ReadByte();
                    if (b != ControlCodes.ETX) return false;

                    len = (Int32)(stream.Position - p);
                    stream.Position = p;
                    CheckSum2 = (Byte)stream.ReadBytes(len).Sum(e => e);

                    CheckSum = stream.ReadBytes(2).ToStr().ToByte(0);
                    break;
                }

            case ControlCodes.ETX:
                return false;
            case ControlCodes.ACK:
            case ControlCodes.NAK:
                {
                    // 05FF
                    var hex = stream.ReadBytes(-1).ToStr();

                    Station = hex.ToByte(0);
                    PLC = hex.ToByte(2);

                    // 데이터가 더 있으면 페이로드로 사용. 보통 ACK 뒤에는 없고 NAK 뒤에는 에러 코드가 있음
                    if (hex.Length > 4) Payload = hex[4..];

                    break;
                }

            case ControlCodes.EOT:
                return false;
            default:
                return false;
        }

        return true;
    }

    /// <summary>메시지를 데이터 스트림에 쓰기</summary>
    /// <param name="stream">데이터 스트림</param>
    /// <param name="context">컨텍스트</param>
    /// <returns></returns>
    public virtual Boolean Write(Stream stream, Object context)
    {
        stream.Write((Byte)Code);

        switch (Code)
        {
            case ControlCodes.STX:
                {
                    // 05FF0001\03B5
                    var sb = new StringBuilder(64);

                    sb.Append(Station.ToString("X2"));
                    sb.Append(PLC.ToString("X2"));

                    var pk = Payload;
                    if (pk != null)
                    {
                        sb.Append(pk);
                        //// 비트 읽기 시 각 포인트는 1자, 워드 읽기 시 각 포인트는 4자
                        //if (pk.Total == 1)
                        //    sb.Append(pk[0].ToString("X"));
                        //else if (Command == "BR")
                        //{
                        //    var buf = pk.ReadBytes();
                        //    //for (var i = 0; i < buf.Length; i++)
                        //    //{
                        //    //    sb.Append(Convert.ToString(buf[i], 16));
                        //    //}
                        //    sb.Append(buf.ToHexString());
                        //}
                        //else
                        //    sb.Append(pk.ToHex(256));
                    }

                    sb.Append((Char)ControlCodes.ETX);

                    var sum = 0;
                    for (var i = 0; i < sb.Length; i++)
                    {
                        sum += sb[i];
                    }
                    CheckSum2 = (Byte)sum;
                    sb.Append(CheckSum2.ToString("X2"));

                    var hex = sb.ToString();
                    stream.Write(hex.GetBytes());

                    break;
                }

            case ControlCodes.ETX:
            case ControlCodes.EOT:
                return false;

            case ControlCodes.ACK:
            case ControlCodes.NAK:
                {
                    // 05FF
                    var sb = new StringBuilder(64);

                    sb.Append(Station.ToString("X2"));
                    sb.Append(PLC.ToString("X2"));

                    var hex = sb.ToString();
                    stream.Write(hex.GetBytes());

                    break;
                }

            default:
                return false;
        }

        return true;
    }

    /// <summary>메시지를 데이터 패킷으로 변환</summary>
    /// <returns></returns>
    public IPacket ToPacket()
    {
        var ms = new MemoryStream();
        Write(ms, null);

        ms.Position = 0;
        return new ArrayPacket(ms);
    }
    #endregion
}