﻿namespace CSMongo.Types {
    
    public enum OpCodeTypes
    {

        Reply = 1,

        Message = 1000,

        Update = 2001,

        Insert = 2002,

        /// <summary>
        /// (Apparently not used)
        /// </summary>
        Reserved = 2003,

        Query = 2004,

        GetMore = 2005,

        Delete = 2006,

        KillCursors = 2007

    }

}
