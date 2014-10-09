using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace RemotingLite
{
    internal sealed class ParameterTypes
    {
        internal const byte Unknown = 0x00;
        internal const byte Bool = 0x01;
        internal const byte Byte = 0x02;
        internal const byte SByte = 0x03;
        internal const byte Char = 0x04;
        internal const byte Decimal = 0x05;
        internal const byte Double = 0x06;
        internal const byte Float = 0x07;
        internal const byte Int = 0x08;
        internal const byte UInt = 0x09;
        internal const byte Long = 0x0A;
        internal const byte ULong = 0x0B;
        internal const byte Short = 0x0C;
        internal const byte UShort = 0x0D;
        internal const byte String = 0x0E;
        internal const byte ByteArray = 0x0F;
        internal const byte CharArray = 0x10;
        internal const byte Null = 0x11;
		internal const byte Type = 0x12;
    }

    internal sealed class ParameterTransferHelper
    {
        private Dictionary<Type, byte> _parameterTypes;

        internal ParameterTransferHelper()
        {
            _parameterTypes = new Dictionary<Type, byte>();
            _parameterTypes.Add(typeof(bool), ParameterTypes.Bool);
            _parameterTypes.Add(typeof(byte), ParameterTypes.Byte);
            _parameterTypes.Add(typeof(sbyte), ParameterTypes.SByte);
            _parameterTypes.Add(typeof(char), ParameterTypes.Char);
            _parameterTypes.Add(typeof(decimal), ParameterTypes.Decimal);
            _parameterTypes.Add(typeof(double), ParameterTypes.Double);
            _parameterTypes.Add(typeof(float), ParameterTypes.Float);
            _parameterTypes.Add(typeof(int), ParameterTypes.Int);
            _parameterTypes.Add(typeof(uint), ParameterTypes.UInt);
            _parameterTypes.Add(typeof(long), ParameterTypes.Long);
            _parameterTypes.Add(typeof(ulong), ParameterTypes.ULong);
            _parameterTypes.Add(typeof(short), ParameterTypes.Short);
            _parameterTypes.Add(typeof(ushort), ParameterTypes.UShort);
            _parameterTypes.Add(typeof(string), ParameterTypes.String);
            _parameterTypes.Add(typeof(byte[]), ParameterTypes.ByteArray);
            _parameterTypes.Add(typeof(char[]), ParameterTypes.CharArray);
			_parameterTypes.Add(typeof(Type), ParameterTypes.Type);
        }

        internal void SendParameters(BinaryWriter writer, params object[] parameters)
        {
            MemoryStream ms;
            BinaryFormatter formatter = new BinaryFormatter();
            //write how many parameters are coming
            writer.Write(parameters.Length);
            //write data for each parameter
            foreach (object parameter in parameters)
            {
                if (parameter != null)
                {
                    Type type = parameter.GetType();
                    byte typeByte = GetParameterType(type);
                    //write the type byte
                    writer.Write(typeByte);
                    //write the parameter
                    switch (typeByte)
                    {
                        case ParameterTypes.Bool:
                            writer.Write((bool)parameter);
                            break;
                        case ParameterTypes.Byte:
                            writer.Write((byte)parameter);
                            break;
                        case ParameterTypes.ByteArray:
                            byte[] byteArray = (byte[])parameter;
                            writer.Write(byteArray.Length);
                            writer.Write(byteArray);
                            break;
                        case ParameterTypes.Char:
                            writer.Write((char)parameter);
                            break;
                        case ParameterTypes.CharArray:
                            char[] charArray = (char[])parameter;
                            writer.Write(charArray.Length);
                            writer.Write(charArray);
                            break;
                        case ParameterTypes.Decimal:
                            writer.Write((decimal)parameter);
                            break;
                        case ParameterTypes.Double:
                            writer.Write((double)parameter);
                            break;
                        case ParameterTypes.Float:
                            writer.Write((float)parameter);
                            break;
                        case ParameterTypes.Int:
                            writer.Write((int)parameter);
                            break;
                        case ParameterTypes.Long:
                            writer.Write((long)parameter);
                            break;
                        case ParameterTypes.SByte:
                            writer.Write((sbyte)parameter);
                            break;
                        case ParameterTypes.Short:
                            writer.Write((short)parameter);
                            break;
                        case ParameterTypes.String:
                            writer.Write((string)parameter);
                            break;
                        case ParameterTypes.UInt:
                            writer.Write((uint)parameter);
                            break;
                        case ParameterTypes.ULong:
                            writer.Write((ulong)parameter);
                            break;
                        case ParameterTypes.UShort:
                            writer.Write((ushort)parameter);
                            break;
						case ParameterTypes.Type:
							writer.Write(((Type)parameter).FullName);
							break;
                        case ParameterTypes.Unknown:
                            ms = new MemoryStream();
                            formatter.Serialize(ms, parameter);
                            ms.Seek(0, SeekOrigin.Begin);
                            //write length of data
                            writer.Write((int)ms.Length);
                            //write data
                            writer.Write(ms.ToArray());
                            break;
                        default:
                            throw new Exception(string.Format("Unknown type byte '0x{0:X}'", typeByte));
                    }
                }
                else
                    writer.Write(ParameterTypes.Null);
            }
        }

        internal object[] ReceiveParameters(BinaryReader reader)
        {
            int parameterCount = reader.ReadInt32();
            object[] parameters = new object[parameterCount];
            MemoryStream ms;
            BinaryFormatter formatter = new BinaryFormatter();
            for (int i = 0; i < parameterCount; i++)
            {
                //read type byte
                byte typeByte = reader.ReadByte();
                if (typeByte == ParameterTypes.Null)
                    parameters[i] = null;
                else
                {
                    int count;
                    switch (typeByte)
                    {
                        case ParameterTypes.Bool:
                            parameters[i] = reader.ReadBoolean();
                            break;
                        case ParameterTypes.Byte:
                            parameters[i] = reader.ReadByte();
                            break;
                        case ParameterTypes.ByteArray:
                            count = reader.ReadInt32();
                            parameters[i] = reader.ReadBytes(count);
                            break;
                        case ParameterTypes.Char:
                            parameters[i] = reader.ReadChar();
                            break;
                        case ParameterTypes.CharArray:
                            count = reader.ReadInt32();
                            parameters[i] = reader.ReadChars(count);
                            break;
                        case ParameterTypes.Decimal:
                            parameters[i] = reader.ReadDecimal();
                            break;
                        case ParameterTypes.Double:
                            parameters[i] = reader.ReadDouble();
                            break;
                        case ParameterTypes.Float:
                            parameters[i] = reader.ReadSingle();
                            break;
                        case ParameterTypes.Int:
                            parameters[i] = reader.ReadInt32();
                            break;
                        case ParameterTypes.Long:
                            parameters[i] = reader.ReadInt64();
                            break;
                        case ParameterTypes.SByte:
                            parameters[i] = reader.ReadSByte();
                            break;
                        case ParameterTypes.Short:
                            parameters[i] = reader.ReadInt16();
                            break;
                        case ParameterTypes.String:
                            parameters[i] = reader.ReadString();
                            break;
                        case ParameterTypes.UInt:
                            parameters[i] = reader.ReadUInt32();
                            break;
                        case ParameterTypes.ULong:
                            parameters[i] = reader.ReadUInt64();
                            break;
                        case ParameterTypes.UShort:
                            parameters[i] = reader.ReadUInt16();
                            break;
						case ParameterTypes.Type:
							var typeName = reader.ReadString();
							parameters[i] = Type.GetType(typeName);
							break;
                        case ParameterTypes.Unknown:
                            ms = new MemoryStream(reader.ReadBytes(reader.ReadInt32()));
                            //deserialize the parameter array
                            parameters[i] = formatter.Deserialize(ms);
                            break;
                        default:
                            throw new Exception(string.Format("Unknown type byte '0x{0:X}'", typeByte));
                    }
                }
            }
            return parameters;
        }

        private byte GetParameterType(Type type)
        {
            if (_parameterTypes.ContainsKey(type))
                return _parameterTypes[type];
            else
                return ParameterTypes.Unknown;
        }
    }
}
