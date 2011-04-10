using System.IO;
using System.Text.RegularExpressions;
using CSMongo.Bson;
using CSMongo.Types;

namespace CSMongo.DataTypes
{
    /// <summary>
    /// Regular expression data type
    /// </summary>
    public class MongoRegexType: MongoDataType
    {
        #region Overrides of MongoDataType

        /// <summary>
        /// Used to determine the Mongo Op code for this type
        /// </summary>
        public override MongoDataTypes GetDataType()
        {
            return MongoDataTypes.Regex;
        }

        /// <summary>
        /// Determines if this type can be cast into the requested type
        /// </summary>
        public override bool IsAllowedValue<T>(T value)
        {
            return value is Regex;
        }

        /// <summary>
        /// Writes a value to BSON format
        /// </summary>
        public override byte[] ToBson()
        {
            return BsonTranslator.AsRegex(Value as Regex);
        }

        /// <summary>
        /// Handles reading the content to find the value of an object
        /// </summary>
        public override object FromBsonStream(Stream stream)
        {
            return BsonTranslator.ReadRegularExpression(stream);
        }

        protected override object ConvertValue<T>(object value)
        {
            return value is Regex ? value : null;
        }
        #endregion
    }
}
