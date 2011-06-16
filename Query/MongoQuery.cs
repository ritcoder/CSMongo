using System;
using System.Collections.Generic;
using System.Linq;
using CSMongo.Requests;
using CSMongo.Types;
using CSMongo.Responses;
using System.Text.RegularExpressions;
using CSMongo.Bson;
using System.Collections;
using CSMongo.Results;
using CSMongo.Commands;

namespace CSMongo.Query {

    /// <summary>
    /// Handles building a query and generating records
    /// </summary>
    public class MongoQuery : MongoQueryBase {

        #region Constructors

        /// <summary>
        /// Creates a new query for this database
        /// </summary>
        public MongoQuery(MongoCollection collection)
            : base (collection) {
            _parameters = new BsonDocument();
            _sortParameters = new BsonDocument();
        }

        /// <summary>
        /// Creates a new query for this database
        /// </summary>
        public MongoQuery(MongoDatabase database, string collection)
            : this(new MongoCollection(database, collection)) {
        }

        #endregion

        #region Properties

        //the class that actually queries the database
        private readonly BsonDocument _parameters;
        private readonly BsonDocument _sortParameters;

        /// <summary>
        /// Gets the query document. This document can be used for filtering.
        /// </summary>
        /// <value>The query document.</value>
        public BsonDocument QueryDocument
        {
            get
            {
                var doc = new BsonDocument();
                if (_sortParameters.FieldCount == 0)
                    doc.Merge(_parameters);
                else
                {
                    doc["query"] = _parameters;
                    doc["orderby"] = _sortParameters;
                }

                return doc;
            }
        }
        #endregion

        #region Query

        /// <summary>
        /// Allows you to append a query option using the Mongo syntax 
        /// </summary>
        public MongoQuery AppendParameter(string field, string modifier, object value) {

            //if using a modifier, set this as a document
            if (modifier != null) {
                var parameters = _parameters.Has(field) ? (BsonDocument) _parameters[field] : new BsonDocument();
                parameters[modifier] = value;
                _parameters[field] = parameters;
            }
            //otherwise, just assign the value
            else {
                _parameters[field] = value;
            }

            //return the query to use
            return this;

        }

        /// <summary>
        /// Writes a manual function to persom a comparison on the server
        /// </summary>
        public MongoQuery Where(string script) {
            return Where(script, false);
        }

        /// <summary>
        /// Writes a manual function to perform a comparison on the server 
        /// </summary>
        public MongoQuery Where(string script, bool wrapWithFunction) {

            //check if this needs to be wrapped or not
            if (wrapWithFunction) {
                script = string.Format("function() {{ {0} }}", script);
            }

            //write the query for the user
            _parameters["$where"] = script;

            //update the assignments
            return this;

        }

        /// <summary>
        /// Finds all records that match the provided expression
        /// </summary>
        public MongoQuery Match(string field, Regex expression) {
            return AppendParameter(field, null, expression);
        }

        /// <summary>
        /// Finds all records that match the provided expression
        /// </summary>
        public MongoQuery Match(string field, string expression) {
            return Match(field, new Regex(expression, RegexOptions.None));
        }

        /// <summary>
        /// Finds all records that match the provided expression
        /// </summary>
        public MongoQuery Match(string field, string expression, RegexOptions options) {
            return Match(field, new Regex(expression, options));
        }

        /// <summary>
        /// Finds all records that are equal to the value provided
        /// </summary>
        public MongoQuery EqualTo(string field, object value) {

            //just in case they tried to use EqualTo for finding an id
            if (field.Equals(Mongo.DocumentIdKey) && value is MongoOid) {
                return FindById(value as MongoOid);
            }

            //otherwise, just do the default step
            return AppendParameter(field, null, value);

        }

        /// <summary>
        /// Finds all records that have fields with values equal to that of the document provided
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        public MongoQuery EqualTo(BsonDocument doc)
        {
            foreach (var name in doc.GetFieldNames())
            {
                AppendParameter(name,null, doc.Get(name));
            }
            return this;
        }

