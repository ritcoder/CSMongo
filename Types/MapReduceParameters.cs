using System.Collections.Generic;
using System.Linq;
using CSMongo.Bson;

namespace CSMongo.Types
{
    /// <summary>
    /// Parameters for executing map/reduce functions
    /// </summary>
    public class MapReduceParameters
    {
        #region Inner classes
        /// <summary>
        /// Represents the out parameter of the request
        /// </summary>
        public class MapReduceResultDocument
        {
            /// <summary>
            /// With this option, no collection will be created, and the whole map-reduce operation will happen in RAM. 
            /// Also, the results of the map-reduce will be returned within the result object. 
            /// This option is possible only when the result set fits within the 16MB limit of a single document.
            /// </summary>
            public int? inline { get; set; }
            /// <summary>
            /// the output will replace any existing output collection with the same name
            /// </summary>
            public string replace { get; set; }
            /// <summary>
            /// If documents exists for a given key in the result set and in the old collection, 
            /// then a reduce operation (using the specified reduce function) will be performed on the two values and the result will 
            /// be written to the output collection. If a finalize function was provided, this will be run after the reduce as well
            /// </summary>
            public string reduce { get; set; }
            /// <summary>
            /// The name of the db to use if not the default
            /// </summary>
            public string db { get; set; }
            /// <summary>
            /// This option will merge new data into the old output collection. 
            /// In other words, if the same key exists in both the result set and the old collection, the new key will overwrite the old one
            /// </summary>
            public string merge { get; set; }
            //todo: add the additional fields
        }
        #endregion
       #region Fields
        /// <summary>
        /// Name of the collection to reduce
        /// </summary>
        public string mapreduce { get; set;}
        /// <summary>
        /// True to treat all functions as statements and thus to wrap the function around it.
        /// </summary>
        public bool wrapFunctions { get; set; }
        /// <summary>
        /// The map function references the variable this to inspect the current object under consideration. 
        /// A map function calls emit(key,value) any number of times to feed data to the reducer. 
        /// In most cases you will emit once per input document, but in some cases such as counting tags, a given document may have one, many, or even zero tags. 
        /// Each emit is limited to 50% of the maximum document size (e.g. 4MB for 1.6.x and 8MB for 1.8.x)
        /// () -> ()
        /// </summary>
        public string map { get; set; }
        /// <summary>
        /// the reduce function will receive an array of emitted values and reduce them to a single value
        /// (k,vs) -> v
        /// </summary>
        public string reduce { get; set; }
        /// <summary>
        /// Name to use as the key. This is used to update the final document
        /// </summary>
        public string keyFieldName { get; set; }
        /// <summary>
        /// function to apply to all the results when finished
        /// (k,v) -> v
        /// </summary>
        public string finalize { get; set; }
        /// <summary>
        /// query filter object
        /// </summary>
        public BsonDocument query { get; set; }
        /// <summary>
        /// sort the query.  useful for optimization
        /// </summary>
        public BsonDocument sort { get; set; }
        /// <summary>
        /// number of objects to return from collection
        /// </summary>
        public int? limit { get; set; }
        /// <summary>
        /// The out parameter
        /// </summary>
        public MapReduceResultDocument @out { get; set; }

        internal bool callOk { get; set; }
        #endregion

        #region .ctor
        /// <summary>
        /// Sets <c>wrapFunctions</c> to true and keyFieldName to key
        /// </summary>
        public MapReduceParameters()
        {
            wrapFunctions = true;
            keyFieldName = "key";
        }
        #endregion

        /// <summary>
        /// Creates a document for the command execution
        /// </summary>
        /// <returns></returns>
        public BsonDocument Build()
        {
            var doc = new BsonDocument{IgnoreNulls = true};
            var output = @out;
            @out = null;
            doc += this;
            if (wrapFunctions)
            {
                doc["map"] = string.Format("function(){{ {0}; }}", map);
                doc["reduce"] = string.Format("function(k,vs){{ {0}; return ret; }}", reduce);
                if (!string.IsNullOrEmpty(finalize))
                    doc["finalize"] = string.Format("function(k,v){{ {0}; return v; }}", finalize);
            }
            var outDoc =  new BsonDocument {IgnoreNulls = true};
            outDoc += output;
            doc["out"] = outDoc;
            @out = output;
            return doc;
        }

        /// <summary>
        /// Executes the command and returned the output documents
        /// </summary>
        /// <param name="db">The db to execute the command on</param>
        /// <returns></returns>
        public List<BsonDocument> Run(MongoDatabase db)
        {
            var ret = db.RunCommand(Build(), true);
            if (ret.Ok && ret.HasResponse)
            {
                callOk = true;
                if (ret.Response.Has("results"))
                {
                    var docs = (ret.Response["results"] as object[])
                                    .OfType<BsonDocument>()
                                    .ToList();
                    if (!string.IsNullOrEmpty(keyFieldName))
                    {
                        var key = "value." + keyFieldName;
                        foreach (var doc in docs)
                        {
                            doc[key] = doc["id"] = doc["_id"]; //id value is set to conform with the doc if read via select
                        }
                        return docs.Select(x => x["value"]).OfType<BsonDocument>().ToList();
                    }
                    //if no key is specified, return the entire document. not just the value
                    return docs;
                }
                else
                {
                    var col = (string)ret.Response["result"];
                    var docs = db.GetCollection(col).Find().Select().OfType<BsonDocument>().ToList();
                    db.DropCollection(col);
                    if (!string.IsNullOrEmpty(keyFieldName))
                    {
                        var key = "value." + keyFieldName;
                        foreach (var doc in docs)
                        {
                            doc[key] = doc["id"];
                        }
                        return docs.Select(x=>x["value"]).OfType<BsonDocument>().ToList();
                    }
                    //if no key is specified, return the entire document, not just the value
                    return docs;
                }
            }
            callOk = false;
            //todo: log the error
            return null;
        }
    }
}
