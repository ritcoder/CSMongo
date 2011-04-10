using System.Collections.Generic;
using System.Linq;
using System.IO;
using CSMongo.Bson;
using CSMongo.Exceptions;

namespace CSMongo.Responses {

    /// <summary>
    /// Records and details of the result of a QueryRequest
    /// </summary>
    public class QueryResponse : ResponseBase {

        #region Constructors

        /// <summary>
        /// Creates a new QueryResponse using an incoming stream
        /// </summary>
        public QueryResponse(Stream response)
            : base(response) {
        }

        #endregion

        #region Properties

        /// <summary>
        /// The cursor returned from this response
        /// </summary>
        public long CursorId { get; private set; }

        /// <summary>
        /// Not sure what this does yet
        /// </summary>
        public int Flag { get; private set; }

        /// <summary>
        /// The starting point for the returned records
        /// </summary>
        public int StartingFrom { get; private set; }

        /// <summary>
        /// Total records returned to use
        /// </summary>
        public int TotalReturned { get; private set; }

        /// <summary>
        /// The documents found from the query
        /// </summary>
        public List<MongoDocument> Documents { get; private set; }

        /// <summary>
        /// Returns if there are documents to use after a response
        /// </summary>
        public bool HasDocuments {
            get { return Documents != null && Documents.Count > 0; }
        }

        #endregion

        #region Required Methods

        /// <summary>
        /// Handles reading back the content for this query
        /// </summary>
        protected override void ParseStream(Stream stream) {
            var reader = new BinaryReader(stream);

            /* Message Format (Excluding Header)
             * Flag (int32) - normally zero, non-zero on query failure
             * CursorId (int64) - id of the cursor created for this query response
             * StartingFrom (int32) - indicates where in this content is starting from
             * TotalReturned (int32) - number of documents in the reply
             * Documents (BSON array) - The documents (parsed separately) 
             */

            //get the flag first
            Flag = reader.ReadInt32();

            //second is the cursor value
            CursorId = reader.ReadInt64();

            //and the skipped and returned counts
            StartingFrom = reader.ReadInt32();
            TotalReturned = reader.ReadInt32();

            //next, read and parse the records
            Documents = new List<MongoDocument>();
            for (var i = 0; i < TotalReturned; i++) {

                //convert to a MongoDocument
                var parsed = BsonDocument.FromStream(stream);
                var document = new MongoDocument();
                document.Merge(parsed);
                //when the parsed doc has a field _id which is not MongOid, it ends up being replaced
                //as a fix, rename it as id
                if (parsed.Has(Mongo.DocumentIdKey) && parsed[Mongo.DocumentIdKey].GetType() != typeof(MongoOid))
                {
                    document["id"] = parsed[Mongo.DocumentIdKey];
                }

                //and add it to the list
                Documents.Add(document);
            }

            //check for server exceptions
            CheckForExceptions();

        }

        #endregion

        #region Checking For Errors

        /// <summary>
        /// Checks for a default response if there is an error
        /// </summary>
        public void CheckForExceptions() {
            var result = GetDefaultResponse();
            if (result.Has("errmsg")) {
                throw new MongoServerException(result.Get("errmsg", "Unknown database error!"));
            }
        }

        /// <summary>
        /// Finds a response or returns an empty object
        /// </summary>
        public BsonObject GetDefaultResponse() {
            return HasDocuments ? Documents.First() : new MongoDocument();
        }

        #endregion

    }

}