        /// <summary>
        /// Finds all records that have fields with values equal to that off the docuemtn provided
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public MongoQuery EqualTo(object o)
        {
            return o == null ? this : EqualTo(new BsonDocument(o));
        }

        /// <summary>
        /// Finds a record based on the Oid value
        /// </summary>
        public MongoQuery FindById(string id) {
            return FindById(new MongoOid(id));
        }

        /// <summary>
        /// Finds a record based on the Oid value
        /// </summary>
        public MongoQuery FindById(byte[] id) {
            return FindById(new MongoOid(id));
        }

        /// <summary>
        /// Finds a record based on the Oid value
        /// </summary>
        public MongoQuery FindById(MongoOid id) {

            //use 'in' to find the id - There is an
            //actual option to use for this which
            //will be converted to later on
            return In(Mongo.DocumentIdKey, new[] { id });

        }

        /// <summary>
        /// Finds all records that are not equal to the value provided
        /// </summary>
        public MongoQuery NotEqualTo(string field, object value) {
            return AppendParameter(field, "$ne", value);
        }

        /// <summary>
        /// Finds all records greater than or equal to the provided value
        /// </summary>
        public MongoQuery GreaterOrEqual(string field, object value) {
            return AppendParameter(field, "$gte", value);
        }

        /// <summary>
        /// Finds all records greater than the provided value
        /// </summary>
        public MongoQuery Greater(string field, object value) {
            return AppendParameter(field, "$gt", value);
        }

        /// <summary>
        /// Finds all records less than or equal to the provided value
        /// </summary>
        public MongoQuery LessOrEqual(string field, object value) {
            return AppendParameter(field, "$lte", value);
        }

        /// <summary>
        /// Finds all records less than the provided value
        /// </summary>
        public MongoQuery Less(string field, object value) {
            return AppendParameter(field, "$lt", value);
        }

        /// <summary>
        /// Finds records that the requested field exists in
        /// </summary>
        public MongoQuery Exists(string field) {
            return AppendParameter(field, "$exists", true);
        }

        /// <summary>
        /// Finds records that the requested field does not exist in
        /// </summary>
        public MongoQuery NotExists(string field) {
            return AppendParameter(field, "$exists", false);
        }

        /// <summary>
        /// Finds fields that match any value within the record
        /// </summary>
        public MongoQuery In(string field, IEnumerable values) {
            return In(field, values.Cast<object>().ToArray());
        }

        /// <summary>
        /// Finds fields that match any value within the record
        /// </summary>
        public MongoQuery In(string field, params object[] values) {
            return AppendParameter(field, "$in", values);
        }

        /// <summary>
        /// Finds fields that haven't any matches within the collection
        /// </summary>
        public MongoQuery NotIn(string field, IEnumerable values) {
            return NotIn(field, values.Cast<object>().ToArray());
        }

        /// <summary>
        /// Finds fields that haven't any matches within the array
        /// </summary>
        public MongoQuery NotIn(string field, params object[] values) {
            return AppendParameter(field, "$nin", values);
        }

        /// <summary>
        /// Finds fields that match all value within the record
        /// </summary>
        public MongoQuery Size(string field, int size) {
            return AppendParameter(field, "$size", size);
        }

        /// <summary>
        /// Finds fields that match all value within the record
        /// </summary>
        public MongoQuery All(string field, IEnumerable values) {
            return All(field, values.Cast<object>().ToArray());
        }

        /// <summary>
        /// Finds fields that match all value within the record
        /// </summary>
        public MongoQuery All(string field, params object[] values) {
            return AppendParameter(field, "$all", values);
        }

        /// <summary>
        /// Performs a modulo comparison (field % value == 1)
        /// </summary>
        public MongoQuery Mod(string field, int value) {
            return Mod(field, value, 1);
        }

        /// <summary>
        /// Performs a modulo comparison (field % value == compare)
        /// </summary>
        public MongoQuery Mod(string field, int value, int compare) {
            return AppendParameter(field, "$mod", new[] { value, compare});
        }

        #endregion

        #region Selection

