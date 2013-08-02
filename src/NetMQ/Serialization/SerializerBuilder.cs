using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using NetMQ.zmq;

namespace NetMQ.Serialization
{
  public class SerializerBuilder
  {
    private class PropertyInfoAttribute
    {
      public PropertyInfoAttribute(PropertyInfo propertyInfo, NetMQMemberAttribute attribute)
      {
        PropertyInfo = propertyInfo;
        Attribute = attribute;

        if (PropertyInfo.PropertyType.Equals(typeof(byte)))
        {
          MemberType = MemberTypeEnum.Byte;
        }
        else if (PropertyInfo.PropertyType.Equals(typeof(char)))
        {
          MemberType = MemberTypeEnum.Char;
        }
        else if (PropertyInfo.PropertyType.Equals(typeof(bool)))
        {
          MemberType = MemberTypeEnum.Boolean;
        }
        else if (PropertyInfo.PropertyType.Equals(typeof(short)))
        {
          MemberType = MemberTypeEnum.Int16;
        }
        else if (PropertyInfo.PropertyType.Equals(typeof(int)))
        {
          MemberType = MemberTypeEnum.Int32;
        }
        else if (PropertyInfo.PropertyType.Equals(typeof(long)))
        {
          MemberType = MemberTypeEnum.Int64;
        }
        else if (PropertyInfo.PropertyType.Equals(typeof(ushort)))
        {
          MemberType = MemberTypeEnum.UInt16;
        }
        else if (PropertyInfo.PropertyType.Equals(typeof(uint)))
        {
          MemberType = MemberTypeEnum.UInt32;
        }
        else if (PropertyInfo.PropertyType.Equals(typeof(ulong)))
        {
          MemberType = MemberTypeEnum.UInt64;
        }
        else if (PropertyInfo.PropertyType.Equals(typeof(float)))
        {
          MemberType = MemberTypeEnum.Float;
        }
        else if (PropertyInfo.PropertyType.Equals(typeof(double)))
        {
          MemberType = MemberTypeEnum.Double;
        }
        else if (PropertyInfo.PropertyType.Equals(typeof(string)))
        {
          MemberType = MemberTypeEnum.String;
        }
        else if (PropertyInfo.PropertyType.Equals(typeof(DateTime)))
        {
          MemberType = MemberTypeEnum.DateTime;
        }
        else if (PropertyInfo.PropertyType.IsEnum)
        {
          MemberType = MemberTypeEnum.Enum;
        }
        else
        {
          MemberType = MemberTypeEnum.Unknown;
        }
      }

      public PropertyInfo PropertyInfo { get; private set; }

      public NetMQMemberAttribute Attribute { get; private set; }

      public MemberTypeEnum MemberType { get; private set; }
    }

    private readonly Type m_type;
    private IList<PropertyInfoAttribute> m_properties;

    public SerializerBuilder(Type type)
    {
      m_type = type;
      m_properties = type.GetProperties().Where(p => p.GetCustomAttributes(typeof(NetMQMemberAttribute), false).Any()).
                          Select(p => new PropertyInfoAttribute(p, ((NetMQMemberAttribute)(p.GetCustomAttributes(typeof(NetMQMemberAttribute), false)[0]))))
                         .OrderBy(p => p.Attribute.Frame).ToList();

      int maxFrameNumber = m_properties.Max(p => p.Attribute.Frame);

      if (maxFrameNumber != m_properties.Count() - 1)
      {
        throw new ArgumentException("No missing frames is allowed");
      }

      int maxFrameCount = m_properties.GroupBy(p => p.Attribute.Frame).Select(g => g.Count()).Max();

      if (maxFrameCount > 1)
      {
        throw new ArgumentException("Each frame can used only once");
      }

      if (m_properties.Any(p => p.MemberType == MemberTypeEnum.Unknown))
      {
        throw new ArgumentException("Member must be primitive type only");
      }
    }

