using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ.Serialization
{
  static class Convertions
  {
    public static int ToInt32(byte[] bytes, Endianness endian)
    {
      if (endian == Endianness.Little && BitConverter.IsLittleEndian)
      {
        return BitConverter.ToInt32(bytes, 0);
      }
      else
      {
        return BitConverter.ToInt32(bytes.Reverse().ToArray(), 0);
      }
    }

    public static byte[] ToBytes(int num, Endianness endian)
    {
      byte[] bytes = BitConverter.GetBytes(num);

      if (BitConverter.IsLittleEndian && endian != Endianness.Little)
      {
        return bytes.Reverse().ToArray();
      }

      return bytes;
    }

    public static Int16 ToInt16(byte[] bytes, Endianness endian)
    {
      if (endian == Endianness.Little && BitConverter.IsLittleEndian)
      {
        return BitConverter.ToInt16(bytes, 0);
      }
      else
      {
        return BitConverter.ToInt16(bytes.Reverse().ToArray(), 0);
      }
    }

    public static byte[] ToBytes(Int16 num, Endianness endian)
    {
      byte[] bytes = BitConverter.GetBytes(num);

      if (BitConverter.IsLittleEndian && endian != Endianness.Little)
      {
        return bytes.Reverse().ToArray();
      }

      return bytes;
    }

    public static Int64 ToInt64(byte[] bytes, Endianness endian)
    {
      if (endian == Endianness.Little && BitConverter.IsLittleEndian)
      {
        return BitConverter.ToInt64(bytes, 0);
      }
      else
      {
        return BitConverter.ToInt64(bytes.Reverse().ToArray(), 0);
      }
    }

    public static byte[] ToBytes(Int64 num, Endianness endian)
    {
      byte[] bytes = BitConverter.GetBytes(num);

      if (BitConverter.IsLittleEndian && endian != Endianness.Little)
      {
        return bytes.Reverse().ToArray();
      }

      return bytes;
    }

    public static uint ToUInt32(byte[] bytes, Endianness endian)
    {
      if (endian == Endianness.Little && BitConverter.IsLittleEndian)
      {
        return BitConverter.ToUInt32(bytes, 0);
      }
      else
      {
        return BitConverter.ToUInt32(bytes.Reverse().ToArray(), 0);
      }
    }

    public static byte[] ToBytes(uint num, Endianness endian)
    {
      byte[] bytes = BitConverter.GetBytes(num);

      if (BitConverter.IsLittleEndian && endian != Endianness.Little)
      {
        return bytes.Reverse().ToArray();
      }

      return bytes;
    }

    public static UInt16 ToUInt16(byte[] bytes, Endianness endian)
    {
      if (endian == Endianness.Little && BitConverter.IsLittleEndian)
      {
        return BitConverter.ToUInt16(bytes, 0);
      }
      else
      {
        return BitConverter.ToUInt16(bytes.Reverse().ToArray(), 0);
      }
    }

    public static byte[] ToBytes(UInt16 num, Endianness endian)
    {
      byte[] bytes = BitConverter.GetBytes(num);

      if (BitConverter.IsLittleEndian && endian != Endianness.Little)
      {
        return bytes.Reverse().ToArray();
      }

      return bytes;
    }

    public static UInt64 ToUInt64(byte[] bytes, Endianness endian)
    {
      if (endian == Endianness.Little && BitConverter.IsLittleEndian)
      {
        return BitConverter.ToUInt64(bytes, 0);
      }
      else
      {
        return BitConverter.ToUInt64(bytes.Reverse().ToArray(), 0);
      }
    }

    public static byte[] ToBytes(UInt64 num, Endianness endian)
    {
      byte[] bytes = BitConverter.GetBytes(num);

      if (BitConverter.IsLittleEndian && endian != Endianness.Little)
      {
        return bytes.Reverse().ToArray();
      }

      return bytes;
    }

    public static float ToFloat(byte[] bytes, Endianness endian)
    {
      if (endian == Endianness.Little && BitConverter.IsLittleEndian)
      {
        return BitConverter.ToSingle(bytes, 0);
      }
      else
      {
        return BitConverter.ToSingle(bytes.Reverse().ToArray(), 0);
      }
    }

    public static byte[] ToBytes(float num, Endianness endian)
    {
      byte[] bytes = BitConverter.GetBytes(num);

      if (BitConverter.IsLittleEndian && endian != Endianness.Little)
      {
        return bytes.Reverse().ToArray();
      }

      return bytes;
    }

    public static double ToDouble(byte[] bytes, Endianness endian)
    {
      if (endian == Endianness.Little && BitConverter.IsLittleEndian)
      {
        return BitConverter.ToDouble(bytes, 0);
      }
      else
      {
        return BitConverter.ToDouble(bytes.Reverse().ToArray(), 0);
      }
    }

    public static byte[] ToBytes(double num, Endianness endian)
    {
      byte[] bytes = BitConverter.GetBytes(num);

      if (BitConverter.IsLittleEndian && endian != Endianness.Little)
      {
        return bytes.Reverse().ToArray();
      }

      return bytes;
    }

    public static bool ToBoolean(byte[] bytes)
    {
      return BitConverter.ToBoolean(bytes, 0);
    }

    public static byte[] ToBytes(bool boolean)
    {
      byte[] bytes = BitConverter.GetBytes(boolean);

      

      return bytes;
    }

    public static string ToString(byte[] bytes, Encoding encoding)
    {     
      if (bytes.Length == 0)
      {
        return string.Empty;
      }
      else
      {
        return encoding.GetString(bytes);
      }
    }

    public static byte[] ToBytes(string str, Encoding encoding)
    {
      if (string.IsNullOrEmpty(str))
      {
        return new byte[0];
      }
      else
      {
        return encoding.GetBytes(str);
      }
    }

    public static object ToEnum(Type type, byte[] bytes, EnumSerializationType serializationType, Endianness endian)
    {
      object value;

      switch (serializationType)
      {
        case EnumSerializationType.Int32:          
          value = Enum.ToObject(type, ToInt32(bytes, endian));
          break;
        case EnumSerializationType.Int16:
          value = Enum.ToObject(type, ToInt16(bytes, endian));
          break;
        case EnumSerializationType.Byte:
          value = Enum.ToObject(type, bytes[0]);
          break;
        case EnumSerializationType.MemberName:
          string text = ToString(bytes, Encoding.ASCII);
          value = Enum.Parse(type, text);

          break;
        default:
          throw new ArgumentOutOfRangeException();
      }

      return value;
    }

    public static byte[] ToBytes(IConvertible value, EnumSerializationType serializationType, Endianness endian)
    {
      switch (serializationType)
      {
        case EnumSerializationType.Int32:
          return Convertions.ToBytes(value.ToInt32(CultureInfo.InvariantCulture), endian);
          break;
        case EnumSerializationType.Int16:
          return Convertions.ToBytes(value.ToInt16(CultureInfo.InvariantCulture), endian);
          break;
        case EnumSerializationType.Byte:
          return Convertions.ToBytes(value.ToByte(CultureInfo.InvariantCulture), endian);
          break;
        case EnumSerializationType.MemberName:
          return Convertions.ToBytes(Enum.GetName(value.GetType(), value), Encoding.ASCII);
          break;
        default:
          throw new ArgumentOutOfRangeException();
      }
    }
  }
}