        /// <summary>
        /// Selects only one document with the provided parameters
        /// </summary>
        public MongoDocument SelectOne() {
            return SelectOne(Mongo.DefaultSkipCount, QueryOptionTypes.None);
        }

        /// <summary>
        /// Selects only one document with the provided parameters
        /// </summary>
        public MongoDocument SelectOne(int skip) {
            return SelectOne(skip, QueryOptionTypes.None);
        }

        /// <summary>
        /// Selects only one document with the provided parameters
        /// </summary>
        public MongoDocument SelectOne(QueryOptionTypes options) {
            return SelectOne(Mongo.DefaultSkipCount, QueryOptionTypes.None);
        }

        /// <summary>
        /// Selects only one document with the provided parameters
        /// </summary>
        public MongoDocument SelectOne(params string[] fields) {
            return SelectOne(Mongo.DefaultSkipCount, QueryOptionTypes.None, fields);
        }

        /// <summary>
        /// Selects only one document with the provided parameters
        /// </summary>
        public MongoDocument SelectOne(int skip, params string[] fields ) {
            return SelectOne(skip, QueryOptionTypes.None, fields);
        }

        /// <summary>
        /// Selects only one document with the provided parameters
        /// </summary>
        public MongoDocument SelectOne(int skip, QueryOptionTypes options, params string[] fields) {
            return Select(skip, 1, options, fields).FirstOrDefault();
        }

        /// <summary>
        /// Selects the records from the database that matches this query
        /// </summary>
        public IEnumerable<MongoDocument> Select() {
            return Select(Mongo.DefaultSkipCount, Mongo.DefaultTakeCount, QueryOptionTypes.None);
        }

        /// <summary>
        /// Selects the records from the database that matches this query
        /// </summary>
        public IEnumerable<MongoDocument> Select(int skip, int take) {
            return Select(skip, take, QueryOptionTypes.None);
        }

        /// <summary>
        /// Selects the records from the database that matches this query
        /// </summary>
        public IEnumerable<MongoDocument> Select(QueryOptionTypes options) {
            return Select(Mongo.DefaultSkipCount, Mongo.DefaultTakeCount, options);
        }

        /// <summary>
        /// Selects the records from the database that matches this query
        /// </summary>
        public IEnumerable<MongoDocument> Select(params string[] fields) {
            return Select(Mongo.DefaultSkipCount, Mongo.DefaultTakeCount, QueryOptionTypes.None, fields);
        }

        /// <summary>
        /// Selects the records from the database that matches this query
        /// </summary>
        public IEnumerable<MongoDocument> Select(int skip, int take, params string[] fields) {
            return Select(skip, take, QueryOptionTypes.None, fields);
        }

        /// <summary>
        /// Selects the records from the database that matches this query
        /// </summary>
        public IEnumerable<MongoDocument> Select(QueryOptionTypes options, params string[] fields) {
            return Select(Mongo.DefaultSkipCount, Mongo.DefaultTakeCount, options, fields);
        }

        /// <summary>
        /// Selects the records from the database that matches this query
        /// </summary>
        public IEnumerable<MongoDocument> Select(int skip, int take, QueryOptionTypes options, params string[] fields) {
            if (take <= 0) take = int.MaxValue; //if this is not set, only 101 docs (max) will be returned
            //create the request to use
            var request = new QueryRequest(Collection);
            if (fields!=null && fields.Length>0) request.Fields.AddRange(fields); 
            request.Skip = skip;
            request.Take = take;
            request.Options = options;
            request.Parameters = QueryDocument;

            //send the request and get the response
            var response = Collection.Database.Connection
                .SendRequest(request) as QueryResponse;


            //and return the records
            var documents = response.Documents.AsEnumerable().ToList();

            //get all other records using the cursor
            if (response.CursorId > 0)
            {

                //save this cursor for later
                var cursor = new MongoCursor(request, response.CursorId, response.TotalReturned);
                Collection.Database.RegisterCursor(cursor); //just keeping it. Don't think it will hurt much to just keep it.

                int max;
                while ((max = take - documents.Count) > 0)
                {
                    var docs = Collection.Database.GetMore(cursor, max);
                    if (docs.Count() == 0) break;
                    if (docs.Count() < max) documents.AddRange(docs);
                    else
                    {
                        documents.AddRange(docs.Take(max));
                        break;
                    }
                }
                //kill the cursor. is this really necessary? it appears it is killed automatically when the data is read to completion.
                MongoDatabaseCommands.KillCursors(Collection.Database, new[] {response.CursorId});
            }


            if (!Collection.InReadOnlyMode) Collection.UpdateOnSubmit(documents); //do not store this list if readonly

            return documents;

        }

