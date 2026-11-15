using MySqlConnector;

namespace MouseProfilesCloud.Data;

public class Db
{
    private readonly string _connStr;

    public Db(IConfiguration config) =>
        _connStr = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

    public MySqlConnection CreateConnection() => new(_connStr);
}