    public Func<byte[][], object> CreateDeserializer()
    {
      ParameterExpression framesParameterExpression = Expression.Parameter(typeof(byte[][]), "frames");

      ParameterExpression messageVariableExpression = Expression.Variable(m_type, "message");

      IList<Expression> expressions = new List<Expression>();

      Expression createMessage = Expression.Assign(messageVariableExpression,
                                                            Expression.New(m_type));

      expressions.Add(createMessage);

      Expression arrayLength = Expression.ArrayLength(framesParameterExpression);

      foreach (PropertyInfoAttribute property in m_properties)
      {
        Expression frameNumberConstantExpression = Expression.Constant(property.Attribute.Frame);

        Expression arrayItem = Expression.ArrayAccess(framesParameterExpression, frameNumberConstantExpression);

        Expression invokeExpression;

        switch (property.MemberType)
        {
          case MemberTypeEnum.Boolean:
            invokeExpression = CreateToBooleanExpression(arrayItem, property);
            break;
          case MemberTypeEnum.Byte:
            invokeExpression = CreateToByteExpression(arrayItem, property);
            break;
          case MemberTypeEnum.Char:
            invokeExpression = CreateToCharExpression(arrayItem, property);
            break;
          case MemberTypeEnum.Int16:
            invokeExpression = CreateToInt16Expression(arrayItem, property);
            break;
          case MemberTypeEnum.Int64:
            invokeExpression = CreateToInt64Expression(arrayItem, property);
            break;
          case MemberTypeEnum.Int32:
            invokeExpression = CreateToInt32Expression(arrayItem, property);
            break;
          case MemberTypeEnum.UInt16:
            invokeExpression = CreateToUInt16Expression(arrayItem, property);
            break;
          case MemberTypeEnum.UInt32:
            invokeExpression = CreateToUInt32Expression(arrayItem, property);
            break;
          case MemberTypeEnum.UInt64:
            invokeExpression = CreateToUInt64Expression(arrayItem, property);
            break;
          case MemberTypeEnum.Float:
            invokeExpression = CreateToFloatExpression(arrayItem, property);
            break;
          case MemberTypeEnum.Double:
            invokeExpression = CreateToDoubleExpression(arrayItem, property);
            break;
          case MemberTypeEnum.String:
            invokeExpression = CreateToStringExpression(arrayItem, property);
            break;
          case MemberTypeEnum.DateTime:
            invokeExpression = CreateToDateTimeExpression(arrayItem, property);
            break;
          case MemberTypeEnum.Enum:
            invokeExpression = CreateToEnumExpression(arrayItem, property);
            break;
          default:
            throw new ArgumentOutOfRangeException();
        }

        Expression propertyExpression = Expression.Property(messageVariableExpression, property.PropertyInfo);

        Expression assignExpression = Expression.Assign(propertyExpression, invokeExpression);

        Expression ifArrayItemExist =
          Expression.IfThen(Expression.GreaterThan(arrayLength, frameNumberConstantExpression), assignExpression);

        expressions.Add(ifArrayItemExist);
      }

      // add the return expression
      expressions.Add(messageVariableExpression);

      Expression blockExpression = Expression.Block(new ParameterExpression[]
        {
          messageVariableExpression
        }, expressions);

      return Expression.Lambda<Func<byte[][], object>>(blockExpression, framesParameterExpression).Compile();
    }

    private Expression CreateToEnumExpression(Expression arrayItem, PropertyInfoAttribute property)
    {
      Expression<Func<byte[], Endianness, EnumSerializationType, Type, object>> convertExpression =
        (bytes, endian, serializationType, type) => Convertions.ToEnum(type, bytes, serializationType, endian);

      return Expression.Convert(
        Expression.Invoke(convertExpression, arrayItem,
        Expression.Constant(property.Attribute.Endianness),
        Expression.Constant(property.Attribute.EnumSerializationType),
        Expression.Constant(property.PropertyInfo.PropertyType)), property.PropertyInfo.PropertyType);
    }

    private Expression CreateToDateTimeExpression(Expression arrayItem, PropertyInfoAttribute property)
    {
      Expression<Func<byte[], Endianness, DateTime>> convertExpression =
        (bytes, endian) => DateTime.FromBinary(Convertions.ToInt64(bytes, endian));

      return Expression.Invoke(convertExpression, arrayItem, Expression.Constant(property.Attribute.Endianness));
    }

    private Expression CreateToStringExpression(Expression arrayItem, PropertyInfoAttribute property)
    {
      Expression<Func<byte[], Encoding, string>> convertExpression = (bytes, encoding) => Convertions.ToString(bytes, encoding);

      return Expression.Invoke(convertExpression, arrayItem, Expression.Constant(property.Attribute.Encoding));
    }

