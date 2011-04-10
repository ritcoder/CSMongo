using System;
using CSMongo.Bson;
using System.IO;
using CSMongo.Types;

namespace CSMongo.DataTypes {

    /// <summary>
    /// Default class to handle long int values
    /// </summary>
    public class MongoInt64Type : MongoDataType {

        /// <summary>
        /// Returns the Mongo type for this data
        /// </summary>
        public override MongoDataTypes GetDataType() {
            return MongoDataTypes.Int64;
        }

        /// <summary>
        /// Returns if the value passed in is allowed to be used
        /// </summary>
        public override bool IsAllowedValue<T>(T value) {
            return value is long || value is long? ||
                value is uint || value is uint? ||
                value is ulong || value is ulong?;
        }

        /// <summary>
        /// Handles converting the value into an appropriate type
        /// </summary>
        protected override object ConvertValue<T>(object value) {
            var change = typeof(T);
            return change.IsEnum ? Enum.Parse(change, (Value ?? string.Empty).ToString(), true) : base.ConvertValue<T>(value);
        }

        /// <summary>
        /// Sets the current value of this MongoNumber
        /// </summary>
        public override void Set<T>(T value) {
            Value = Convert.ToInt64(value);
        }

        /// <summary>
        /// Converts this value into a series of BSON bytes
        /// </summary>
        public override byte[] ToBson() {
            return BsonTranslator.AsInt64(Value as long? ?? default(long));
        }

        /// <summary>
        /// Reads the value from a stream
        /// </summary>
        public override object FromBsonStream(Stream stream) {
            return BsonTranslator.ReadInt64(stream);
        }

    }

}