        /// <summary>
        /// Determines the total count of records matching this query
        /// </summary>
        public long Count() {

            //request the count using the current parameters
            CollectionCountResult count = MongoDatabaseCommands.CollectionCount(
                Collection.Database, 
                Collection.Name, 
                _parameters
                );
            return count.TotalDocuments;

        }

        #endregion

        #region Updating

        #region PullAll
        /// <summary>
        /// removes all occurrences of each value in value_array from field, if field is an array. If field is present but is not an array, an error condition is raised.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="value"></param>
        public void PullAll(string field, object value)
        {
            var doc = new BsonDocument();
            doc[field] = value;
            PullAll(doc);
        }
        /// <summary>
        /// removes all occurrences of each value in value_array from field, if field is an array. If field is present but is not an array, an error condition is raised.
        /// </summary>
        /// <param name="document"></param>
        public void PullAll(object document)
        {
            PullAll(new BsonDocument(document));
        }
        /// <summary>
        /// removes all occurrences of each value in value_array from field, if field is an array. If field is present but is not an array, an error condition is raised.
        /// </summary>
        /// <param name="document"></param>
        public void PullAll(BsonDocument document)
        {
            _SendUpdate("$pullAll", UpdateOptionTypes.MultiUpdate, document);
        }

        #endregion

        #region Pull
        /// <summary>
        /// removes all occurrences of value from field, if field is an array. If field is present but is not an array, an error condition is raised.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="value"></param>
        public void Pull(string field, object value)
        {
            var doc = new BsonDocument();
            doc[field] = value;
            Pull(doc);
        }
        /// <summary>
        /// removes all occurrences of value from field, if field is an array. If field is present but is not an array, an error condition is raised.
        /// </summary>
        /// <param name="document"></param>
        public void Pull(object document)
        {
            Pull(new BsonDocument(document));
        }
        /// <summary>
        /// removes all occurrences of value from field, if field is an array. If field is present but is not an array, an error condition is raised.
        /// </summary>
        /// <param name="document"></param>
        public void Pull(BsonDocument document)
        {
            _SendUpdate("$pull", UpdateOptionTypes.MultiUpdate, document);
        }
        #endregion

        #region Rename
        /// <summary>
        /// Renames the specified field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="newName">The new name.</param>
        public void Rename(string field,string newName)
        {
            var doc = new BsonDocument();
            doc[field] = newName;
            Rename(doc);
        }
        /// <summary>
        ///  Renames the fields in specified document.
        /// </summary>
        /// <param name="document">The document.</param>
        public void Rename(object document)
        {
            Rename(new BsonDocument(document));
        }
        /// <summary>
        /// Renames the fields in specified document.
        /// </summary>
        /// <param name="document">The document.</param>
        public void Rename(BsonDocument document)
        {
            _SendUpdate("$rename",UpdateOptionTypes.MultiUpdate,document);
        }
        #endregion

        #region Add To Set
        /// <summary>
        /// Adds value to the array only if its not in the array already.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="value"></param>
        public void AddToSet(string field, object value)
        {
            var doc = new BsonDocument();
            doc[field] = value;
            AddToSet(doc);
        }
        /// <summary>
        /// Adds value to the array only if its not in the array already.
        /// </summary>
        /// <param name="document"></param>
        public void AddToSet(object document)
        {
            AddToSet(new BsonDocument(document));
        }
        /// <summary>
        /// Adds value to the array only if its not in the array already.
        /// </summary>
        /// <param name="document"></param>
        public void AddToSet(BsonDocument document)
        {
            _SendUpdate("$addToSet", UpdateOptionTypes.MultiUpdate, document);
        }
        #endregion

