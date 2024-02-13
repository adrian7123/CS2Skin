using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using CSSKin.Core.Configs;
using CSSKin.Core.Enums;
using CSSKin.Core.Repository;
using CSSKin.Core.Utilities;
using CSSKin.Models;
using Microsoft.Extensions.Logging;

namespace CSSKin;

public class CSSkin : BasePlugin, IPluginConfig<BaseConfig>
{
    public override string ModuleName => "CsSkin";
    public override string ModuleVersion => "1.1.0";
    public BaseConfig Config { get; set; } = new BaseConfig();
    private Dictionary<ulong, List<Weapon>> playerWeapons = new();
    private IRepository<Weapon>? repository;

    public void OnConfigParsed(BaseConfig config)
    {
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        Logger.LogInformation("Plugin loaded");

        switch (Config.DbType)
        {
            case nameof(DatabaseType.MYSQL):
                repository = new WeaponMysqlRepository(Config.ConnectionString, Config.MysqlTableName);
                break;
            case nameof(DatabaseType.MONGODB):
                repository =
                    new WeaponCollectionRepository(Config.ConnectionString, Config.MongoDatabaseName);
                break;
        }


        RegisterListener<Listeners.OnClientPutInServer>((slot) =>
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            var skins = repository?.Get(player.SteamID.ToString());
            if (!playerWeapons.TryAdd(player.SteamID,
                    skins != null ? skins.ToList() : new List<Weapon>()))
            {
                playerWeapons[player.SteamID] = skins != null ? skins.ToList() : new List<Weapon>();
            }
        });

