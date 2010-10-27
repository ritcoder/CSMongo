using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSMongo.Bson;

namespace CSMongo.Types
{
    public class FindAndModifyParameters
    {
        public BsonDocument query { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the updates values are returned. Defaults to false
        /// </summary>
        /// <value><c>true</c> if update  values must be returned; otherwise, <c>false</c>.</value>
        public bool returnUpdatedValues { get; set; }
        public bool upsert { get; set; }
        public string associatedTable { get; set; }
        public BsonDocument update { get; set; }

        public bool callOk { get; private set; }
        public BsonDocument responseDoc { get; private set; }

        public FindAndModifyParameters()
        {
            query = new BsonDocument();
            update = new BsonDocument();
        }

        private BsonDocument Build()
        {
            var findAndModifyDoc = new BsonDocument();
            findAndModifyDoc["findandmodify"] = associatedTable;
            findAndModifyDoc["query"] = query;
            findAndModifyDoc["new"] = returnUpdatedValues;
            findAndModifyDoc["upsert"] = true;
            findAndModifyDoc["update"] = update;
            return findAndModifyDoc;
        }
        
        public bool Run(MongoDatabase db)
        {
            var findAndModifyDoc = Build();
            var x = db.RunCommand(findAndModifyDoc, true);
            if (x.Ok && x.HasResponse)
            {
                callOk = true;
                responseDoc = (BsonDocument) x.Response["value"];
                return true;
            }
            return false;
        }
    }
}
