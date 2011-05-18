using System;
using System.Net.Sockets;
using System.IO;
using CSMongo.Requests;
using CSMongo.Exceptions;
using CSMongo.Responses;

namespace CSMongo {

    /// <summary>
    /// Creates a new connection to a Mongo database
    /// </summary>
    public class MongoConnection : IDisposable {

        #region Constructors

        /// <summary>
        /// Creates a new Mongo connection
        /// </summary>
        public MongoConnection(string host)
            : this (host, Mongo.DefaultPort, true) {
        }

        /// <summary>
        /// Creates a new Mongo connection
        /// </summary>
        public MongoConnection(string host, bool autoConnect)
            : this(host, Mongo.DefaultPort, autoConnect) {
        }
        
        /// <summary>
        /// Creates a new Mongo connection
        /// </summary>
        public MongoConnection(string host, int port)
            : this(host, port, true) {
        }

        /// <summary>
        /// Creates a new Mongo connection
        /// </summary>
        public MongoConnection(string host, int port, bool autoConnect) {
            Host = (host ?? string.Empty).Trim();
            Port = port;
            AutoConnect = autoConnect;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the port to connect on
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the host to connect to
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Gets or sets if this connection should automatically open
        /// </summary>
        public bool AutoConnect { get; set; }

        /// <summary>
        /// Returns of this connection is currently open or not
        /// </summary>
        public bool Connected {
            get { return _client != null && _client.Connected; }
        }

        //the current connection to the host
        private TcpClient _client;
        private BufferedStream _buffer;
        //private NetworkStream _buffer; //remove this
        private BinaryWriter _writer;

        #endregion

        #region Events

        /// <summary>
        /// Event raised just before the database is closed
        /// </summary>
        public event Action<MongoConnection> BeforeConnectionOpened = connection => { };

        /// <summary>
        /// Event raised when right after the database is closed
        /// </summary>
        public event Action<MongoConnection> AfterConnectionOpen = connection => { };

        /// <summary>
        /// Event raised just before the database is closed
        /// </summary>
        public event Action<MongoConnection> BeforeConnectionClosed = connection => { };

        /// <summary>
        /// Event raised when right after the database is closed
        /// </summary>
        public event Action<MongoConnection> AfterConnectionClosed = connection => { };

        #endregion

        #region Methods

        /// <summary>
        /// Opens the connection to the database
        /// </summary>
        public void Open() {
            if (Connected) { return; }

            //notify any event handlers this is opening
            if (BeforeConnectionOpened != null) { BeforeConnectionOpened(this); }

            //and then try and open the connection
            _client = new TcpClient();
            _client.Connect(Host, Port);
            _buffer = new BufferedStream(_client.GetStream());
            //_buffer = _client.GetStream(); //remove this
            _writer = new BinaryWriter(_buffer);

            //notify this has been connected
            if (AfterConnectionOpen != null) { AfterConnectionOpen(this); }

        }

        /// <summary>
        /// Handles disconnecting from the client
        /// </summary>
        public void Close() {

            //notify any event handlers
            if (BeforeConnectionClosed != null) { BeforeConnectionClosed(this); }

            //close up all of the streams
            if (_buffer != null) { _buffer.Dispose(); }
            if (_writer != null) { _writer.Close(); }
            if (_client != null) { _client.Close(); }

            //and then finally any event handling
            if (AfterConnectionClosed != null) { AfterConnectionClosed(this); }
        }

        /// <summary>
        /// Sends a request to the server
        /// </summary>
        public ResponseBase SendRequest(RequestBase request) {

            //manage the connection state automatically if needed
            if (AutoConnect) { Open(); }

            //attempt to perform the request
            try {

                //perform normal checking
                if (!Connected) {
                    throw new ConnectionNotOpenedException("Connection isn't open yet!");
                }

                //send the header first
                _writer.Write(request.GetHeader());
                _writer.Flush();

                //then the rest of the content
                _writer.Write(request.GetBody());
                _writer.Flush();

                //next, read for the response
                return request.OnResponse(_buffer);

            }
            //forward the exception onto the caller
            catch (Exception up) {

                //attempt to kill the connection
                //ignore any problems since we are
                //already forwarding an exception
                try { Dispose(); }
                catch { }

                //and then forward the error for handling
                throw;
            }
            

        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Handles disconnecting and disposing a connection
        /// </summary>
        public virtual void Dispose() {
            Close();
        }

        #endregion

    }

}