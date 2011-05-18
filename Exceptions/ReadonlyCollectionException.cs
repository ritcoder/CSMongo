using System;

namespace CSMongo.Exceptions {

    /// <summary>
    /// Thrown when a non query operation is performed on a read only collection
    /// </summary>
    public class ReadonlyCollectionException : Exception {

        /// <summary>
        /// Thrown when a non query operation is performed on a read only collection
        /// </summary>
        public ReadonlyCollectionException()
            : base("Only queries are permitted on read only collections") {
        }

    }

}
