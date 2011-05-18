using System;
using System.Collections.Generic;
using System.Linq;
using CSMongo.Exceptions;
using CSMongo.Requests;
using CSMongo.Commands;
using CSMongo.Results;
using CSMongo.Query;
using CSMongo.Bson;
using CSMongo.Types;

namespace CSMongo {

    /// <summary>
    /// A collection of documents within a Mongo database
    /// </summary>
    public class MongoCollection {

        #region Constructors

        /// <summary>
        /// Creates a new MongoCollection - It is better to create a
        /// collection using the MongoDatabase command GetCollection
        /// so the database can keep track of multiple instances of
        /// the same collection
        /// </summary>
        public MongoCollection(MongoDatabase database, string collection) {
            Database = database;
            Name = collection;

            //set the containers for updating
            _Inserts = new List<MongoDocument>();
            _Deletes = new List<MongoDocument>();
            _Updates = new List<KeyValuePair<string, MongoDocument>>();
            _Upserts = new List<KeyValuePair<string, MongoDocument>>();
        }

        #endregion

        #region Fields
        //update information
        internal List<MongoDocument> _Inserts;
        internal List<MongoDocument> _Deletes;

        //document with a hashcode to track changes
        internal List<KeyValuePair<string, MongoDocument>> _Updates;
        internal List<KeyValuePair<string, MongoDocument>> _Upserts;
        #endregion

        #region Properties

        /// <summary>
        /// The name of the collection to query
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The database connection information
        /// </summary>
        public MongoDatabase Database { get; private set; }

        /// <summary>
        /// Returns the connection used for this request
        /// </summary>
        public MongoConnection Connection {
            get { return Database.Connection; }
        }
        /// <summary>
        /// Gets or sets whether the collection is in read only mode.
        /// In read only mode, history of query documents are not kept. Also, inserts, updates and upserts will throw exceptions
        /// </summary>
        public bool InReadOnlyMode { get; set; }
        #endregion

        #region Helper query procs
        /// <summary>
        /// Gets the first document that matches the given filter
        /// </summary>
        /// <param name="filter">The filter.</param>
        /// <returns></returns>
        public MongoDocument First(IDictionary<string,string> filter=null)
        {
            var query = FindAll(filter);
            return query.SelectOne();
        }
        /// <summary>
        /// Gets the first object after applying the filter and converts it to the given object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="refObj">The ref obj.</param>
        /// <param name="filter">The filter.</param>
        /// <param name="idField">The id field.</param>
        /// <returns></returns>
        public T First<T>(T refObj,IDictionary<string,string> filter=null,string idField="id") where T:class
        {
            var first = First(filter);
            return first == null ? null : first.GetWithId(refObj,idField);
        }

        /// <summary>
        /// Gets the first item with the given id and converts it to an object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id">The id.</param>
        /// <param name="refObj">The ref obj.</param>
        /// <param name="idField">The id field.</param>
        /// <returns></returns>
        public T First<T>(string id,T refObj,string idField="id") where T:class
        {
            var instance = Find().FindById(id).SelectOne();
            return instance == null ? null : instance.GetWithId(refObj, idField);
        }

        #endregion

        #region Query Records
        /// <summary>
        /// Starts a new query for this collection and appends the added filter
        /// </summary>
        /// <param name="filter">The filter.</param>
        /// <returns></returns>
        public MongoQuery FindAll(IDictionary<string,string> filter)
        {
            var query = Find();
            if (filter != null)
            {
                foreach (var kvp in filter)
                {
                    query.EqualTo(kvp.Key, kvp.Value);
                }
            }
            return query;
        }

        /// <summary>
        /// Starts a new query for this collection
        /// </summary>
        public MongoQuery Find() {
            return Find<MongoQuery>();
        }

        /// <summary>
        /// Starts a new query for this collection using the provider requested
        /// </summary>
        public TQueryProvider Find<TQueryProvider>() where TQueryProvider : MongoQueryBase {
            return Activator.CreateInstance(typeof(TQueryProvider), this) as TQueryProvider;
        }

        /// <summary>
        /// Returns the current count of records for the database
        /// </summary>
        public long Count() {
            return Find().Count();
        }

        #endregion

        #region Updating Changes

        /// <summary>
        /// Adds a record to be inserted when changes are submitted
        /// </summary>
        public void InsertOnSubmit(object document) {
            ValidateState();
            if (document is MongoDocument)
                InsertOnSubmit((MongoDocument)document);
            else
                _Inserts.Add(new MongoDocument(document));
        }

        /// <summary>
        /// Adds a record to be inserted when changes are submitted
        /// If the document contains a field with name as _upsert, it uses <c>UpsertOnInsert</c>
        /// </summary>
        public void InsertOnSubmit(MongoDocument document) {
            ValidateState();
            _Inserts.Add(document);
        }

        /// <summary>
        /// Adds a set of records to be inserted when changes are submitted
        /// </summary>
        public void InsertOnSubmit(IEnumerable<MongoDocument> documents) {
            ValidateState();
            _Inserts.AddRange(documents);
        }

