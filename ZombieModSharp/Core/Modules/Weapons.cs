using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sharp.Extensions.CommandManager;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using ZombieModSharp.Abstractions;

namespace ZombieModSharp.Core.Modules;

public class WeaponData
{
    public required string WeaponName { get; set; }
    public required string EntityName { get; set; }
    public float Knockback { get; set; } = 1.0f;
    public bool Restrict { get; set; } = false;
    public int MaxPurchase { get; set; } = 0;
    public List<string> Command { get; set; } = [];
    public int Price { get; set; }
    public WeaponAmmo? Ammo { get; set; }
}

public class WeaponAmmo
{
    public int Clip { get; set; }
    public int ReserveAmmo { get; set; }
}

public class Weapons : IWeapons
{
    private readonly ISharedSystem _sharedSystem;
    private readonly ILogger<Weapons> _logger;
    private readonly IModSharp _modsharp;
    private readonly ICommandManager _commandManager;
    private readonly IPlayerManager _playerManager;

    private Dictionary<string, WeaponData> weaponDatas = [];

    public Weapons(ISharedSystem sharedSystem, ILogger<Weapons> logger, ICommandManager commandManager, IPlayerManager playerManager)
    {
        _sharedSystem = sharedSystem;
        _logger = _sharedSystem.GetLoggerFactory().CreateLogger<Weapons>();
        _modsharp = _sharedSystem.GetModSharp();
        _commandManager = commandManager;
        _playerManager = playerManager;
    }

    public void LoadConfig(string path)
    {
        var configPath = Path.Combine(path, "weapons.jsonc");

        if (!File.Exists(configPath))
        {
            _logger.LogCritical("File is not found!");
            return;
        }

        weaponDatas.Clear();

        try
        {
            var jsonContent = File.ReadAllText(configPath);
            
            // Simple comment removal (basic implementation)
            var lines = jsonContent.Split('\n');
            var cleanedLines = lines.Select(line => 
            {
                var commentIndex = line.IndexOf("//");
                return commentIndex >= 0 ? line.Substring(0, commentIndex) : line;
            });
            var cleanedJson = string.Join('\n', cleanedLines);

            weaponDatas = JsonSerializer.Deserialize<Dictionary<string, WeaponData>>(cleanedJson) ?? [];
            _logger.LogInformation("Successfully loaded {count} weapon configurations", weaponDatas.Count);
            AssignWeaponPurchaseCommand();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse weapons configuration");
        }
    }

    private void AssignWeaponPurchaseCommand()
    {
        if(weaponDatas == null || weaponDatas.Count <= 0)
            return;

        foreach(var weapon in weaponDatas)
        {
            if(weapon.Value.Command == null || weapon.Value.Command.Count <= 0)
                continue;

            foreach(var command in weapon.Value.Command)
            {
                _commandManager.RegisterClientCommand(command, OnPurchaseWeaponCommand);
            }
        }
    }

    private void OnPurchaseWeaponCommand(IGameClient client, StringCommand command)
    {
        var arg = command.GetArg(0);
        var weaponData = weaponDatas.FirstOrDefault(w => w.Value.Command.Contains(arg));

        if(weaponData.Value == null)
        {
            PrintToChat(client, "Invalid weapon command!");
            return;
        }

        PurchaseWeapon(client, weaponData.Value);
    }

    public void PurchaseWeapon(IGameClient client, WeaponData weapon)
    {
        var controller = client.GetPlayerController();
        var pawn = controller?.GetPlayerPawn();
        var player = _playerManager.GetOrCreatePlayer(client);

        if(weapon.Restrict)
        {
            PrintToChat(client, $"Weapon \x05{weapon.WeaponName}\x01 is restricted");
            return;
        }

        if(pawn == null || controller == null)
        {
            return;
        }

        if(pawn.Team <= CStrikeTeam.Spectator)
        {
            PrintToChat(client, "This feature require player to be in team.");
            return;
        }

        if(!pawn.IsAlive)
        {
            PrintToChat(client, "This feature require player to be alive.");
            return;
        }

        if(player.IsInfected())
        {
            PrintToChat(client, "This feautre require player to be human.");
            return;
        }

        if(weapon.MaxPurchase == -1)
        {
            PrintToChat(client, $"Weapon \x05{weapon.WeaponName}\x01 is restricted for purchasing, and only can be obtained in the map.");
            return;
        }

        if(weapon.MaxPurchase > 0)
        {
            if(player.PurchaseHistory[weapon.WeaponName] >= weapon.MaxPurchase)
            {
                PrintToChat(client, $"Your purchase of weapon \x05{weapon.WeaponName}\x01 has reached maximum number that allow this round.");
                return;
            }
        }

        var money = controller.GetInGameMoneyService()?.Account;

        if(money < weapon.Price)
        {
            PrintToChat(client, $"You don't have enough cash for purchasing this weapon! (Price: {weapon.Price}$)");
            return;
        }

        controller.GetInGameMoneyService()!.Account -= weapon.Price;

        if (!player.PurchaseHistory.ContainsKey(weapon.WeaponName))
            player.PurchaseHistory[weapon.WeaponName] = 0;

        player.PurchaseHistory[weapon.WeaponName] += 1;
        pawn.GiveNamedItem(weapon.EntityName);
        PrintToChat(client, $"You have purchased weapon \x05{weapon.WeaponName}\x01. {(weapon.MaxPurchase > 0 ? $"Purchases available left: ({weapon.MaxPurchase - player.PurchaseHistory[weapon.WeaponName]}/{weapon.MaxPurchase})" : "")}");
    }

    public float GetWeaponKnockback(string weaponentity)
    {
        if (!weaponDatas.TryGetValue(weaponentity, out var weaponData))
        {
            // _modsharp.PrintToChatAll($"No weapons name {weaponentity}");
            return 1.0f;
        }

        // _modsharp.PrintToChatAll($"Found {weaponData.EntityName} and KB: {weaponData.Knockback}");
        return weaponData.Knockback;
    }

    public WeaponAmmo? GetWeaponAmmo(string weaponentity)
    {
        return weaponDatas.FirstOrDefault(p => p.Value.EntityName == weaponentity).Value?.Ammo;
    }

    public bool IsWeaponRestricted(string weaponentity)
    {
        var data = weaponDatas.FirstOrDefault(p => p.Value.EntityName == weaponentity).Value;
        return data != null ? data.Restrict : false;
    }

    public WeaponData GetWeaponDataWithEntityName(string weaponentity)
    {
        var result = weaponDatas.FirstOrDefault(p => p.Value.EntityName == weaponentity).Value;
        return result;
    }

    private void PrintToChat(IGameClient client, string text)
    {
        _modsharp.PrintChannelFilter(HudPrintChannel.Chat, $"{ZombieModSharp.Prefix} {text}", new RecipientFilter(client));
    }
}