        #region Add All To Set
        /// <summary>
        /// Adds values to the array only if its not in the array already.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="values"></param>
        public void AddAllToSet(string field, IEnumerable values)
        {
            var innerDoc = new BsonDocument();
            innerDoc["$each"] = values;
            var doc = new BsonDocument();
            doc[field] = innerDoc;
            AddAllToSet(doc);
        }
        /// <summary>
        /// Adds values to the array only if its not in the array already.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="values"></param>
        public void AddAllToSet(string field, params object[] values)
        {
            var innerDoc = new BsonDocument();
            innerDoc["$each"] = values;
            var doc = new BsonDocument();
            doc[field] = innerDoc;
            AddAllToSet(doc);
        }
        /// <summary>
        /// Adds value to the array only if its not in the array already.
        /// </summary>
        /// <param name="document"></param>
        public void AddAllToSet(BsonDocument document)
        {
            _SendUpdate("$addToSet", UpdateOptionTypes.MultiUpdate, document);
        }
        #endregion

        #region Push
        /// <summary>
        /// appends value to field, if field  is an existing array, otherwise sets field to the array [value] if field is not present. If field is present but is not an array, an error condition is raised.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="value"></param>
        public void Push(string field, object value)
        {
            var doc = new BsonDocument();
            doc[field] = value;
            Push(doc);
        }
        /// <summary>
        /// appends value to field, if field  is an existing array, otherwise sets field to the array [value] if field is not present. If field is present but is not an array, an error condition is raised.
        /// </summary>
        /// <param name="document"></param>
        public void Push(object document)
        {
            Push(new BsonDocument(document));
        }
        /// <summary>
        /// appends value to field, if field  is an existing array, otherwise sets field to the array [value] if field is not present. If field is present but is not an array, an error condition is raised.
        /// </summary>
        /// <param name="document"></param>
        public void Push(BsonDocument document)
        {
            _SendUpdate("$push", UpdateOptionTypes.MultiUpdate, document);
        }

        #endregion

        #region PushAll
        /// <summary>
        /// appends each value in value_array to field, if field is an existing array, otherwise sets field  to the array value_array if field is not present. If field is present but is not an array, an error condition is raised.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="value"></param>
        public void PushAll(string field, object value)
        {
            var doc = new BsonDocument();
            doc[field] = value;
            PushAll(doc);
        }
        /// <summary>
        /// appends each value in value_array to field, if field is an existing array, otherwise sets field  to the array value_array if field is not present. If field is present but is not an array, an error condition is raised.
        /// </summary>
        /// <param name="document"></param>
        public void PushAll(object document)
        {
            PushAll(new BsonDocument(document));
        }
        /// <summary>
        /// appends each value in value_array to field, if field is an existing array, otherwise sets field  to the array value_array if field is not present. If field is present but is not an array, an error condition is raised.
        /// </summary>
        /// <param name="document"></param>
        public void PushAll(BsonDocument document)
        {
            _SendUpdate("$pushAll", UpdateOptionTypes.MultiUpdate, document);
        }
        #endregion

        #region Pop
        /// <summary>
        /// removes the last element in an array
        /// </summary>
        /// <param name="field"></param>
        public void Pop(string field)
        {
            Pop(field, false);
        }
        /// <summary>
        /// Removes all matching fields from each document in the query
        /// </summary>
        public void Pop(string field, bool first)
        {
            //mark the fields to be removed
            var remove = new BsonDocument();
            remove[field] = first ? -1 : 1;
            //send the command
            _SendUpdate("$pop", UpdateOptionTypes.MultiUpdate, remove);
        }
        #endregion

        #region Set
        /// <summary>
        /// Updates the matching record with with values provided or adds the new item to the object entirely
        /// </summary>
        /// <param name="field"></param>
        /// <param name="value"></param>
        public void Set(string field,object value)
        {
            var doc = new BsonDocument();
            doc[field] = value;
            Set(doc);
        }
        /// <summary>
        /// Updates all matching records with the values on the provided object
        /// or adds the new item to object entirely
        /// </summary>
        public void Set(object changes) {
            Set(new BsonDocument(changes));
        }

