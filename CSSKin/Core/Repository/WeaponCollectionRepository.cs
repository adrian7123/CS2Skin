using CSSKin.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace CSSKin.Core.Repository;

public class WeaponCollectionRepository : IRepository<Weapon>
{
    private readonly IMongoCollection<Weapon> mongoCollection;

    public WeaponCollectionRepository(string connectionString, string databaseName)
    {
        var client = new MongoClient(connectionString);
        client.StartSession();
        var database = client.GetDatabase(databaseName);

        mongoCollection = database.GetCollection<Weapon>("UserSkins");
    }


    public Weapon Create(Weapon data)
    {
        var index = mongoCollection.CountDocuments(new BsonDocument()) + 1;
        data.Id = index;
        mongoCollection.InsertOne(data);
        return data;
    }

    public void Delete(string uuid)
    {
        throw new NotImplementedException();
    }

    public List<Weapon> GetAll()
    {
        return mongoCollection.Find(data => true).ToList();
    }

    public List<Weapon> Get(string uuid)
    {
        return mongoCollection.Find(data => data.steamid == uuid).ToList(); ;
    }

    public void Update(Weapon data)
    {
        mongoCollection.ReplaceOne(Data => Data.Id == data.Id, data);
    }

    public void UpdateOne(Weapon data)
    {
        throw new NotImplementedException();
    }
}