using System.Text;

namespace NewLife.Melsec;

internal static class HexHelper
{
    ///// <summary>바이트를 16진수 문자 1개로 변환</summary>
    ///// <param name="b"></param>
    ///// <returns></returns>
    //public static String ToHexString(this Byte b) => Convert.ToString(b, 16);

    /// <summary>바이트 1개를 16진수 문자 2개로 변환</summary>
    /// <param name="b"></param>
    /// <returns></returns>
    public static String ToHexChars(this Byte b)
    {
        //Convert.ToString(b, 16);
        var cs = new Char[2];
        var ch = b >> 4;
        var cl = b & 0x0F;
        cs[0] = (Char)(ch >= 0x0A ? ('A' + ch - 0x0A) : ('0' + ch));
        cs[1] = (Char)(cl >= 0x0A ? ('A' + cl - 0x0A) : ('0' + cl));

        return new String(cs);
    }

    /// <summary>바이트 배열을 16진수 문자열로 변환</summary>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static String ToHexString(this Byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        for (var i = 0; i < bytes.Length; i++)
        {
            sb.Append(ToHexChars(bytes[i]));
        }
        return sb.ToString();
    }

    //public static Byte ToByte(this String str) => Convert.ToByte(str, 16);

    /// <summary>각 문자를 바이트 1개로 변환</summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static Byte[] ToChars(this String str)
    {
        var buf = new Byte[str.Length];
        for (var i = 0; i < str.Length; i++)
        {
            var ch = str[i];
            //if (ch >= '0' && ch <= 9)
            //    buf[i] = (Byte)(ch - '0');
            buf[i] = Convert.ToByte(ch);
        }

        return buf;
    }

    /// <summary>각 문자를 바이트 1개로 변환</summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static Byte[] ToBytes(this String str)
    {
        var buf = new Byte[str.Length];
        for (var i = 0; i < str.Length; i++)
        {
            var ch = str[i];
            if (ch >= '0' && ch <= '9')
                buf[i] = (Byte)(ch - '0');
            else if (ch >= 'A' && ch <= 'F')
                buf[i] = (Byte)(ch - 'A' + 0x0A);
        }

        return buf;
    }

    /// <summary>문자열의 지정된 위치에서 문자 2개를 잘라서 바이트로 변환</summary>
    /// <param name="str"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    public static Byte ToByte(this String str, Int32 offset) => Convert.ToByte(str.Substring(offset, 2), 16);
}