        /// <summary>
        /// Updates all matching records with the values on the provided BsonDocument
        /// or adds the new item to object entirely
        /// </summary>
        public void Set(BsonDocument document) {
            _SendUpdate("$set", UpdateOptionTypes.MultiUpdate, document);
        }
        #endregion

        /// <summary>
        /// Removes all matching fields from each document in the query
        /// </summary>
        public void Unset(params string[] fields) {

            //mark the fields to be removed
            var remove = new BsonDocument();
            foreach (var field in fields) {
                remove.Set(field, 1);
            }

            //send the command
            _SendUpdate("$unset", UpdateOptionTypes.MultiUpdate, remove);

        }

        /// <summary>
        /// Increments each of the fields provided by one
        /// </summary>
        public void Increment(params string[] fields) {
            
            //create the document
            var document = new BsonDocument{UseRawFieldNames = true};
            foreach (var field in fields) {
                document.Set(field, 1);
            }

            //send the command
            Increment(document);

        }

        /// <summary>
        /// Increments each of the fields by the number provides - first
        /// converting the value to an integer
        /// </summary>
        public void Increment(object parameters) {
            Increment(new BsonDocument(parameters));
        }

        /// <summary>
        /// Increments each of the fields by the number provides - first
        /// converting the value to an integer
        /// </summary>
        public void Increment(BsonDocument document) {

            //recast each to an integer value - I'm not sure
            //if any numeric type can be used in this instance
            foreach (var item in document.GetValues()) {
                document.Set(item.Key, document.Get(item.Key, 1));
            }

            //send the update request
            _SendUpdate("$inc", UpdateOptionTypes.MultiUpdate, document);

        }

        //prepares the actual update to send
        private void _SendUpdate(string type, UpdateOptionTypes options, BsonDocument changes) {

            //update the changes to actually make
            var document = new BsonDocument();
            document[type] = changes;

            //create the request to use
            var request = new UpdateRequest(Collection)
                              {
                                  Modifications = document,
                                  Parameters = _parameters,
                                  Options = options
                              };

            //make sure something is found to change
            if (request.Modifications.FieldCount == 0) { return; }

            //send the request and get the response
            Collection.Database.Connection.SendRequest(request);

        }

        #endregion

        #region Replacing

        #endregion

        #region Deletion

        /// <summary>
        /// Performs a selection of all fields matching the query
        /// </summary>
        public void Delete() {

            //create the request to use
            DeleteRequest request = new DeleteRequest(this.Collection);
            request.Parameters = this._parameters;

            //send the delete request
            this.Collection.Database.Connection.SendRequest(request);

        }

        #endregion

        #region Static
        /// <summary>
        /// Creates an instance of the mongo query with the collection set to null.
        /// This has been added to allow generating query documents that can be used in other areas such as findAndReplace and upserts.
        /// Only queries should be executed against this object. All updates, deletions, or selections will fail since there is not collection.
        /// </summary>
        /// <returns></returns>
        public static MongoQuery CreateQueryInstance()
        {
            return new MongoQuery(null);
        }
        #endregion

        #region Filtering
        /// <summary>
        /// Updates the parameters with those in the provided document
        /// </summary>
        /// <param name="doc">The doc.</param>
        /// <returns></returns>
        public MongoQuery SetFilter(BsonObject doc)
        {
            if (doc == null) return this;
            _parameters.Merge(doc);
            return this;
        }
        #endregion

        /// <summary>
        /// Sorts the query results by the field prrovided.
        /// </summary>
        /// <param name="field">The field to sort by.</param>
        /// <param name="ascending">if set to <c>true</c> sorting will be in [ascending] order.</param>
        /// <returns></returns>
        public MongoQuery SortBy(string field, bool ascending=true)
        {
            _sortParameters.Set(field, ascending ? 1 : -1);
            return this;
        }
        /// <summary>
        /// Sorts the collection using fields in the passsed in document
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        public MongoQuery SortBy(BsonDocument doc)
        {
            if (doc != null)
            {
                _sortParameters.Merge(doc);
            }
            return this;
        }