    private Expression CreateToDoubleExpression(Expression arrayItem, PropertyInfoAttribute property)
    {
      Expression<Func<byte[], Endianness, double>> convertExpression = (bytes, endian) => Convertions.ToDouble(bytes, endian);

      return Expression.Invoke(convertExpression, arrayItem, Expression.Constant(property.Attribute.Endianness));
    }

    private Expression CreateToFloatExpression(Expression arrayItem, PropertyInfoAttribute property)
    {
      Expression<Func<byte[], Endianness, float>> convertExpression = (bytes, endian) => Convertions.ToFloat(bytes, endian);

      return Expression.Invoke(convertExpression, arrayItem, Expression.Constant(property.Attribute.Endianness));
    }

    private Expression CreateToUInt64Expression(Expression arrayItem, PropertyInfoAttribute property)
    {
      Expression<Func<byte[], Endianness, ulong>> convertExpression = (bytes, endian) => Convertions.ToUInt64(bytes, endian);

      return Expression.Invoke(convertExpression, arrayItem, Expression.Constant(property.Attribute.Endianness));
    }

    private Expression CreateToUInt32Expression(Expression arrayItem, PropertyInfoAttribute property)
    {
      Expression<Func<byte[], Endianness, uint>> convertExpression = (bytes, endian) => Convertions.ToUInt32(bytes, endian);

      return Expression.Invoke(convertExpression, arrayItem, Expression.Constant(property.Attribute.Endianness));
    }

    private Expression CreateToUInt16Expression(Expression arrayItem, PropertyInfoAttribute property)
    {
      Expression<Func<byte[], Endianness, ushort>> convertExpression = (bytes, endian) => Convertions.ToUInt16(bytes, endian);

      return Expression.Invoke(convertExpression, arrayItem, Expression.Constant(property.Attribute.Endianness));
    }

    private Expression CreateToInt32Expression(Expression arrayItem, PropertyInfoAttribute property)
    {
      Expression<Func<byte[], Endianness, int>> convertExpression = (bytes, endian) => Convertions.ToInt32(bytes, endian);

      return Expression.Invoke(convertExpression, arrayItem, Expression.Constant(property.Attribute.Endianness));
    }

    private Expression CreateToInt64Expression(Expression arrayItem, PropertyInfoAttribute property)
    {
      Expression<Func<byte[], Endianness, long>> convertExpression = (bytes, endian) => Convertions.ToInt64(bytes, endian);

      return Expression.Invoke(convertExpression, arrayItem, Expression.Constant(property.Attribute.Endianness));
    }

    private Expression CreateToInt16Expression(Expression arrayItem, PropertyInfoAttribute property)
    {
      Expression<Func<byte[], Endianness, short>> convertExpression = (bytes, endian) => Convertions.ToInt16(bytes, endian);

      return Expression.Invoke(convertExpression, arrayItem, Expression.Constant(property.Attribute.Endianness));
    }

    private Expression CreateToCharExpression(Expression arrayItem, PropertyInfoAttribute property)
    {
      Expression<Func<byte[], Endianness, char>> convertExpression = (bytes, endian) => (char)Convertions.ToInt16(bytes, endian);

      return Expression.Invoke(convertExpression, arrayItem, Expression.Constant(property.Attribute.Endianness));
    }

    private Expression CreateToByteExpression(Expression arrayItem, PropertyInfoAttribute property)
    {
      Expression<Func<byte[], byte>> convertExpression = (bytes) => bytes[0];

      return Expression.Invoke(convertExpression, arrayItem);
    }

    private Expression CreateToBooleanExpression(Expression arrayItem, PropertyInfoAttribute property)
    {
      Expression<Func<byte[], bool>> convertExpression = (bytes) => BitConverter.ToBoolean(bytes, 0);

      return Expression.Invoke(convertExpression, arrayItem);
    }

