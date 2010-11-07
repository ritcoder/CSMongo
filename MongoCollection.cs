using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using CSMongo.Requests;
using CSMongo.Responses;
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
            this.Database = database;
            this.Name = collection;

            //set the containers for updating
            this._Inserts = new List<MongoDocument>();
            this._Deletes = new List<MongoDocument>();
            this._Updates = new List<KeyValuePair<string, MongoDocument>>();
            _Upserts = new List<KeyValuePair<string, MongoDocument>>();
        }

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
            get { return this.Database.Connection; }
        }

        //update information
        internal List<MongoDocument> _Inserts;
        internal List<MongoDocument> _Deletes;

        //document with a hashcode to track changes
        internal List<KeyValuePair<string, MongoDocument>> _Updates;
        internal List<KeyValuePair<string, MongoDocument>> _Upserts;

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
            return this.Find().Count();
        }

        #endregion

        #region Updating Changes

        /// <summary>
        /// Adds a record to be inserted when changes are submitted
        /// </summary>
        public void InsertOnSubmit(object document) {
            if (document is MongoDocument)
                InsertOnSubmit((MongoDocument)document);
            else
                this._Inserts.Add(new MongoDocument(document));
        }

        /// <summary>
        /// Adds a record to be inserted when changes are submitted
        /// </summary>
        public void InsertOnSubmit(MongoDocument document) {
            this._Inserts.Add(document);
        }

        /// <summary>
        /// Adds a set of records to be inserted when changes are submitted
        /// </summary>
        public void InsertOnSubmit(IEnumerable<MongoDocument> documents) {
            this._Inserts.AddRange(documents);
        }

        /// <summary>
        /// Adds a record to be deleted when changes are submitted
        /// </summary>
        public void DeleteOnSubmit(MongoDocument document) {
            this._Deletes.Add(document);
        }

        /// <summary>
        /// Adds a set of records to be deleted when changes are submitted
        /// </summary>
        public void DeleteOnSubmit(IEnumerable<MongoDocument> documents) {
            this._Deletes.AddRange(documents);
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
            foreach (var doc in documents)
            {
                var key = new BsonDocument();
                var upsertKey = doc[Mongo.UpsertKey];
                if (upsertKey is string)
                {
                    key[(string)upsertKey] = doc[(string)upsertKey];
                }
                else
                    key.Merge((BsonDocument)upsertKey);
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
            InsertRequest insert = new InsertRequest(this);
            insert.Documents.AddRange(this._Inserts);
            this.Connection.SendRequest(insert);
        }

        //handles updating records that are changed
        private void _PerformUpdates() {
            //check for changed items and update them now
            foreach (KeyValuePair<string, MongoDocument> item in _Updates) {

                //if this hasn't changed then skip it
                if (item.Key.Equals(item.Value.GetObjectHash())) { continue; }

                /*//create a bson document of the update to create
                var update = new BsonDocument();
                update.Merge(item.Value);
                update.Remove(Mongo.DocumentIdKey);

                //start with the update
                this.Find().FindById(item.Value.Id).Set(update);

                //check for anything removed
                var removed = item.Value.GetRemovedFields();
                if (removed.Count() > 0) {
                    Find().FindById(item.Value.Id).Unset(removed.ToArray());
                }*/

                //Might want to try and merge this into the same
                //request to avoid two trips to the database -- But
                //this might cause an issue with older versions of
                //the same database since an unset call would cause
                //the inital set request to fail...

                var request = new UpdateRequest(this);
                request.Modifications["$set"] = item.Value;
                request.Modifications["$unset"] = item.Value.GetRemovedFields();
                request.Parameters["$in"] = new[] { item.Value.Id };
                Database.SendRequest(request);

            }
            
        }

        //handles updating records that are changed
        private void _PerformUpserts()
        {

            //check for changed items and update them now
            foreach (var item in this._Upserts)
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
            IEnumerable<MongoOid> ids = this._Deletes.Select(item => item.Id);
            this.Database.From(this.Name).In("_id", ids).Delete();
        }

        #endregion

        #region Administrative

        /// <summary>
        /// Removes a collection from the database
        /// </summary>
        public DropCollectionResult DropCollection() {
            return this.Database.DropCollection(this.Name);
        }

        /// <summary>
        /// Returns details about the status of this collection
        /// </summary>
        public CollectionStatsResult GetStatus() {
            return MongoDatabaseCommands.GetCollectionStats(this.Database, this.Name);
        }

        /// <summary>
        /// Removes all indexes from this collection
        /// </summary>
        public DeleteCollectionIndexResult DeleteIndex() {
            return this.Database.DeleteCollectionIndex(this.Name);
        }

        /// <summary>
        /// Removes the specified index from this collection
        /// </summary>
        public DeleteCollectionIndexResult DeleteIndex(string collection, string index) {
            return this.Database.DeleteCollectionIndex(collection, index);
        }

        #endregion

        #region Commands
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
            //request.Parameters["$in"] = new[] { item.Value.Id };
            Database.SendRequest(request);
            //Find().Set();
            ////check for changed items and update them now
            //foreach (KeyValuePair<string, MongoDocument> item in _Updates)
            //{

            //    //if this hasn't changed then skip it
            //    if (item.Key.Equals(item.Value.GetObjectHash())) { continue; }

            //    /*//create a bson document of the update to create
            //    var update = new BsonDocument();
            //    update.Merge(item.Value);
            //    update.Remove(Mongo.DocumentIdKey);

            //    //start with the update
            //    this.Find().FindById(item.Value.Id).Set(update);

            //    //check for anything removed
            //    var removed = item.Value.GetRemovedFields();
            //    if (removed.Count() > 0) {
            //        Find().FindById(item.Value.Id).Unset(removed.ToArray());
            //    }*/

            //    //Might want to try and merge this into the same
            //    //request to avoid two trips to the database -- But
            //    //this might cause an issue with older versions of
            //    //the same database since an unset call would cause
            //    //the inital set request to fail...

            //    var request = new UpdateRequest(this);
            //    request.Modifications["$set"] = item.Value;
            //    request.Modifications["$unset"] = item.Value.GetRemovedFields();
            //    request.Parameters["$in"] = new[] { item.Value.Id };
            //    Database.SendRequest(request);

            //}

        }
        #endregion
    }

}