        /// <summary>
        /// Adds a record to be deleted when changes are submitted
        /// </summary>
        public void DeleteOnSubmit(MongoDocument document) {
            ValidateState();
            _Deletes.Add(document);
        }

        /// <summary>
        /// Adds a set of records to be deleted when changes are submitted
        /// </summary>
        public void DeleteOnSubmit(IEnumerable<MongoDocument> documents) {
            ValidateState();
            _Deletes.AddRange(documents);
        }

        /// <summary>
        /// Appends a document to monitor for changes and updates
        /// </summary>
        public void UpdateOnSubmit(MongoDocument document) {
            UpdateOnSubmit((new[] { document }).AsEnumerable());
        }

        /// <summary>
        /// Appends a document to monitor for changes and updates
        /// </summary>
        public void UpdateOnSubmit(IEnumerable<MongoDocument> documents) {
            ValidateState();
            //append each of the items to the updates
            foreach (var update in documents) {
                if (_Updates.Any(item => item.Value.Equals(update))) { return; }
                _Updates.Add(new KeyValuePair<string, MongoDocument>(update.GetObjectHash(), update));
            }
        }

        /// <summary>
        /// Inserts the document if not found else it is updated
        /// </summary>
        /// <param name="document">The document to upsert. It must have a field called _upsert</param>
        public void UpsertOnSubmit(MongoDocument document)
        {
            UpsertOnSubmit(new[]{document});
        }
        /// <summary>
        /// Inserts the documents if not found else it is updated
        /// </summary>
        /// <param name="documents">The documents to upsert</param>
        public void UpsertOnSubmit(IEnumerable<MongoDocument> documents)
        {
            ValidateState();
            foreach (var doc in documents)
            {
                var key = new BsonDocument();
                var upsertKey = doc[Mongo.UpsertKey];
                if (upsertKey is string)
                {
                    key[(string)upsertKey] = doc[(string)upsertKey];
                }
                else if (upsertKey is BsonDocument)
                    key.Merge((BsonDocument)upsertKey);
                else //an object so wrap it up
                    key.Merge(new BsonDocument(upsertKey));
                doc[Mongo.UpsertKey] = key;
                var hash = key.GetObjectHash();
                if (_Upserts.Any(item => item.Key.Equals(hash))) { return;}
                _Upserts.Add(new KeyValuePair<string, MongoDocument>(hash,doc));
            }
        }
        #endregion

        #region Submitting Changes

        /// <summary>
        /// Handles updating changes for the database
        /// </summary>
        public void SubmitChanges() {

            ValidateState();
            //check for changes
            if (_Inserts.Count > 0) { _PerformInserts(); }
            if (_Updates.Count > 0) { _PerformUpdates(); }
            if (_Deletes.Count > 0) { _PerformDeletes(); }
            if (_Upserts.Count > 0)  {  _PerformUpserts(); }

            //then clear the lists
            _Inserts = new List<MongoDocument>();
            _Updates = new List<KeyValuePair<string, MongoDocument>>();
            _Deletes = new List<MongoDocument>();
            _Upserts = new List<KeyValuePair<string, MongoDocument>>();
        }

        //handles inserting records waiting
        private void _PerformInserts() {
            var insert = new InsertRequest(this);
            insert.Documents.AddRange(_Inserts);
            Connection.SendRequest(insert);
        }

        //handles updating records that are changed
        private void _PerformUpdates() {
            //check for changed items and update them now
            foreach (var item in _Updates) {

                //if this hasn't changed then skip it
                if (item.Key.Equals(item.Value.GetObjectHash())) { continue; }

                var request = new UpdateRequest(this);
                //set the updates
                var update = new BsonDocument();
                update.Merge(item.Value);
                update.Remove(Mongo.DocumentIdKey);
                request.Modifications["$set"] = update;
                //check for anything removed
                var removeDoc = new BsonDocument();
                foreach (var field in item.Value.GetRemovedFields())
                {
                    removeDoc[field] = 1;
                }
                if (removeDoc.FieldCount > 0)
                {
                    request.Modifications["$unset"] = removeDoc;
                }
                request.Parameters += MongoQuery.CreateQueryInstance().FindById(item.Value.Id).QueryDocument; 
                Database.SendRequest(request);

            }
            
        }

        //handles updating records that are changed
        private void _PerformUpserts()
        {

            //check for changed items and update them now
            foreach (var item in _Upserts)
            {

                //create a bson document of the update to create
                var update = new BsonDocument();
                update.Merge(item.Value);

                var key = (BsonDocument)update[Mongo.UpsertKey]; 
                update.Remove(Mongo.DocumentIdKey);
                update.Remove(Mongo.UpsertKey);
                key.Remove(Mongo.DocumentIdKey);


                var request = new UpdateRequest(this) {Options = UpdateOptionTypes.Upsert};
                request.Modifications["$set"] = update;
                request.Parameters.Merge(key);
                Database.SendRequest(request);

            }

        }

