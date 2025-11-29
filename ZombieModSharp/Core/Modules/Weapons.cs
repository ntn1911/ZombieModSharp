using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sharp.Extensions.CommandManager;
using Sharp.Shared;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using ZombieModSharp.Abstractions;

namespace ZombieModSharp.Core.Modules;

public class WeaponData
{
    public required string EntityName { get; set; }
    public float Knockback { get; set; } = 1.0f;
    public bool Restrict { get; set; } = false;
    public int MaxPurchase { get; set; } = -1;
    public List<string> Command { get; set; } = [];
    public int Price { get; set; }
}

public class Weapons : IWeapons
{
    private readonly ISharedSystem _sharedSystem;
    private readonly ILogger<Weapons> _logger;
    private readonly IModSharp _modsharp;
    private readonly ICommandManager _commandManager;
    private readonly ICommand _command;

    private Dictionary<string, WeaponData> weaponDatas = [];

    public Weapons(ISharedSystem sharedSystem, ILogger<Weapons> logger, ICommandManager commandManager, ICommand command)
    {
        _sharedSystem = sharedSystem;
        _logger = _sharedSystem.GetLoggerFactory().CreateLogger<Weapons>();
        _modsharp = _sharedSystem.GetModSharp();
        _commandManager = commandManager;
        _command = command;
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse weapons configuration");
        }
    }

    public void AssignWeaponPurchaseCommand()
    {
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

    public void OnPurchaseWeaponCommand(IGameClient client, StringCommand command)
    {
        var arg = command.GetArg(0);
        var weaponData = weaponDatas.FirstOrDefault(w => w.Value.Command.Contains(arg)).Value;

        if(weaponData == null)
        {
            _command.ReplyToCommand(client, "Invalid weapon command!");
            return;
        }

        PurchaseWeapon(client, weaponData);
    }

    public void PurchaseWeapon(IGameClient client, WeaponData weapon)
    {
        var controller = client.GetPlayerController();
        var pawn = controller?.GetPlayerPawn();
        
        if(weapon.Restrict)
        {
            _command.ReplyToCommand(client, $"weapon \x05{weapon.EntityName}\x01 is restricted");
            return;
        }

        
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
}