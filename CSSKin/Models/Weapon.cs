using System.Text.Json.Serialization;
using CSSKin.Core.Enums;
using MongoDB.Bson.Serialization.Attributes;

namespace CSSKin.Models;

//      def paint Seed  Wear          Stickers          Unknown StartTrakEnable StarTrakValue NameTag
// !gen 7   801   1     0.4   1353 0 1353 0 1353 0 1353 0 0 0       1           123           My Ak

public class Weapon
{
    [BsonId]
    public long Id { get; set; }
    public int DefIndex { get; set; }
    public int Paint { get; set; }
    public int Seed { get; set; }
    public double Wear { get; set; }
    public bool IsKnife { get; set; }
    public string? steamid { get; set; }
    [JsonPropertyName("WeaponType")]
    public int type { get; set; } = (int)WeaponType.Weapon;
    public List<WeaponSticker> stickers { get; set; } = new List<WeaponSticker>();
}