    public Func<object, byte[][]> CreateSerializer()
    {
      ParameterExpression objectParameterExpression = Expression.Parameter(typeof(object), "obj");

      ParameterExpression castedObjectParameterExpression = Expression.Variable(m_type, "message");

      IList<Expression> propertiesExpressions = new List<Expression>();

      foreach (var property in m_properties)
      {
        Expression propertyExpression = Expression.Property(castedObjectParameterExpression, property.PropertyInfo);

        Expression invokeExpression;

        switch (property.MemberType)
        {
          case MemberTypeEnum.Boolean:
            invokeExpression = CreateFromBooleanExpression(propertyExpression, property);
            break;
          case MemberTypeEnum.Byte:
            invokeExpression = CreateFromByteExpression(propertyExpression, property);
            break;
          case MemberTypeEnum.Char:
            invokeExpression = CreateFromCharExpression(propertyExpression, property);
            break;
          case MemberTypeEnum.Int16:
            invokeExpression = CreateFromInt16Expression(propertyExpression, property);
            break;
          case MemberTypeEnum.Int64:
            invokeExpression = CreateFromInt64Expression(propertyExpression, property);
            break;
          case MemberTypeEnum.Int32:
            invokeExpression = CreateFromInt32Expression(propertyExpression, property);
            break;
          case MemberTypeEnum.UInt16:
            invokeExpression = CreateFromUInt16Expression(propertyExpression, property);
            break;
          case MemberTypeEnum.UInt32:
            invokeExpression = CreateFromUInt32Expression(propertyExpression, property);
            break;
          case MemberTypeEnum.UInt64:
            invokeExpression = CreateFromUInt64Expression(propertyExpression, property);
            break;
          case MemberTypeEnum.Float:
            invokeExpression = CreateFromFloatExpression(propertyExpression, property);
            break;
          case MemberTypeEnum.Double:
            invokeExpression = CreateFromDoubleExpression(propertyExpression, property);
            break;
          case MemberTypeEnum.String:
            invokeExpression = CreateFromStringExpression(propertyExpression, property);
            break;
          case MemberTypeEnum.DateTime:
            invokeExpression = CreateFromDateTimeExpression(propertyExpression, property);
            break;
          case MemberTypeEnum.Enum:
            invokeExpression = CreateFromEnumExpression(propertyExpression, property);
            break;
          default:
            throw new ArgumentOutOfRangeException();
        }

        propertiesExpressions.Add(invokeExpression);
      }

      ParameterExpression framesVariableExpression = Expression.Variable(typeof(byte[][]), "frames");

      Expression initFramesArray = Expression.Assign(framesVariableExpression,
        Expression.NewArrayInit(typeof(byte[]), propertiesExpressions));

      Expression castObjectToType = Expression.Assign(castedObjectParameterExpression,
        Expression.TypeAs(objectParameterExpression, m_type));

      Expression blockExpression = Expression.Block(new ParameterExpression[]
        {
          framesVariableExpression,
          castedObjectParameterExpression
        },
        castObjectToType,
        initFramesArray,
        framesVariableExpression);

      return Expression.Lambda<Func<object, byte[][]>>(blockExpression, objectParameterExpression).Compile();
    }

    private Expression CreateFromEnumExpression(Expression propertyExpression, PropertyInfoAttribute property)
    {
      Expression<Func<IConvertible, Endianness, EnumSerializationType, Byte[]>> convertExpression =
        (e, endian, serializationType) => Convertions.ToBytes(e, serializationType, endian);


      Expression invokeExpression = Expression.Invoke(convertExpression,
        Expression.Convert(propertyExpression, typeof(IConvertible)),
        Expression.Constant(property.Attribute.Endianness),
        Expression.Constant(property.Attribute.EnumSerializationType));
      return invokeExpression;
    }

    private Expression CreateFromDateTimeExpression(Expression propertyExpression, PropertyInfoAttribute property)
    {
      Expression<Func<DateTime, Endianness, Byte[]>> convertExpression =
        (d, endian) => Convertions.ToBytes(d.ToBinary(), endian);
      Expression invokeExpression = Expression.Invoke(convertExpression, propertyExpression,
                                                      Expression.Constant(property.Attribute.Endianness));
      return invokeExpression;
    }

    private Expression CreateFromStringExpression(Expression propertyExpression, PropertyInfoAttribute property)
    {
      Expression<Func<string, Encoding, Byte[]>> convertExpression = (str, encoding) => Convertions.ToBytes(str, encoding);
      Expression invokeExpression = Expression.Invoke(convertExpression, propertyExpression,
                                                      Expression.Constant(property.Attribute.Encoding));
      return invokeExpression;
    }

