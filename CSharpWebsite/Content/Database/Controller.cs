using MongoDB.Driver;
using System.Text.Json;

namespace CSharpWebsite.Content.Database
{
    public class Controller
    {
        public static IMongoDatabase database { get; set; }
        public static void Init()
        {
            var cfg = JsonSerializer.Deserialize<Config>(File.ReadAllText("appsettings.json"));
            MongoClient client = new MongoClient(cfg?.DatabaseData.connection);
            database = client.GetDatabase(cfg?.DatabaseData.name);
        }
    }
    public class Config
    {
        public object Logging { get; set; }
        public string AllowedHosts { get; set; }
        public DatabaseInfo DatabaseData { get; set; }
    }
    public class DatabaseInfo
    {
        public string name { get; set; }
        public string connection { get; set; }
    }
}