        /// <summary>
        /// Runs M/R on the given collection
        /// </summary>
        /// <param name="map">the map statements/ funcation</param>
        /// <param name="reduce">the reduce statements or functions</param>
        /// <param name="p">the parameters</param>
        /// <param name="limit">Limits the number of documents processed</param>
        /// <returns></returns>
        public List<BsonDocument> MapReduce(string map, string reduce, MapReduceParameters p=null,int limit=0)
        {
            if (p == null) p = new MapReduceParameters { keyFieldName = "key" };
            var doc = QueryDocument;
            if (doc.Has("query"))
            {
                p.query = doc["query"] as BsonDocument;
                p.sort = doc["orderby"] as BsonDocument;
            }
            else
            {
                p.query = doc;
            }
            if (limit > 0) p.limit = limit;
            return Collection.MapReduce(map, reduce, p);
        }
        /// <summary>
        /// The distinct command returns returns a list of distinct values for the given key across a collection
        /// If an error occurs, null is returned. the exact error is not stored anywhere.
        /// </summary>
        /// <typeparam name="T">The type of the objects in the array</typeparam>
        /// <param name="key">the key field to use</param>
        /// <param name="sort">true to sort the final list</param>
        /// <returns>The list of distinct values</returns>
        public List<T> Distinct<T>(string key,bool sort=false)
        {
            var doc = new BsonDocument();
            doc["distinct"] = Collection.Name;
            doc["query"] = _parameters;
            doc["key"] = key;
            var ret = Collection.Database.RunCommand(doc, true);
            if (ret.Ok && ret.HasResponse)
            {
                var outType = typeof(T);
                return (ret.Response["values"] as object[])
                        .Select(entry => (T)Convert.ChangeType(entry, outType))
                        .ToList();
            }
            return null;
        }
        /// <summary>
        /// Group returns an array of grouped items. The command is similar to SQL's group by
        /// </summary>
        /// <param name="key">The key parameter. If it is a string, it will be treated as a javascript function</param>
        /// <param name="initial"></param>
        /// <param name="reduce"></param>
        /// <param name="finalize"></param>
        /// <returns></returns>
        public List<BsonDocument> Group(object key,object initial,string reduce,string finalize=null)
        {
            var p = new BsonDocument{IgnoreNulls = true};
            p["group"] =new {ns = Collection.Name, cond = _parameters, initial};
            p["group.$reduce"] = reduce.Contains("function") ? reduce : string.Format("function(doc,out){{ {0} }}",reduce);
            if (key is string)
            {
                var keyStatements = (string)key;
                p["group.$keyf"] = keyStatements.Contains("function")
                                       ? keyStatements
                                       : string.Format("function(doc){{ {0} }}", keyStatements);
            }
            else
            {
                p["group.key"] = key;
            }
            if (!string.IsNullOrEmpty(finalize)) p["group.finalize"] = finalize;
            var ret = Collection.Database.RunCommand(p, true);
            return ret.Ok ? (ret.Response["retval"] as object[]).OfType<BsonDocument>().ToList() : null;
        }
        /// <summary>
        /// Computes the sum of multiple fields
        /// </summary>
        /// <param name="fields"></param>
        /// <returns></returns>
        public BsonDocument Sum(params string[] fields)
        {
            var reduceFn = string.Join(";", fields.Select(x => string.Format("out.{0}+=doc.{0}", x)).ToArray());
            var initial = new BsonDocument();
            foreach (var x in fields)
            {
                initial[x] = 0.0;
            }
            var key = "return {key:true}";
            var sumImpDocs = Group(key, initial, reduceFn);
            if (sumImpDocs != null && sumImpDocs.Count == 1)
            {
                return sumImpDocs[0];
            }
            return null;
        }
        /// <summary>
        /// Computes the sum of the given field
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public T? Sum<T>(string key) where T:struct 
        {
            var doc = Sum(key);
            if (doc != null) return doc.Get<T>(key);
            return null;
        }
    }

}