    private Expression CreateFromDoubleExpression(Expression propertyExpression, PropertyInfoAttribute property)
    {
      Expression<Func<double, Endianness, Byte[]>> convertExpression = (num, endian) => Convertions.ToBytes(num, endian);
      Expression invokeExpression = Expression.Invoke(convertExpression, propertyExpression,
                                                      Expression.Constant(property.Attribute.Endianness));
      return invokeExpression;
    }

    private Expression CreateFromFloatExpression(Expression propertyExpression, PropertyInfoAttribute property)
    {
      Expression<Func<float, Endianness, Byte[]>> convertExpression = (num, endian) => Convertions.ToBytes(num, endian);
      Expression invokeExpression = Expression.Invoke(convertExpression, propertyExpression,
                                                      Expression.Constant(property.Attribute.Endianness));
      return invokeExpression;
    }

    private Expression CreateFromUInt64Expression(Expression propertyExpression, PropertyInfoAttribute property)
    {
      Expression<Func<UInt64, Endianness, Byte[]>> convertExpression = (num, endian) => Convertions.ToBytes(num, endian);
      Expression invokeExpression = Expression.Invoke(convertExpression, propertyExpression,
                                                      Expression.Constant(property.Attribute.Endianness));
      return invokeExpression;
    }

    private Expression CreateFromUInt32Expression(Expression propertyExpression, PropertyInfoAttribute property)
    {
      Expression<Func<UInt32, Endianness, Byte[]>> convertExpression = (num, endian) => Convertions.ToBytes(num, endian);
      Expression invokeExpression = Expression.Invoke(convertExpression, propertyExpression,
                                                      Expression.Constant(property.Attribute.Endianness));
      return invokeExpression;
    }

    private Expression CreateFromUInt16Expression(Expression propertyExpression, PropertyInfoAttribute property)
    {
      Expression<Func<UInt16, Endianness, Byte[]>> convertExpression = (num, endian) => Convertions.ToBytes(num, endian);
      Expression invokeExpression = Expression.Invoke(convertExpression, propertyExpression,
                                                      Expression.Constant(property.Attribute.Endianness));
      return invokeExpression;
    }

    private Expression CreateFromInt64Expression(Expression propertyExpression, PropertyInfoAttribute property)
    {
      Expression<Func<Int64, Endianness, Byte[]>> convertExpression = (num, endian) => Convertions.ToBytes(num, endian);
      Expression invokeExpression = Expression.Invoke(convertExpression, propertyExpression,
                                                      Expression.Constant(property.Attribute.Endianness));
      return invokeExpression;
    }

    private Expression CreateFromInt16Expression(Expression propertyExpression, PropertyInfoAttribute property)
    {
      Expression<Func<Int16, Endianness, Byte[]>> convertExpression = (num, endian) => Convertions.ToBytes(num, endian);
      Expression invokeExpression = Expression.Invoke(convertExpression, propertyExpression,
                                                      Expression.Constant(property.Attribute.Endianness));
      return invokeExpression;
    }

    private Expression CreateFromCharExpression(Expression propertyExpression, PropertyInfoAttribute property)
    {
      Expression<Func<char, Endianness, Byte[]>> convertExpression = (c, endian) => Convertions.ToBytes((short)c, endian);
      Expression invokeExpression = Expression.Invoke(convertExpression, propertyExpression,
                                                      Expression.Constant(property.Attribute.Endianness));
      return invokeExpression;
    }

    private Expression CreateFromByteExpression(Expression propertyExpression, PropertyInfoAttribute property)
    {
      Expression<Func<byte, Byte[]>> convertExpression = (b) => new byte[] { b };
      Expression invokeExpression = Expression.Invoke(convertExpression, propertyExpression);
      return invokeExpression;
    }

    private Expression CreateFromBooleanExpression(Expression propertyExpression, PropertyInfoAttribute property)
    {
      Expression<Func<bool, Byte[]>> convertExpression = (boolean) => Convertions.ToBytes(boolean);
      Expression invokeExpression = Expression.Invoke(convertExpression, propertyExpression);
      return invokeExpression;
    }

    private static Expression CreateFromInt32Expression(Expression propertyExpression, PropertyInfoAttribute property)
    {
      Expression<Func<int, Endianness, Byte[]>> convertExpression = (num, endian) => Convertions.ToBytes(num, endian);
      Expression invokeExpression = Expression.Invoke(convertExpression, propertyExpression,
                                                      Expression.Constant(property.Attribute.Endianness));
      return invokeExpression;
    }
  }
}
