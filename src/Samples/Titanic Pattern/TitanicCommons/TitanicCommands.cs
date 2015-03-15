namespace TitanicCommons
{
    public enum TitanicCommand { Ok = 200, Pending = 300, Unknown = 400, Failure = 501 }

    public enum TitanicOperation { Request, Reply, Close }
}
