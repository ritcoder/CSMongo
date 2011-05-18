namespace CSMongo.Query
{

    /// <summary>
    /// Base type to use for custom Query providers
    /// </summary>
    public abstract class MongoQueryBase
    {

        #region Constructors

        /// <summary>
        /// Creates a new query for the database
        /// </summary>
        protected MongoQueryBase(MongoDatabase database, string collection)
            : this(new MongoCollection(database, collection))
        {
        }

        /// <summary>
        /// Creates a new query for the database
        /// </summary>
        protected MongoQueryBase(MongoCollection collection)
        {
            Collection = collection;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The collection that is being queried
        /// </summary>
        public MongoCollection Collection { get; private set; }

        #endregion

    }

}
