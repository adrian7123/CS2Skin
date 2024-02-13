using CSSKin.Models;
using Dapper;
using MySqlConnector;

namespace CSSKin.Core.Repository;

public class WeaponMysqlRepository : IRepository<Weapon>
{
    private MySqlConnection _connection;
    private string TableName;
    private string StickerTableName;

    public WeaponMysqlRepository(string connectionString, string tableName)
    {
        //      def paint Seed  Wear          Stickers          Unknown StartTrakEnable StarTrakValue NameTag
        // !gen 7   801   1     0.4   1353 0 1353 0 1353 0 1353 0 0 0       1           123           My Ak

        TableName = tableName;
        StickerTableName = $"{tableName}_stickers";

        _connection = new MySqlConnection(connectionString);
        _DumpTable();
        _DumpStickersTable();
    }

    private void _DumpStickersTable()
    {
        _connection.Open();
        _connection.Execute($@"
            CREATE TABLE IF NOT EXISTS {StickerTableName} (
                Id BIGINT PRIMARY KEY AUTO_INCREMENT,
                DefIndex INT,
                Wear DOUBLE NOT NULL,
                Parent_id BIGINT,
                FOREIGN KEY (parent_id) REFERENCES {TableName}(id)
            );
        ");
        _connection.Close();

    }

    private void _DumpTable()
    {
        _connection.Open();

        _connection.Execute($@"
            CREATE TABLE IF NOT EXISTS {TableName} (
                Id BIGINT PRIMARY KEY AUTO_INCREMENT,
                DefIndex INT,
                Paint INT,
                Seed INT,
                Wear DOUBLE NOT NULL,
                IsKnife BOOLEAN NOT NULL,
                steamid VARCHAR(255) NOT NULL
            );
        ");

        List<string> newColumns = new List<string> { "Type INT" };

        var existingColumns = _connection.Query($@"
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = '{TableName}'
        ").ToList();

        foreach (var column in newColumns.Where((column) => existingColumns.Contains(column.Split(" ")[0])).ToList())
        {
            var alterTableQuery = $"ALTER TABLE {TableName} ADD COLUMN {column}";
            _connection.Execute(alterTableQuery);
        }
        _connection.Close();
    }

    public Weapon? Create(Weapon data)
    {
        _connection.Open();

        try
        {
            int index = _connection.QuerySingle($@"
                INSERT INTO {TableName} (DefIndex, Paint, Seed, Wear, IsKnife, steamid, Type)
                VALUES (@DefIndex, @Paint, @Seed, @Wear, @IsKnife, @SteamId, @Type);
                SELECT LAST_INSERT_ID();",
             new
             {
                 DefIndex = data.DefIndex,
                 Paint = data.Paint,
                 Seed = data.Seed,
                 Wear = data.Wear,
                 IsKnife = data.IsKnife,
                 SteamId = data.steamid,
                 Type = data.type
             });

            foreach (var sticker in data.stickers)
            {
                _connection.Execute($@"
                    INSERT INTO {StickerTableName} (DefIndex, Wear, Parent_id)
                    VALUES (@DefIndex, @Wear, @Parent_id);
                    SELECT LAST_INSERT_ID();",
               new
               {
                   DefIndex = sticker.DefIndex,
                   Wear = sticker.Wear,
                   Parent_id = index
               });
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error inserting data: {e.Message}");
        }

        _connection.Close();
        return Get(data.steamid!).First();
    }

    public void Delete(string uuid)
    {
        throw new NotImplementedException();
    }

    public List<Weapon> GetAll()
    {
        _connection.Open();
        List<Weapon> skins = _connection.Query<Weapon>($"SELECT * FROM {TableName}").ToList();
        _connection.Close();
        return skins;
    }

    public List<Weapon> Get(string uuid)
    {
        List<Weapon> weapons = new();
        _connection.Open();
        try
        {
            var query = $@"
                SELECT
                    w.*,
                    s.*
                FROM
                    {TableName} w
                LEFT JOIN
                    {StickerTableName} s
                ON
                    w.Id = s.Parent_id";

            var weaponDictionary = new Dictionary<long, Weapon>();

            _connection.Query<Weapon, WeaponSticker, Weapon>(
                query,
                (weapon, sticker) =>
                {
                    if (!weaponDictionary.TryGetValue(weapon.Id, out var weaponEntry))
                    {
                        weaponEntry = weapon;
                        weaponEntry.stickers = new List<WeaponSticker>();
                        weaponDictionary.Add(weaponEntry.Id, weaponEntry);
                    }

                    if (sticker != null)
                        weaponEntry.stickers.Add(sticker);

                    return weaponEntry;
                },
                splitOn: "Id"
            );

            weapons = weaponDictionary.Values.ToList();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        _connection.Close();
        return weapons;
    }

    public void UpdateOne(Weapon data)
    {
        try
        {
            _connection.Open();

            _connection.Execute($@"
                UPDATE {TableName}
                SET DefIndex = @DefIndex, Paint = @Paint, Seed = @Seed, Wear = @Wear, IsKnife = @IsKnife, steamid = @SteamId
                WHERE Id = @Id;",
            new
            {
                DefIndex = data.DefIndex,
                Paint = data.Paint,
                Seed = data.Seed,
                Wear = data.Wear,
                IsKnife = data.IsKnife,
                SteamId = data.steamid,
                Id = data.Id,
            });

            _connection.Execute($@"
                DELETE FROM weapon_stickers
                WHERE Parent_id = @Parent_id",
            new
            {
                Parent_id = data.Id
            }
            );

            foreach (var sticker in data.stickers)
            {
                _connection.Execute($@"
                    INSERT INTO {StickerTableName} (DefIndex, Wear, Parent_id)
                    VALUES (@DefIndex, @Wear, @Parent_id);
                    SELECT LAST_INSERT_ID();",
               new
               {
                   DefIndex = sticker.DefIndex,
                   Wear = sticker.Wear,
                   Parent_id = data.Id
               });
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Erro ao atualizar dados: {e.Message}");
        }
        _connection.Close();
    }

    public void Update(Weapon data)
    {
        throw new NotImplementedException();
    }
}