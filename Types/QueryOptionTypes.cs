namespace CSMongo.Types {

    /// <summary>
    /// Options available when performing a query
    /// </summary>
    public enum QueryOptionTypes
    {

        None = 0,
        
        TailableCursor = 2,

        SlaveOK = 4,

        NoCursorTimeout = 16,

        Exhaust = 64

    }

}
