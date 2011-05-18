using System.Collections.Generic;
using System.Linq;

namespace CSMongo.IO {

    /// <summary>
    /// Works with bytes that could be modified in different locations
    /// at any given time
    /// </summary>
    public class DynamicStream {

        #region Constructors

        /// <summary>
        /// Creates an empty DynamicStream
        /// </summary>
        public DynamicStream()
           : this(0) {
        }

        /// <summary>
        /// Creates a stream with the provided length with all 0 bytes
        /// </summary>
        public DynamicStream(int length)
            : this(length, 0) {
        }

        /// <summary>
        /// Creates a stream with the provided length defaulting to the byte specified
        /// </summary>
        public DynamicStream(int length, byte @default) {
            _Output = new List<byte>();
            for (var i = 0; i < length; i++) {
                _Output.Add(@default);
            }
        }

        #endregion

        #region Fields

        //contains the bytes that are being modified
        List<byte> _Output;

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current length of the stream
        /// </summary>
        public int Length {
            get { return _Output.Count; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Inserts a new byte at the specified index
        /// </summary>
        public void InsertAt(int index, byte[] bytes) {
            _Reset();
            _Output.InsertRange(index, bytes);
        }

        /// <summary>
        /// Inserts new bytes at the specified index
        /// </summary>
        public void InsertAt(int index, byte @byte) {
            _Reset();
            _Output.Insert(index, @byte);
        }

        /// <summary>
        /// Overwrites the byte at the specified index
        /// </summary>
        public void WriteAt(int index, byte @byte) {
            _Reset();
            _Output.RemoveAt(index);
            InsertAt(index, @byte);
        }

        /// <summary>
        /// Overwrites the bytes the the specified index
        /// </summary>
        public void WriteAt(int index, byte[] bytes) {
            _Reset();
            _Output.RemoveRange(index, bytes.Length);
            InsertAt(index, bytes);
        }

        /// <summary>
        /// Appends the byte to the end of the stream
        /// </summary>
        public void Append(byte @byte) {
            _Reset();
            _Output.Add(@byte);
        }

        /// <summary>
        /// Appends the bytes to the end of the stream
        /// </summary>
        public void Append(byte[] bytes) {
            _Reset();
            _Output.InsertRange(_Output.Count, bytes);
        }

        /// <summary>
        /// Reads the bytes within the specified area
        /// </summary>
        public byte[] Read(int start, int length) {
            return _Output.Skip(start).Take(length).ToArray();
        }

        /// <summary>
        /// Returns all of the bytes for the stream as an array
        /// </summary>
        public byte[] ToArray() {
            if (_generated == null) {
                _generated = _Output.ToArray();
            }
            return _generated;
        }
        private byte[] _generated;

        //clears the saved array of bytes, if any
        private void _Reset() {
            _generated = null;
        }

        #endregion

    }

}
