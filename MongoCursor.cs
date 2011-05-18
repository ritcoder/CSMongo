using CSMongo.Requests;

namespace CSMongo {
    
    /// <summary>
    /// Contains information about a cursor for Mongo
    /// </summary>
    public class MongoCursor {

        #region Constructors

        /// <summary>
        /// Creates a new MongoCursor
        /// </summary>
        public MongoCursor(QueryRequest query, long cursor, int count) {
            Cursor = cursor;
            Query = query;
            ReturnCount = count;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The actual cursor value for this 
        /// </summary>
        public long Cursor { get; private set; }

        /// <summary>
        /// The previous query request incase there is no
        /// cursor information available
        /// </summary>
        public QueryRequest Query { get; private set; }

        /// <summary>
        /// The total number of records that were returned previously
        /// </summary>
        public int ReturnCount { get; private set; }

        /// <summary>
        /// Make certain there is a real cursor to use
        /// </summary>
        public bool HasCursor {
            get { return Cursor > 0; }
        }

        #endregion

    }

}
