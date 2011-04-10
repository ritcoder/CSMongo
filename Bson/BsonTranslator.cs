﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using CSMongo.IO;
using CSMongo.Types;
using System.Text.RegularExpressions;
using CSMongo.Extensions;

namespace CSMongo.Bson {
    
    /// <summary>
    /// Handles translating bytes to and from BSON format
    /// </summary>
    public class BsonTranslator {

        #region Reading From Stream

        /// <summary>
        /// Handles reading a type from a stream
        /// </summary>
        public static Dictionary<string, object> FromStream(Stream stream) {

            //start reading out the values
            var values = new Dictionary<string, object>();
            while (stream.Position < stream.Length) {

                //get the type to use
                var type = (MongoDataTypes)stream.ReadByte();

                //get the information for this value
                var field = ReadString(stream);
                var value = ParseValueFromStream(type, stream);

                //remove just in case and add the item
                if (string.IsNullOrEmpty(field)) { continue; }
                values.Remove(field);
                values.Add(field, value);

                //if there is a zero byte then ignore it
                //the caller should have taken care of this though
                if (stream.Position == stream.Length - 1) { break; }

            }

            //return the final document values
            return values;

        }
         
        //uses a type to determine how to read a value
        private static object ParseValueFromStream(MongoDataTypes type, Stream stream) {

            //check the types provided
            switch (type) {
                case MongoDataTypes.Oid:
                    return ReadOid(stream);
                case MongoDataTypes.String:
                    return ReadCString(stream);
                case MongoDataTypes.Int32:
                    return ReadInt32(stream);
                case MongoDataTypes.Int64:
                    return ReadInt64(stream);
                case MongoDataTypes.Date:
                    return ReadDate(stream);
                case MongoDataTypes.Number:
                    return ReadNumber(stream);
                case MongoDataTypes.Boolean:
                    return ReadBoolean(stream);
                case MongoDataTypes.Binary:
                    return ReadBinary(stream);
                case MongoDataTypes.Array:
                    return ReadArray(stream);
                case MongoDataTypes.Regex:
                    return ReadRegularExpression(stream);
                case MongoDataTypes.Object:
                    return ReadObject(stream);
                default:
                    return null;
            }

        }

        #endregion

        #region To BSON Format

        /// <summary>
        /// Creates the bytes for an integer in BSON format
        /// </summary>
        public static byte[] AsByte(byte value) {
            return new[] { value };
        }

        /// <summary>
        /// Writes a binary object to the Mongo database
        /// </summary>
        public static byte[] AsBinary(byte[] value) {
            var stream = new DynamicStream(4);

            //write the kinds of stream this is
            //for now default to User-Defined
            stream.Append((byte)MongoBinaryTypes.UserDefined);

            //write the bytes and update the length
            stream.Append(value);
            stream.WriteAt(0, BitConverter.GetBytes(value.Length));

            //and return the final bytes to use
            return stream.ToArray();

        }

        /// <summary>
        /// Creates the bytes for an integer in BSON format
        /// </summary>
        public static byte[] AsInt32(int value) {
            return BitConverter.GetBytes(value);
        }

        /// <summary>
        /// Creates the bytes for a number in BSON format
        /// </summary>
        public static byte[] AsNumber(double value) {
            return BitConverter.GetBytes(value);
        }

        /// <summary>
        /// Creates the bytes for a boolean in BSON format
        /// </summary>
        public static byte[] AsBoolean(bool value) {
            return BitConverter.GetBytes(value);
        }

        /// <summary>
        /// Creates the bytes for a unsigned long in BSON format
        /// </summary>
        public static byte[] AsUInt64(ulong value) {
            return BitConverter.GetBytes(value);
        }

        /// <summary>
        /// Creates the bytes for a long in BSON format
        /// </summary>
        public static byte[] AsInt64(long value) {
            return BitConverter.GetBytes(value);
        }

        /// <summary>
        /// Writes a the bytes for a CS string in BSON format
        /// </summary>
        public static byte[] AsCString(string value) {
            var stream = new DynamicStream();
            stream.Append(AsString(value));
            stream.InsertAt(0, BitConverter.GetBytes(stream.Length));
            return stream.ToArray();
        }

        /// <summary>
        /// Creates the bytes for a regular string in BSON format
        /// </summary>
        public static byte[] AsString(string value) {
            var stream = new DynamicStream();
            stream.Append(Encoding.UTF8.GetBytes(value));
            stream.Append(0);
            return stream.ToArray();
        }

        /// <summary>
        /// Writes the null value in BSON format
        /// </summary>
        public static byte[] AsNull() {
            return new byte[] { };
        }

        /// <summary>
        /// Returns the bytes required for the MongoOid
        /// </summary>
        public static byte[] AsOid(MongoOid value) {
            return value.Value;
        }

        /// <summary>
        /// Writes the bytes for an array of values
        /// </summary>
        public static byte[] AsArray(IEnumerable<object> array) {
            array = array ?? new object[] { };

            //simply create a Document with each index as
            //the value of the index of the item
            var result = new BsonDocument();
            for(var i = 0; i < array.Count(); i++) {
                result.Set(i.ToString(), array.ElementAt(i));
            }

            //then generate the bytes
            return result.ToBsonByteArray();
        }

        /// <summary>
        /// Writes a date in BSON format
        /// </summary>
        public static byte[] AsDate(DateTime value) {
            
            //get the tick count
            var difference = value.ToUniversalTime() - Mongo.Epoch;
            var span = Convert.ToInt64(Math.Floor(difference.TotalMilliseconds));

            //and write as an int
            return AsInt64(span);

        }

