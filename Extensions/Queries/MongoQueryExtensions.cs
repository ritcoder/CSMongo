using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSMongo.Extensions.Queries {

    /// <summary>
    /// Extension methods for working with Mongo Queries
    /// </summary>
    public static class MongoQueryExtensions {

        /// <summary>
        /// Selects information from the document in a specific format
        /// </summary>
        public static IEnumerable<T> As<T>(this IEnumerable<MongoDocument> documents, T template)
        {
            return documents.Select(document => document.Get(template));
        }

        /// <summary>
        /// Selects information from the document in a specific format
        /// </summary>
        public static IEnumerable<T> AsWithId<T>(this IEnumerable<MongoDocument> documents, T template, string idField = "Id")
        {
            return documents.Select(document => document.GetWithId(template, idField));
        }
        /// <summary>
        /// Selects information from the document in a specific format
        /// </summary>
        public static IEnumerable<T> As<T>(this IEnumerable<MongoDocument> documents, string start, T template)
        {
            return documents.Select(document => document.Get(start, template));
        }

        /// <summary>
        /// Selects information from the document in a specific format
        /// </summary>
        public static IEnumerable<MongoDocument> Apply(this IEnumerable<MongoDocument> documents, object parameters) {
            foreach(var document in documents) {
                document.Merge(parameters);
                yield return document;
            }
        }

    }

}
