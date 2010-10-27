using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CSMongo.Bson {

    //<summary>
    //Class designed to use an AnonymousType as a template
    //for a return value using a BsonObject for a data source
    //Really shouldn't be used for anything else since it
    //only works with the BsonObjects 
    internal class BsonAnonymousTypeParser {

        #region Constructors

        /// <summary>
        /// Creates a new parser for the provided information
        /// </summary>
        public BsonAnonymousTypeParser(BsonObject data,string idField="_id") {
            this._Data = data;
            _idField = idField;
        }

        #endregion

        #region Fields

        //houses the information to use
        private readonly BsonObject _Data;
        private string _idField;
        #endregion

        #region Parsing

        //handles reading a node of anonymous type and
        //returns the values for the type - This section
        //is heavily commented to try and explain what it
        //doing 
        public object ReadType(string parent, object section) {

            //create a list of values for the constructor
            var values = new List<object>();

            //and also get this type so we know how to create a
            //new instance using the constructor
            var type = section.GetType();

            //start checking each of the properties
            foreach (PropertyInfo property in type.GetProperties()) {

                //get the path to the value in the document 
                //but only if this isn't the first set
                var path = string.IsNullOrEmpty(parent)
                    ? property.Name
                    : string.Concat(parent, ".", property.Name);

                //get the value to use
                var value = property.GetValue(section, null);

                //if this is anonymous type, parse it
                if (IsAnonymousType(value)) {
                    value = this.ReadType(path, value);
                }
                //try to extract the id value
                else if (path.Equals(_idField))
                {
                    value = ((MongoOid) _Data.Get("_id")).GetId();
                }
                //if it is not, check to see if there is an existing value
                else
                {

                    //try and get the value to use
                    value = _Data.Get(path, value);
                }

                //add this to the list of values
                values.Add(value);

            }

            //now that our object is created, try and create the instance
            //but if it fails just give up and return the template value
            try {
                return Activator.CreateInstance(type, values.ToArray());
            }
            catch {
                return section;
            }

        }

        #endregion

        #region Static Methods

        /// <summary>
        /// Attempts to distingusing anonymous types from
        /// other object types
        /// </summary>
        public static bool IsAnonymousType(object value) {

            //make sure this has a value
            if (value == null) { return false; }

            //check if this is anonymous type using the name and 
            //some values that hint it might be anonymous - This
            //could probably be better though if a better way of
            //checking for anonymous types becomes available
            Type type = value.GetType();
            return Regex.IsMatch(type.FullName, "^(<>f__AnonymousType|VB\\$AnonymousType)") &&
                type.IsSealed &&
                type.IsGenericType &&
                type.BaseType.Equals(typeof(object));

        }

        /// <summary>
        /// Attempts to assign the values of a BsonObject into new Anonymous Type values
        /// </summary>
        public static T PopulateAnonymousType<T>(BsonObject data, T template) {
            return BsonAnonymousTypeParser.PopulateAnonymousType<T>(data, string.Empty, template);
        }

        /// <summary>
        /// Attempts to assign the values of a BsonObject into new Anonymous Type values
        /// </summary>
        public static T PopulateAnonymousType<T>(BsonObject data, string parent, T template) {
            var parser = new BsonAnonymousTypeParser(data);
            return (T)parser.ReadType(parent, template);
        }

        /// <summary>
        /// Attempts to assign the values of a BsonObject into new Anonymous Type values. It attempted to extract the id as well
        /// </summary>
        public static T PopulateAnonymousTypeWithId<T>(BsonObject data, T template,string idField)
        {
            var parser = new BsonAnonymousTypeParser(data,idField);
            return (T)parser.ReadType(string.Empty, template);
        }
        #endregion

    }

}
