using System;
using System.IO;
using CSMongo.Types;
using CSMongo.IO;
using CSMongo.Responses;

namespace CSMongo.Requests {
    
    /// <summary>
    /// Base class for making requests to the server
    /// </summary>
    public abstract class RequestBase {

        #region Constants

        private const int DefaultHeaderLength = 16;
        private const int PositionRequestLength = 0;
        private const int PositionRequestId = 4;
        private const int PositionResponseId = 8;
        private const int PositionOpCode = 12;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new request for the specified type
        /// </summary>
        protected RequestBase(OpCodeTypes code) {
            OpCode = code;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current length of this request
        /// </summary>
        public int RequestLength {
            get { return _output.Length; }
        }

        /// <summary>
        /// Returns the OpCode used for this request
        /// </summary>
        public OpCodeTypes OpCode { get; private set; }

        /// <summary>
        /// The current RequestId for this 
        /// </summary>
        public int RequestId { get; set; }

        /// <summary>
        /// The curretn ResponseID for this request
        /// </summary>
        public int ResponseId { get; set; }

        /// <summary>
        /// the container for the stream to write
        /// </summary>
        private DynamicStream _output;

        #endregion

        #region Methods

        //generates the entire request
        private void GenerateStream() {

            //if the stream has already been created then don't bother
            if (_output != null) { return; }

            //called just before the generation starts
            OnBeforeGenerateStream();

            //start building the header
            var stream = new DynamicStream(DefaultHeaderLength);
            stream.WriteAt(PositionOpCode, BitConverter.GetBytes((int)OpCode));

            //generate the bytes to use for the body
            GenerateBody(stream);

            //update the request/response IDs incase they change when building
            stream.WriteAt(PositionRequestId, BitConverter.GetBytes(RequestId));
            stream.WriteAt(PositionResponseId, BitConverter.GetBytes(ResponseId));

            //finally, remember to update the length
            stream.WriteAt(PositionRequestLength, BitConverter.GetBytes(stream.Length));

            //cache this value to use it later
            _output = stream;

        }

        /// <summary>
        /// Resets the bytes for this request
        /// </summary>
        public void Reset() {
            _output = null;
        }

        /// <summary>
        /// Returns the bytes to send as a header for this request
        /// </summary>
        public byte[] GetHeader() {
            GenerateStream();
            return _output.Read(0, DefaultHeaderLength);
        }

        /// <summary>
        /// Returns the bytes that should be sent as a header
        /// </summary>
        public byte[] GetBody() {
            GenerateStream();
            return _output.Read(DefaultHeaderLength, _output.Length - DefaultHeaderLength);
        }

        #endregion

        #region Required Methods

        /// <summary>
        /// Required function to generate the content for sending
        /// </summary>
        protected abstract void GenerateBody(DynamicStream stream);

        #endregion

        #region Optional Methods

        /// <summary>
        /// optional method to read a return stream from the 
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public virtual ResponseBase OnResponse(Stream stream) {
            return null;
        }

        /// <summary>
        /// Optional functionality to perform before generating 
        /// the stream content
        /// </summary>
        protected virtual void OnBeforeGenerateStream() { }

        #endregion

    }

}