        //handles deleting records that need to be removed
        private void _PerformDeletes() {
            var ids = _Deletes.Select(item => item.Id);
            Database.From(Name).In("_id", ids).Delete();
        }

        #endregion

        #region Administrative

        /// <summary>
        /// Removes a collection from the database
        /// </summary>
        public DropCollectionResult DropCollection() {
            return Database.DropCollection(Name);
        }

        /// <summary>
        /// Returns details about the status of this collection
        /// </summary>
        public CollectionStatsResult GetStatus() {
            return MongoDatabaseCommands.GetCollectionStats(Database, Name);
        }

        /// <summary>
        /// Removes all indexes from this collection
        /// </summary>
        public DeleteCollectionIndexResult DeleteIndex() {
            return this.Database.DeleteCollectionIndex(Name);
        }

        /// <summary>
        /// Removes the specified index from this collection
        /// </summary>
        public DeleteCollectionIndexResult DeleteIndex(string collection, string index) {
            return Database.DeleteCollectionIndex(collection, index);
        }

        #endregion

        #region Commands
        /// <summary>
        /// Runs M/R on the given collection
        /// </summary>
        /// <param name="map">the map statements/ funcation</param>
        /// <param name="reduce">the reduce statements or functions</param>
        /// <param name="p">the parameters</param>
        /// <returns></returns>
        public List<BsonDocument> MapReduce(string map,string reduce,MapReduceParameters p)
        {
            if (p == null) p = new MapReduceParameters{keyFieldName = "key"};
            if (!string.IsNullOrEmpty(map)) p.map = map;
            if (!string.IsNullOrEmpty(reduce)) p.reduce = reduce;
            if (string.IsNullOrEmpty(p.mapreduce)) p.mapreduce = Name;
            if (p.@out == null) p.@out = new MapReduceParameters.MapReduceResultDocument { replace = Name + DateTime.Now.Ticks };
            var docs = p.Run(Database);
            if (p.callOk) return docs;
            return null;
        }
        /// <summary>
        /// Gets the next id. This is used for auto incrementing values.
        /// This was inspired by the post @ http://shiflett.org/blog/2010/jul/auto-increment-with-mongodb
        /// </summary>
        /// <param name="increment">The incremental value.</param>
        /// <param name="idField">The id field.</param>
        /// <param name="idsTable">The name of the collection that holds the counts.</param>
        /// <returns></returns>
        public long GetNextId(long increment=1L,string idField="lastId",string idsTable = "TableIds")
        {
            var p = new FindAndModifyParameters
                        {
                            associatedTable = idsTable,
                            query = new BsonDocument(new {name = Name}),
                            returnUpdatedValues = true,
                            upsert = true,
                            update = new BsonDocument().AppendField("$inc", new BsonDocument(new {lastId = increment}))
                        };
            if (!p.Run(Database))
            {
                //todo: what do we do here?
                return -1;
            }
            return p.responseDoc.Get<long>(idField);
        }
        /// <summary>
        /// Upserts the document with the given key. This is done instantly. To queue until submission, use UpsertOnSubmit
        /// </summary>
        /// <param name="key"></param>
        /// <param name="doc"></param>
        public void Upsert(BsonDocument doc,BsonDocument key)
        {
            var update = new BsonDocument();
            update.Merge(doc);
            update.Remove(Mongo.DocumentIdKey);
            key.Remove(Mongo.DocumentIdKey);

            var request = new UpdateRequest(this) { Options = UpdateOptionTypes.Upsert };
            request.Modifications["$set"] = update;
            request.Parameters.Merge(key);
            Database.SendRequest(request);
        }
        /// <summary>
        /// Upserts the specified doc. The keys are the property names.
        /// </summary>
        /// <param name="doc">The document to update</param>
        /// <param name="keys">The keys.</param>
        public void Upsert(BsonDocument doc,params string[] keys)
        {
            if (keys.Length==0) throw new ArgumentException("No key specified");
            var key = new BsonDocument();
            foreach (var k in keys)
            {
                key[k] = doc[k];
            }
            Upsert(doc, key);
        }

        /// <summary>
        /// Updates all matching documents
        /// </summary>
        /// <param name="queryDoc"></param>
        /// <param name="setDoc"></param>
        /// <param name="unsetDoc"></param>
        public void Update(BsonDocument queryDoc,BsonDocument setDoc,BsonDocument unsetDoc)
        {
            var request = new UpdateRequest(this) {Options = UpdateOptionTypes.MultiUpdate};
            if (setDoc!=null) request.Modifications["$set"] = setDoc;
            if (unsetDoc != null) request.Modifications["$unset"] = unsetDoc;
            request.Parameters.Merge(queryDoc);
            Database.SendRequest(request);

        }
        #endregion

        #region Helpers
        /// <summary>
        /// Checks the state of the operation against the state of the collection. Write operations are not permitted when InReadOnlyMode is true
        /// </summary>
        /// <param name="write"></param>
        private void ValidateState(bool write=true)
        {
            if (write && InReadOnlyMode) throw new ReadonlyCollectionException();
        }
        #endregion
    }

}