        RegisterListener<Listeners.OnClientDisconnect>(slot =>
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            playerWeapons[player.SteamID] = new List<Weapon>();
        });

        RegisterListener<Listeners.OnEntityCreated>(entity => { });

        RegisterListener<Listeners.OnEntitySpawned>(entity =>
        {
            CBasePlayerWeapon? pBasePlayerWeapon = new(entity.Handle);
            CEconEntity pCEconEntityWeapon = new(entity.Handle);

            Logger.LogInformation(pCEconEntityWeapon.DesignerName);

            Server.NextFrame(() =>
            {
                if (pCEconEntityWeapon != null && pCEconEntityWeapon.DesignerName != null &&
                    pCEconEntityWeapon.DesignerName.StartsWith("weapon_"))
                {
                    string designerName = pCEconEntityWeapon.DesignerName;

                    bool isKnife = designerName.Contains("knife") || designerName.Contains("bayonet");
                    bool isWeapon = designerName.Contains("weapon_") && !isKnife;

                    ushort weaponId = pCEconEntityWeapon.AttributeManager.Item.ItemDefinitionIndex;
                    int weaponOwner = (int)pBasePlayerWeapon.OwnerEntity.Index;

                    CBasePlayerPawn pBasePlayerPawn =
                        new CBasePlayerPawn(NativeAPI.GetEntityFromIndex(weaponOwner));

                    if (!pBasePlayerPawn.IsValid) return;

                    var playerIndex = (int)pBasePlayerPawn.Controller.Index;
                    var player = Utilities.GetPlayerFromIndex(playerIndex);

                    var skins = repository?.Get(player.SteamID.ToString());
                    if (!playerWeapons.TryAdd(player.SteamID,
                            skins != null ? skins.ToList() : new List<Weapon>()))
                    {
                        playerWeapons[player.SteamID] = skins != null ? skins.ToList() : new List<Weapon>();
                    }

                    playerWeapons.TryGetValue(player.SteamID, out List<Weapon>? weapons);

                    var requestWeapon = weapons?.FirstOrDefault(c =>
                        c.DefIndex == weaponId && !isKnife ||
                        isKnife && ConstantsWeapon.g_KnivesMap.ContainsValue(designerName));

                    if (requestWeapon != null)
                    {
                        Weapon? weapon = weapons?.FirstOrDefault(weapon =>
                            (weaponId == weapon.DefIndex && isWeapon && !isKnife) ||
                            ConstantsWeapon.g_KnivesMap.ContainsKey(weaponId));

                        pCEconEntityWeapon.FallbackPaintKit = weapon!.Paint;
                        pCEconEntityWeapon.FallbackSeed = weapon.Seed;
                        pCEconEntityWeapon.FallbackWear = (float)weapon.Wear;
                        pCEconEntityWeapon.FallbackStatTrak = -1;

                        pCEconEntityWeapon.AttributeManager.Item.ItemDefinitionIndex = (ushort)weapon.DefIndex;
                        pCEconEntityWeapon.AttributeManager.Item.ItemID = 16384;
                        pCEconEntityWeapon.AttributeManager.Item.ItemIDLow = 16384 & 0xFFFFFFFF;
                        pCEconEntityWeapon.AttributeManager.Item.ItemIDHigh = 16384 >> 32;

                        if (pBasePlayerWeapon.CBodyComponent is { SceneNode: not null })
                        {
                            var skeleton = GetSkeletonInstance(pBasePlayerWeapon.CBodyComponent.SceneNode);
                            skeleton.ModelState.MeshGroupMask = 2;
                        }

                        if (ConstantsWeapon.g_KnivesMap.ContainsKey(weaponId))
                        {
                            Server.ExecuteCommand("sv_cheats 1");

                            pBasePlayerWeapon.AttributeManager.Item.EntityQuality = 3;
                            Server.ExecuteCommand($"subclass_change {weapon.DefIndex} {entity.Index}");

                            Server.ExecuteCommand("sv_cheats 0");
                        }
                    }
                }
            });
        });

        RegisterListener<Listeners.OnEntityParentChanged>((entity, parent) => { });

        RegisterListener<Listeners.OnEntityDeleted>(entity => { });

        base.Load(hotReload);
    }

    private static CSkeletonInstance GetSkeletonInstance(CGameSceneNode node)
    {
        Func<nint, nint> GetSkeletonInstance = VirtualFunction.Create<nint, nint>(node.Handle, 8);
        return new CSkeletonInstance(GetSkeletonInstance(node.Handle));
    }


    [ConsoleCommand("css_glove", "Get Gloves")]
    [ConsoleCommand("css_gengl", "Get Gloves")]
    [CommandHelper(minArgs: 1, usage: "[defIndex] [paintId] [seed]", whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnGloveCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        // ====== Gloves ========
        CEconWearable? entity = Utilities.CreateEntityByName<CEconWearable>("wearable_item");

        entity!.AttributeManager.Item.ItemDefinitionIndex = 5027;
        entity!.FallbackPaintKit = 10006;

        entity!.FallbackSeed = 0;
        entity!.FallbackWear = 0.00000001f;
        entity!.FallbackStatTrak = -1;

        entity!.AttributeManager.Item.ItemID = 16384;
        entity!.AttributeManager.Item.ItemIDLow = 16384 & 0xFFFFFFFF;
        entity!.AttributeManager.Item.ItemIDHigh = 16384 >> 32;
        entity!.AttributeManager.Item.Initialized = true;

        // Schema.SetSchemaValue(entity.Handle, "CBaseEntity", "m_hOwnerEntity", player);
        // Schema.SetSchemaValue(entity.Handle, "CBaseEntity", "m_hParent", player);

        // entity.DispatchSpawn();

        // Schema.SetSchemaValue(player.PlayerPawn.Value.Handle, "CBaseCombatCharacter", "m_hMyWearables", entity);
    }

    // Commands can also be registered using the `Command` attribute.
    [ConsoleCommand("css_skin", "Get skin")]
    [ConsoleCommand("css_gen", "Get skin")]
    // The `CommandHelper` attribute can be used to provide additional information about the command.
    [CommandHelper(minArgs: 1, usage: "[defIndex] [paintId] [seed]", whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnCssSkinCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        var defIndex = int.Parse(commandInfo.GetArg(1));
        var paintId = int.Parse(commandInfo.GetArg(2));
        var seed = int.Parse(commandInfo.GetArg(3));

        ulong playerSteamId = player!.SteamID;

        bool isKnife = ConstantsWeapon.g_KnivesMap.ContainsKey(defIndex);
        bool isWeapon = ConstantsWeapon.g_WeaponsMap.ContainsKey(defIndex);

        if (!player.IsValid || player.Index <= 0) return;

        var skins = repository?.Get(playerSteamId.ToString()).ToList();

        if (isKnife)
        {
            var skin = skins?.FirstOrDefault(data => data.IsKnife);
            if (skin != null)
            {
                skin.DefIndex = defIndex;
                skin.Paint = paintId;
                skin.Wear = 0.00000001f;
                skin.Seed = seed;
                skin.IsKnife = true;
                skin.steamid = player.SteamID.ToString();
                repository?.UpdateOne(skin);
            }
            else
            {
                var newSkin = new Weapon()
                {
                    steamid = playerSteamId.ToString(),
                    Seed = seed,
                    Wear = 0.00000001f,
                    IsKnife = true,
                    DefIndex = defIndex,
                    Paint = paintId
                };
                repository?.Create(newSkin);
            }
        }

        if (isWeapon)
        {
            var skin = skins?.FirstOrDefault(data => data.DefIndex == defIndex);
            if (skin != null)
            {
                skin.DefIndex = defIndex;
                skin.Paint = paintId;
                skin.Wear = 0.00000001f;
                skin.Seed = seed;
                skin.IsKnife = false;
                skin.steamid = player.SteamID.ToString();
                repository?.UpdateOne(skin);
            }
            else
            {
                var newSkin = new Weapon()
                {
                    steamid = playerSteamId.ToString(),
                    Seed = seed,
                    Wear = 0.00000001f,
                    IsKnife = false,
                    DefIndex = defIndex,
                    Paint = paintId
                };
                repository?.Create(newSkin);
            }
        }

        playerWeapons[playerSteamId] = repository!.Get(playerSteamId.ToString()).ToList();

        var weapons = player.PlayerPawn.Value?.WeaponServices?.MyWeapons;

        foreach (var weaponData in weapons!)
        {
            if (weaponData.IsValid && weaponData.Value != null)
            {
                if (ConstantsWeapon.g_KnivesMap.ContainsKey(weaponData.Value.AttributeManager.Item
                        .ItemDefinitionIndex) || ConstantsWeapon.g_WeaponsMap.ContainsKey(weaponData.Value
                        .AttributeManager.Item
                        .ItemDefinitionIndex))
                {
                    if (isWeapon)
                    {
                        player.RemoveItemByDesignerName(weaponData.Value.DesignerName, true);
                    }

                    if (isKnife)
                    {
                        player.RemoveItemByDesignerName("weapon_knife", true);
                    }
                }
            }
        }

        if (ConstantsWeapon.g_WeaponsMap.TryGetValue(defIndex, out string? weapon_name))
        {
            player.GiveNamedItem(weapon_name);
        }

        if (ConstantsWeapon.g_KnivesMap.ContainsKey(defIndex))
        {
            player.GiveNamedItem("weapon_knife");
        }
    }
}