using S7.Net.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace S7.Net.Types
{
    /// <summary>
    /// Contains the methods to convert a C# class to S7 data types
    /// </summary>
    public static class Class
    {
        private static IEnumerable<PropertyInfo> GetAccessableProperties(Type classType)
        {
            return classType
#if NETSTANDARD1_3
                .GetTypeInfo().DeclaredProperties.Where(p => p.SetMethod != null);
#else
                .GetProperties(
                    BindingFlags.SetProperty |
                    BindingFlags.Public |
                    BindingFlags.Instance)
                .Where(p => p.GetSetMethod() != null);
#endif

        }

        private static double GetIncreasedNumberOfBytes(double numBytes, Type type, PropertyInfo? propertyInfo, CpuType cpu)
        {
            switch (type.Name)
            {
                case "Boolean":
                    numBytes += 0.125;
                    break;
                case "Byte":
                    numBytes = Math.Ceiling(numBytes);
                    numBytes++;
                    break;
                case "Int16":
                case "UInt16":
                    ByteHelper.IncrementToEven(ref numBytes);
                    numBytes += 2;
                    break;
                case "Int32":
                case "UInt32":
                    ByteHelper.IncrementToEven(ref numBytes);
                    numBytes += 4;
                    break;
                case "Single":
                    ByteHelper.IncrementToEven(ref numBytes);
                    numBytes += 4;
                    break;
                case "Double":
                    ByteHelper.IncrementToEven(ref numBytes);
                    numBytes += 8;
                    break;
                case "DateTime":
                    // https://support.industry.siemens.com/cs/document/43566349/in-step-7-(tia-portal)-how-can-you-input-read-out-and-edit-the-date-and-time-for-the-cpu-modules-?dti=0&lc=en-WW
                    // Per Siemens documentation, DateTime structures are model specific, and compatibility to exchange types
                    // is not supported by Siemens.
                    S7DateTimeAttribute? dateAttribute = propertyInfo?.GetCustomAttributes<S7DateTimeAttribute>().SingleOrDefault();

                    if (dateAttribute == default(S7DateTimeAttribute))
                        throw new ArgumentException($"Please add {nameof(S7DateTimeAttribute)} to the datetime field {propertyInfo.Name} in class {propertyInfo.DeclaringType.Name}.");
                    else if (dateAttribute == null)
                        dateAttribute = cpu switch
                        {
                            CpuType.S71200 => new S7DateTimeAttribute(S7DateTimeType.DTL),
                            CpuType.S71500 => new S7DateTimeAttribute(S7DateTimeType.DTL),
                            _ => new S7DateTimeAttribute(S7DateTimeType.DT),
                        };

                    ByteHelper.IncrementToEven(ref numBytes);
                    numBytes += dateAttribute.ByteLength;
                    break;
                case "String":
                    S7StringAttribute? attribute = propertyInfo?.GetCustomAttributes<S7StringAttribute>().SingleOrDefault();

                    if (attribute == default(S7StringAttribute))
                        throw new ArgumentException($"Please add {nameof(S7StringAttribute)} to the string field {propertyInfo.Name} in class {propertyInfo.DeclaringType.Name}.");
                    else if (attribute == null)
                        attribute = new S7StringAttribute(S7StringType.S7String, 254);

                    ByteHelper.IncrementToEven(ref numBytes);
                    numBytes += attribute.ReservedLengthInBytes;
                    break;
                case "Int64":
                case "UInt64":
                    ByteHelper.IncrementToEven(ref numBytes);
                    numBytes += 8;
                    break;
                default:
                    var propertyClass = Activator.CreateInstance(type) ??
                        throw new ArgumentException($"Failed to create instance of type {type}.", nameof(type));

                    numBytes = GetClassSize(propertyClass, numBytes, true);
                    break;
            }

            return numBytes;
        }

        /// <summary>
        /// Gets the size of the class in bytes.
        /// </summary>
        /// <param name="instance">An instance of the class</param>
        /// <param name="numBytes">The offset of the current field.</param>
        /// <param name="isInnerProperty"><see langword="true" /> if this property belongs to a class being serialized as member of the class requested for serialization; otherwise, <see langword="false" />.</param>
        /// <returns>the number of bytes</returns>
        public static double GetClassSize(object instance, double numBytes = 0.0, bool isInnerProperty = false, CpuType cpu = CpuType.S71500)
        {
            var properties = GetAccessableProperties(instance.GetType());
            foreach (var property in properties)
            {
                if (property.PropertyType.IsArray)
                {
                    Type elementType = property.PropertyType.GetElementType()!;

                    Array array = (Array?)property.GetValue(instance, null) ??
                        throw new ArgumentException($"Property {property.Name} on {instance} must have a non-null value to get its size.", nameof(instance));

                    if (array.Length <= 0)
                        throw new Exception($"Cannot determine the size of the class because the array {property.Name} is defined in class {property.DeclaringType.Name} which has no fixed size greater than zero.");

                    ByteHelper.IncrementToEven(ref numBytes);

                    for (int i = 0; i < array.Length; i++)
                    {
                        numBytes = GetIncreasedNumberOfBytes(numBytes, elementType, property, cpu);
                    }
                }
                else
                {
                    numBytes = GetIncreasedNumberOfBytes(numBytes, property.PropertyType, property, cpu);
                }
            }

            if (false == isInnerProperty)
                // enlarge numBytes to next even number because S7-Structs in a DB always will be resized to an even byte count
                ByteHelper.IncrementToEven(ref numBytes);

            return numBytes;
        }

        private static object? GetPropertyValue(Type propertyType, PropertyInfo? propertyInfo, byte[] bytes, ref double numBytes, CpuType cpu)
        {
            object? value = null;

            switch (propertyType.Name)
            {
                case "Boolean":
                    // get the value
                    int bytePos = (int)Math.Floor(numBytes);
                    int bitPos = (int)((numBytes - (double)bytePos) / 0.125);
                    if ((bytes[bytePos] & (int)Math.Pow(2, bitPos)) != 0)
                        value = true;
                    else
                        value = false;
                    numBytes += 0.125;
                    break;
                case "Byte":
                    numBytes = Math.Ceiling(numBytes);
                    value = (byte)(bytes[(int)numBytes]);
                    numBytes++;
                    break;
                case "Int16":
                    ByteHelper.IncrementToEven(ref numBytes);
                    // hier auswerten
                    ushort source = Word.FromBytes(bytes[(int)numBytes + 1], bytes[(int)numBytes]);
                    value = source.ConvertToShort();
                    numBytes += 2;
                    break;
                case "UInt16":
                    ByteHelper.IncrementToEven(ref numBytes);
                    // hier auswerten
                    value = Word.FromBytes(bytes[(int)numBytes + 1], bytes[(int)numBytes]);
                    numBytes += 2;
                    break;
                case "Int32":
                    ByteHelper.IncrementToEven(ref numBytes);
                    var wordBuffer = new byte[4];
                    Array.Copy(bytes, (int)numBytes, wordBuffer, 0, wordBuffer.Length);
                    uint sourceUInt = DWord.FromByteArray(wordBuffer);
                    value = sourceUInt.ConvertToInt();
                    numBytes += 4;
                    break;
                case "UInt32":
                    ByteHelper.IncrementToEven(ref numBytes);
                    var wordBuffer2 = new byte[4];
                    Array.Copy(bytes, (int)numBytes, wordBuffer2, 0, wordBuffer2.Length);
                    value = DWord.FromByteArray(wordBuffer2);
                    numBytes += 4;
                    break;
                case "Single":
                    ByteHelper.IncrementToEven(ref numBytes);
                    // hier auswerten
                    value = Real.FromByteArray(
                        new byte[] {
                            bytes[(int)numBytes],
                            bytes[(int)numBytes + 1],
                            bytes[(int)numBytes + 2],
                            bytes[(int)numBytes + 3] });
                    numBytes += 4;
                    break;
                case "Int64":
                    ByteHelper.IncrementToEven(ref numBytes);
                    ulong sourceULInt = LWord.FromByteArray(
                        new byte[] {
                            bytes[(int)numBytes],
                            bytes[(int)numBytes + 1],
                            bytes[(int)numBytes + 2],
                            bytes[(int)numBytes + 3],
                            bytes[(int)numBytes + 4],
                            bytes[(int)numBytes + 5],
                            bytes[(int)numBytes + 6],
                            bytes[(int)numBytes + 7] });
                    value = sourceULInt.ConvertToLong();
                    numBytes += 8;
                    break;
                case "UInt64":
                    ByteHelper.IncrementToEven(ref numBytes);
                    value = LWord.FromByteArray(
                        new byte[] {
                            bytes[(int)numBytes],
                            bytes[(int)numBytes + 1],
                            bytes[(int)numBytes + 2],
                            bytes[(int)numBytes + 3],
                            bytes[(int)numBytes + 4],
                            bytes[(int)numBytes + 5],
                            bytes[(int)numBytes + 6],
                            bytes[(int)numBytes + 7] });
                    numBytes += 8;
                    break;
                case "Double":
                    ByteHelper.IncrementToEven(ref numBytes);
                    var buffer = new byte[8];
                    Array.Copy(bytes, (int)numBytes, buffer, 0, 8);
                    // hier auswerten
                    value = LReal.FromByteArray(buffer);
                    numBytes += 8;
                    break;
                case "DateTime":
                    numBytes = Math.Ceiling(numBytes);
                    if ((numBytes / 2 - Math.Floor(numBytes / 2.0)) > 0)
                        numBytes++;
                    // https://support.industry.siemens.com/cs/document/43566349/in-step-7-(tia-portal)-how-can-you-input-read-out-and-edit-the-date-and-time-for-the-cpu-modules-?dti=0&lc=en-WW
                    // Per Siemens documentation, DateTime structures are model specific, and compatibility to exchange types
                    // is not supported by Siemens.

                    // If the property does not have a S7DateTimeAttribute set, then set a default attribute based on what
                    // the CPU's default DateTime parsing mechanism is

                    S7DateTimeAttribute? dateAttribute = propertyInfo?.GetCustomAttributes<S7DateTimeAttribute>().SingleOrDefault();

                    if (dateAttribute == default(S7DateTimeAttribute))
                        throw new ArgumentException($"Please add {nameof(S7DateTimeAttribute)} to the datetime field {propertyInfo.Name} in class {propertyInfo.DeclaringType.Name}.");
                    else if (dateAttribute == null)
                        dateAttribute = cpu switch
                        {
                            CpuType.S71200 => new S7DateTimeAttribute(S7DateTimeType.DTL),
                            _ => new S7DateTimeAttribute(S7DateTimeType.DT),
                        };

                    ByteHelper.IncrementToEven(ref numBytes);

                    // get the value
                    var dateData = new byte[dateAttribute.ByteLength];
                    Array.Copy(bytes, (int)numBytes, dateData, 0, dateData.Length);

                    switch (cpu)
                    {
                        case CpuType.S71500:
                            value = dateAttribute.Type switch
                            {
                                S7DateTimeType.DTL => DateTimeLong.FromByteArray(dateData),
                                _ => DateTime.FromByteArray(dateData)
                            };
                            break;
                        case CpuType.S71200:
                            value = DateTimeLong.FromByteArray(dateData);
                            break;
                        default:
                            value = DateTime.FromByteArray(dateData);
                            break;
                    }

                    numBytes += dateData.Length;
                    break;

                case "String":
                    S7StringAttribute? attribute = propertyInfo?.GetCustomAttributes<S7StringAttribute>().SingleOrDefault();

                    if (attribute == default(S7StringAttribute))
                        throw new ArgumentException($"Please add {nameof(S7StringAttribute)} to the string field {propertyInfo.Name} in class {propertyInfo.DeclaringType.Name}.");
                    else if (attribute == null)
                        attribute = new S7StringAttribute(S7StringType.S7String, 254);

                    ByteHelper.IncrementToEven(ref numBytes);

                    // get the value
                    var sData = new byte[attribute.ReservedLengthInBytes];
                    Array.Copy(bytes, (int)numBytes, sData, 0, sData.Length);
                    value = attribute.Type switch
                    {
                        S7StringType.S7String => S7String.FromByteArray(sData),
                        S7StringType.S7WString => S7WString.FromByteArray(sData),
                        _ => throw new ArgumentException($"Please use a valid string type for the {nameof(S7StringAttribute)} on property {propertyInfo.Name} in class {propertyInfo.DeclaringType.Name}.")
                    };
                    numBytes += sData.Length;
                    break;
                default:
                    var propClass = Activator.CreateInstance(propertyType) ??
                        throw new ArgumentException($"Failed to create instance of type {propertyType} in class {propertyInfo.DeclaringType.Name}.", nameof(propertyType));

                    numBytes = FromBytes(propClass, bytes, numBytes);
                    value = propClass;
                    break;
            }

            return value;
        }

        /// <summary>
        /// Sets the object's values with the given array of bytes
        /// </summary>
        /// <param name="sourceClass">The object to fill in the given array of bytes</param>
        /// <param name="bytes">The array of bytes</param>
        /// <param name="numBytes">The offset for the current field.</param>
        /// <param name="isInnerClass"><see langword="true" /> if this class is the type of a member of the class to be serialized; otherwise, <see langword="false" />.</param>
        public static double FromBytes(object sourceClass, byte[] bytes, double numBytes = 0, bool isInnerClass = false, CpuType cpu = CpuType.S71500)
        {
            if (bytes == null)
                return numBytes;

            var properties = GetAccessableProperties(sourceClass.GetType());
            foreach (var property in properties)
            {
                if (property.PropertyType.IsArray)
                {
                    Array array = (Array?)property.GetValue(sourceClass, null) ??
                        throw new ArgumentException($"Property {property.Name} on sourceClass must be an array instance.", nameof(sourceClass));

                    ByteHelper.IncrementToEven(ref numBytes);
                    Type elementType = property.PropertyType.GetElementType()!;
                    for (int i = 0; i < array.Length && numBytes < bytes.Length; i++)
                    {
                        array.SetValue(
                            GetPropertyValue(elementType, property, bytes, ref numBytes, cpu),
                            i);
                    }
                }
                else
                {
                    property.SetValue(
                        sourceClass,
                        GetPropertyValue(property.PropertyType, property, bytes, ref numBytes, cpu),
                        null);
                }
            }

            return numBytes;
        }

        private static double SetBytesFromProperty(object propertyValue, PropertyInfo? propertyInfo, byte[] bytes, double numBytes, CpuType cpu)
        {
            int bytePos = 0;
            int bitPos = 0;
            byte[]? bytes2 = null;

            switch (propertyValue.GetType().Name)
            {
                case "Boolean":
                    // get the value
                    bytePos = (int)Math.Floor(numBytes);
                    bitPos = (int)((numBytes - (double)bytePos) / 0.125);
                    if ((bool)propertyValue)
                        bytes[bytePos] |= (byte)Math.Pow(2, bitPos);            // is true
                    else
                        bytes[bytePos] &= (byte)(~(byte)Math.Pow(2, bitPos));   // is false
                    numBytes += 0.125;
                    break;
                case "Byte":
                    numBytes = (int)Math.Ceiling(numBytes);
                    bytePos = (int)numBytes;
                    bytes[bytePos] = (byte)propertyValue;
                    numBytes++;
                    break;
                case "Int16":
                    bytes2 = Int.ToByteArray((Int16)propertyValue);
                    break;
                case "UInt16":
                    bytes2 = Word.ToByteArray((UInt16)propertyValue);
                    break;
                case "Int32":
                    bytes2 = DInt.ToByteArray((Int32)propertyValue);
                    break;
                case "UInt32":
                    bytes2 = DWord.ToByteArray((UInt32)propertyValue);
                    break;
                case "Int64":
                    bytes2 = LInt.ToByteArray((Int64)propertyValue);
                    break;
                case "UInt64":
                    bytes2 = LWord.ToByteArray((UInt64)propertyValue);
                    break;
                case "Single":
                    bytes2 = Real.ToByteArray((float)propertyValue);
                    break;
                case "Double":
                    bytes2 = LReal.ToByteArray((double)propertyValue);
                    break;
                case "DateTime":
                    S7DateTimeAttribute? dateAttribute = propertyInfo?.GetCustomAttributes<S7DateTimeAttribute>().SingleOrDefault();

                    if (dateAttribute == default(S7DateTimeAttribute))
                        throw new ArgumentException($"Please add {nameof(S7DateTimeAttribute)} to the datetime field {propertyInfo.Name} in class {propertyInfo.DeclaringType.Name}.");
                    else if (dateAttribute == null)
                        dateAttribute = cpu switch
                        {
                            CpuType.S71200 => new S7DateTimeAttribute(S7DateTimeType.DTL),
                            _ => new S7DateTimeAttribute(S7DateTimeType.DT),
                        };

                    bytes2 = dateAttribute.Type switch
                    {
                        S7DateTimeType.DTL => DateTimeLong.ToByteArray((System.DateTime)propertyValue),
                        _ => DateTime.ToByteArray((System.DateTime)propertyValue)
                    };

                    break;
                case "String":
                    S7StringAttribute? attribute = propertyInfo?.GetCustomAttributes<S7StringAttribute>().SingleOrDefault();

                    if (attribute == default(S7StringAttribute))
                        throw new ArgumentException($"Please add {nameof(S7StringAttribute)} to the string field {propertyInfo.Name} in class {propertyInfo.DeclaringType.Name}.");

                    bytes2 = attribute.Type switch
                    {
                        S7StringType.S7String => S7String.ToByteArray((string)propertyValue, attribute.ReservedLength),
                        S7StringType.S7WString => S7WString.ToByteArray((string)propertyValue, attribute.ReservedLength),
                        _ => throw new ArgumentException($"Please use a valid string type for the {nameof(S7StringAttribute)} on property {propertyInfo.Name} in class {propertyInfo.DeclaringType.Name}.")
                    };
                    break;
                default:
                    numBytes = ToBytes(propertyValue, bytes, numBytes, cpu: cpu);
                    break;
            }

            if (bytes2 != null)
            {
                ByteHelper.IncrementToEven(ref numBytes);

                bytePos = (int)numBytes;

                for (int bCnt = 0; bCnt < bytes2.Length; bCnt++)
                    bytes[bytePos + bCnt] = bytes2[bCnt];

                numBytes += bytes2.Length;
            }

            return numBytes;
        }

        /// <summary>
        /// Creates a byte array depending on the struct type.
        /// </summary>
        /// <param name="sourceClass">The struct object.</param>
        /// <param name="bytes">The target byte array.</param>
        /// <param name="numBytes">The offset for the current field.</param>
        /// <param name="cpu"></param>
        /// <returns>A byte array or null if fails.</returns>
        public static double ToBytes(object sourceClass, byte[] bytes, double numBytes = 0.0, CpuType cpu = CpuType.S71500)
        {
            var properties = GetAccessableProperties(sourceClass.GetType());
            foreach (var property in properties)
            {
                var value = property.GetValue(sourceClass, null) ??
                    throw new ArgumentException($"Property {property.Name} on sourceClass can't be null.", nameof(sourceClass));

                if (property.PropertyType.IsArray)
                {
                    Array array = (Array)value;
                    ByteHelper.IncrementToEven(ref numBytes);
                    for (int i = 0; i < array.Length && numBytes < bytes.Length; i++)
                    {
                        numBytes = SetBytesFromProperty(array.GetValue(i)!, property, bytes, numBytes, cpu);
                    }
                }
                else
                {
                    numBytes = SetBytesFromProperty(value, property, bytes, numBytes, cpu);
                }
            }

            return numBytes;
        }
    }
}
