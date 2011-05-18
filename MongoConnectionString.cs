using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CSMongo.Exceptions;

namespace CSMongo {

    /// <summary>
    /// Reads connection strings for MongoDatabases
    /// </summary>
    internal sealed class MongoConnectionString {

        #region Constructors

        /// <summary>
        /// Creates a new empty connection string reader
        /// </summary>
        public MongoConnectionString() {
            Port = Mongo.DefaultPort;
            AutoConnect = true;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The server or IP address to connect to
        /// </summary>
        public string Host { get; private set; }

        /// <summary>
        /// The port number to connect on
        /// </summary>
        public int Port { get; private set; }

        /// <summary>
        /// The name of the database to open
        /// </summary>
        public string Database { get; private set; }

        /// <summary>
        /// The username to use when connecting
        /// </summary>
        public string Username { get; private set; }

        /// <summary>
        /// The password to use when connecting
        /// </summary>
        public string Password { get; private set; }

        /// <summary>
        /// Does this connection automatically open
        /// </summary>
        public bool AutoConnect { get; private set; }

        #endregion

        #region Static Creation

        /// <summary>
        /// Parses a string for Mongo connection data
        /// </summary>
        public static MongoConnectionString Parse(string connectionString) {
            var reader = new MongoConnectionString();
            reader.ParseString(connectionString);
            return reader;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Attempts to read the values for a connection string
        /// </summary>
        public void ParseString(string connectionString) {
            try {
                foreach (var pair in _ExtractPairs(connectionString)) {
                    _AssignValue(pair);
                }
            }
            catch {
                throw new InvalidMongoConnectionStringException();
            }
        }

        #endregion

        #region Private Methods

        //handles reading the actual value of the string
        private void _AssignValue(string item) {

            //extract the values
            var pair = Regex.Match(item ?? string.Empty, "^(?<key>[^=]+)=(?<value>.*)$");
            if (!pair.Success) { return; }

            //otherwise, get the values
            var key = pair.Groups["key"].Value.Trim();
            if (string.IsNullOrEmpty(key)) { return; }

            //get the value and undo escaping the ; values
            var value = pair.Groups["value"].Value.Replace(@"\;", ";");

            //depending on the value, set the property
            if (key.Equals("username", StringComparison.OrdinalIgnoreCase)) {
                Username = value.Trim();
            }
            else if (key.Equals("password", StringComparison.OrdinalIgnoreCase)) {
                Password = value;
            }
            else if (key.Equals("database", StringComparison.OrdinalIgnoreCase)) {
                Database = value.Trim();
            }
            else if (key.Equals("host", StringComparison.OrdinalIgnoreCase)) {
                Host = value.Trim();
            }
            else if (key.Equals("autoconnect")) {
                bool auto;
                bool.TryParse(value, out auto);
                AutoConnect = auto;
            }
            else if (key.Equals("port")) {
                int port;
                int.TryParse(value, out port);
                Port = port;
            }

        }

        //extracts all of the pairs of values (but doesn't format them)
        private static IEnumerable<string> _ExtractPairs(string connectionString) {

            //start looping each value in the string
            var escape = false;
            var set = string.Empty;
            foreach (var letter in connectionString) {

                //check if this is the end of ths string
                if (letter.Equals(';') && !escape) {

                    //return back the current value
                    yield return set;

                    //reset the values and continue parsing
                    set = string.Empty;
                    escape = false; //todo: dest is same
                    continue;
                }

                //reset escaping and check for it again
                escape = letter.Equals('\\') && !escape;

                //update the current string
                set = string.Concat(set, letter);
            }

            //if there is remaining characters, return them now
            if (set.Length > 0) {
                yield return set;
            }

        }

        #endregion

    }

}
