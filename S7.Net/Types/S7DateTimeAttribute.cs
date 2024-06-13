using System;

namespace S7.Net.Types
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class S7DateTimeAttribute : Attribute
    {
        private readonly S7DateTimeType type;

        /// <summary>
        /// Initializes a new instance of the <see cref="S7DateTimeAttribute"/> class.
        /// </summary>
        /// <param name="type">The datetime type.</param>
        /// <exception cref="ArgumentException">Please use a valid value for the datetime type</exception>
        public S7DateTimeAttribute(S7DateTimeType type)
        {
            if (!Enum.IsDefined(typeof(S7DateTimeType), type))
                throw new ArgumentException("Please use a valid value for the datetime type");

            this.type = type;
        }

        /// <summary>
        /// Gets the type of the datetime.
        /// </summary>
        /// <value>
        /// The string type.
        /// </value>
        public S7DateTimeType Type => type;

        /// <summary>
        /// Gets the length of the datetime in bytes.
        /// </summary>
        /// <value>
        /// The length in bytes.
        /// </value>
        public int ByteLength => type == S7DateTimeType.DTL ? 12 : 8;
    }

    public enum S7DateTimeType
    {
        DT = VarType.DateTime,
        DTL = VarType.DateTimeLong
    }
}
