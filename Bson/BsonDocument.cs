using System.Collections.Generic;
using System.Linq;
using CSMongo.IO;
using System.IO;

namespace CSMongo.Bson {
    
    /// <summary>
    /// Creates a BSON document where the field order is managed
    /// </summary>
    public class BsonDocument : BsonObject {

        #region Constants

        private const int DocumentLengthCount = 4;
        private const int DocumentTerminatorCount = 1;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new empty BSON document
        /// </summary>
        public BsonDocument()
        {
        }

        /// <summary>
        /// Creates a new BSON document based on the source provided
        /// </summary>
        public BsonDocument(object source)
            : base(source) {
        }

        #endregion

        #region Operators

        /// <summary>
        /// Merges another object with this object - Same as calling Merge
        /// </summary>
        public static BsonDocument operator +(BsonDocument target, object value) {
            target.Merge(value);
            return target;
        }

        /// <summary>
        /// Merges another DynamicObject with this object - Same as calling Merge
        /// </summary>
        public static BsonDocument operator +(BsonDocument target, BsonObject value) {
            target.Merge(value);
            return target;
        }

        /// <summary>
        /// Removes the field provided - Same as calling Remove
        /// </summary>
        public static BsonDocument operator -(BsonDocument target, string field) {
            target.Remove(field);
            return target;
        }

        /// <summary>
        /// Removes the field provided - Same as calling Remove
        /// </summary>
        public static BsonDocument operator -(BsonDocument target, string[] fields) {
            target.Remove(fields);
            return target;
        }

        #endregion

        #region Rendering Changes

        /// <summary>
        /// Renders the bytes required to create a document
        /// </summary>
        public override byte[] ToBsonByteArray() {

            //create the default size
            var stream = new DynamicStream(5);

            //generate the bytes
            stream.InsertAt(4, base.ToBsonByteArray());

            //update the length
            stream.WriteAt(0, BsonTranslator.AsInt32(stream.Length));

            //and return the bytes to use
            return stream.ToArray();

        }

        #endregion

        #region Static Creation

        /// <summary>
        /// Reads an incoming BSON document from a stream
        /// </summary>
        public static BsonDocument FromStream(Stream stream) {

            //read the first byte to determine the type
            var reader = new BinaryReader(stream);

            //get the length of this document and the amount
            //that should be read into the parsing stream.
            //removes 4 bytes to account for the length value
            //and an additional 1 byte to account for the
            //terminator value
            var length = reader.ReadInt32();
            int read = length - (DocumentLengthCount + DocumentTerminatorCount);

            //read out the bytes to use and the terminator
            var bytes = reader.ReadBytes(read);
            reader.ReadByte();

            //use the bytes to generate the document
            using (var content = new MemoryStream(bytes)) {

                //read the content
                Dictionary<string, object> values = BsonTranslator.FromStream(content);

                //fill and return the object
                var document = new BsonDocument();
                document.Merge(values);
                return document;

            }

        }

        #endregion

        /// <summary>
        /// Appends the specified field and value to the document and returns the original collection.
        /// </summary>
        /// <param name="field">The field to set.</param>
        /// <param name="document">The document to use as the value.</param>
        /// <returns></returns>
        public BsonDocument AppendField(string field, object document)
        {
            this[field] = document;
            return this;
        }

        /// <summary>
        /// Attempts to recreate fields by appending @ in front of the name. This is to prevent the fieldname from splitting the field since the name contains a .
        /// </summary>
        /// <returns></returns>
        public BsonDocument NormalizeCompoundFieldNames()
        {
            foreach (var name in GetFieldNames().Where(name => name.Contains(".") && !name.StartsWith("@")))
            {
                this["@@" + name] = this["@"+name];
                Remove("@"+name);
            }
            return this;
        }
    }

}
