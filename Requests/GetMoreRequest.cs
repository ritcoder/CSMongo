using CSMongo.IO;
using CSMongo.Types;
using CSMongo.Bson;
using CSMongo.Responses;
using System.IO;

namespace CSMongo.Requests {

    public class GetMoreRequest : CollectionRequestBase {

        #region Constructors

        /// <summary>
        /// Creates a new GetMoreRequest with the provided MongoCursor
        /// </summary>
        public GetMoreRequest(MongoCollection collection, MongoCursor cursor, int count)
            : base(OpCodeTypes.GetMore, collection) {
            Cursor = cursor;
            Count = count;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The cursor to find for the query
        /// </summary>
        public MongoCursor Cursor { get; private set; }

        /// <summary>
        /// The total records to return
        /// </summary>
        public int Count { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Generates the body for this request
        /// </summary>
        protected override void GenerateBody(DynamicStream stream) {

            //required ZERO byte
            stream.Append(BsonTranslator.AsInt32(0));

            //the name of the collection
            stream.Append(BsonTranslator.AsString(Cursor.Query.GetDatabaseTarget()));

            //the total records to select
            stream.Append(BsonTranslator.AsInt32(Count));

            //required ZERO byte
            stream.Append(BsonTranslator.AsInt64(Cursor.Cursor));

        }

        /// <summary>
        /// Reads the response stream for results
        /// </summary>
        public override ResponseBase OnResponse(Stream stream) {
            return new QueryResponse(stream);
        }

        #endregion

    }

}