        /// <summary>
        /// Writes a regex in BSON format
        /// </summary>
        /// <param name="value">The regex instance</param>
        /// <returns>regex as bson byte arrray</returns>
        public static byte[] AsRegex(Regex value)
        {
            var optionsStr = "";
            var flags = value.Options;
            if ((flags & RegexOptions.IgnoreCase) == RegexOptions.IgnoreCase) optionsStr += "i";
            if ((flags & RegexOptions.CultureInvariant) == RegexOptions.CultureInvariant) optionsStr += "l";
            if ((flags & RegexOptions.Multiline) == RegexOptions.Multiline) optionsStr += "m";
            if ((flags & RegexOptions.Singleline) == RegexOptions.Singleline) optionsStr += "s";
            if ((flags & RegexOptions.IgnorePatternWhitespace) == RegexOptions.IgnorePatternWhitespace) optionsStr += "x";
            var stream = new DynamicStream();
            stream.Append(AsString(value.ToString()));
            stream.Append(AsString(optionsStr));
            return stream.ToArray();
        }
        #endregion

        #region From BSON Format

        /// <summary>
        /// Reads a regular expression from the stream
        /// </summary>
        public static Regex ReadRegularExpression(Stream stream) {
            var expression = ReadString(stream);
            var options = ReadString(stream);

            //create the new Regex options value
            var flag = RegexOptions.None;
            if (options.Contains("i")) { flag = flag.Include(RegexOptions.IgnoreCase); }
            if (options.Contains("l")) { flag = flag.Include(RegexOptions.CultureInvariant); }
            if (options.Contains("m")) { flag = flag.Include(RegexOptions.Multiline); }
            if (options.Contains("s")) { flag = flag.Include(RegexOptions.Singleline); }
            if (options.Contains("x")) { flag = flag.Include(RegexOptions.IgnorePatternWhitespace); }

            //return the restored object
            return new Regex(expression, flag);
        }

        /// <summary>
        /// Reads a binary object from the stream
        /// </summary>
        public static byte[] ReadBinary(Stream stream) {
            var reader = new BinaryReader(stream);

            //get the length and flag
            var length = reader.ReadInt32();
            
            //read the type identifier (we don't need to use it)
            //MongoBinaryTypes type = (MongoBinaryTypes)reader.ReadByte();
            reader.ReadByte();

            //then read the binary object (take in account the starting info)
            return reader.ReadBytes(length);

        }

        /// <summary>
        /// Reads a MongoDocument from the stream
        /// </summary>
        public static BsonDocument ReadObject(Stream stream) {
            return BsonDocument.FromStream(stream);
        }

        /// <summary>
        /// Reads a boolean value from the stream
        /// </summary>
        public static bool ReadBoolean(Stream stream) {
            var reader = new BinaryReader(stream);
            return reader.ReadBoolean();
        }

        /// <summary>
        /// Reads a number value from the stream
        /// </summary>
        public static double ReadNumber(Stream stream) {
            var reader = new BinaryReader(stream);
            return reader.ReadDouble();
        }

        /// <summary>
        /// Reads an int32 value from the stream
        /// </summary>
        public static int ReadInt32(Stream stream) {
            var reader = new BinaryReader(stream);
            return reader.ReadInt32();
        }

        /// <summary>
        /// Handles reading int64 value from the stream
        /// </summary>
        public static object ReadInt64(Stream stream) {
            var reader = new BinaryReader(stream);
            return reader.ReadInt64();
        }

        /// <summary>
        /// Reads an Oid value from a BSON object
        /// </summary>
        public static MongoOid ReadOid(Stream stream) {
            var reader = new BinaryReader(stream);
            var bytes = reader.ReadBytes(12);
            return new MongoOid(bytes);
        }

        /// <summary>
        /// Reads a string value from a BSON object (zero terminated)
        /// </summary>
        public static string ReadString(Stream stream) {
            var reader = new BinaryReader(stream);

            //gather all of the bytes to use
            var bytes = new List<byte>();
            while (true) {
                var character = reader.ReadByte();

                //if the zero byte, give up
                if (character <= 0) { break; }
                bytes.Add(character);
            }

            //return the correctly encoded values
            return Encoding.UTF8.GetString(bytes.ToArray());

        }

        /// <summary>
        /// Reads a cstring value using the length provided as the object value
        /// </summary>
        public static string ReadCString(Stream stream) {
            var reader = new BinaryReader(stream);

            //get the 'length' value - Since we are currently
            //reading CStrings like normal strings we don't
            //actually need to use this value
            //int length = reader.ReadInt32();
            reader.ReadInt32();

            //just use the typical string reading for now. 
            //not sure if this needs to be something special
            return ReadString(stream);

        }

        /// <summary>
        /// Handles reading the null value from the stream
        /// </summary>
        public static object ReadNull(Stream stream) {
            return null;
        }

        /// <summary>
        /// Reads incoming values from the stream as an array
        /// </summary>
        public static object ReadArray(Stream stream) {
            BsonDocument collection = ReadObject(stream);
            return collection.GetValues().Select(item => item.Value).ToArray();
        }

        /// <summary>
        /// Reads a stream to get a date time value
        /// </summary>
        public static object ReadDate(Stream stream) {
            BinaryReader reader = new BinaryReader(stream);
            long value = reader.ReadInt64();

            //try and perform the conversion - If there
            //is a problem use the default Mongo date
            try {
                return Mongo.Epoch.AddMilliseconds(value);
            }
            catch {
                return Mongo.Epoch;
            }
        }

        #endregion

    }

}
