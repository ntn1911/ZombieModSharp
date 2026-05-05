using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using ZombieModSharp.Abstractions;

namespace ZombieModSharp.Core.Modules;

public class ClassAttribute
{
    public string Name { get; set; } = string.Empty;
    public int Team { get; set; } = 0;
    public bool MotherZombie { get; set; } = false;
    public string Model { get; set; } = "default";
    public int Health { get; set; } = 100;
    public int HealthRegen { get; set; } = 0;
    public float HealthRegenInterval { get; set; } = 0.0f;
    public float NapalmDuration { get; set; } = 0.0f;
    public float Knockback { get; set; } = 3.0f;
    public float Speed { get; set; } = 250f;
}

public class PlayerClasses : IPlayerClasses
{
    private readonly ISharedSystem _sharedSystem;
    private readonly ILogger<PlayerClasses> _logger;
    private readonly IPlayerManager _playerManager;
    private readonly IModSharp _modSharp;

    public PlayerClasses(ISharedSystem sharedSystem, IPlayerManager playerManager)
    {
        _sharedSystem = sharedSystem;
        _logger = _sharedSystem.GetLoggerFactory().CreateLogger<PlayerClasses>();
        _playerManager = playerManager;
        _modSharp = _sharedSystem.GetModSharp();
    }

    public Dictionary<string, ClassAttribute> classesData = [];

    public void LoadConfig(string path)
    {
        var configPath = Path.Combine(path, "playerclasses.jsonc");

        if (!File.Exists(configPath))
        {
            _logger.LogCritical("File is not found!");
            return;
        }

        classesData.Clear();

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

            classesData = JsonSerializer.Deserialize<Dictionary<string, ClassAttribute>>(cleanedJson) ?? [];

            _logger.LogInformation("Successfully loaded {count} classes configurations", classesData.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse classes configuration");
        }
    }

    public void ApplyPlayerClassAttribute(IPlayerPawn playerPawn, ClassAttribute classAttribute)
    {
        if (!playerPawn.IsAlive)
        {
            return;
        }

        playerPawn.Health = classAttribute.Health;
        var team = classAttribute.Team;

        if (classAttribute.Model != "default" || !string.IsNullOrEmpty(classAttribute.Model))
            playerPawn.SetModel(classAttribute.Model);

        else
        {
            if (team == 0)
                playerPawn.SetModel("characters/models/tm_phoenix/tm_phoenix.vmdl");

            if (team == 1)
                playerPawn.SetModel("characters/models/ctm_sas/ctm_sas.vmdl");
        }

        var gameClient = playerPawn.GetController()?.GetGameClient();

        if (gameClient == null)
        {
            _logger.LogError("IGameClient is null!");
            return;
        }

        var client = _playerManager.GetOrCreatePlayer(gameClient);

        SetClassArmor(playerPawn, team);
        InitialHeathRegen(playerPawn, classAttribute, client);

        client.ActiveClass = classAttribute;
    }

    private void SetClassArmor(IPlayerPawn playerPawn, int team)
    {
        if (team == 0)
        {
            playerPawn.ArmorValue = 0;
            try
            {
                playerPawn.GetItemService()!.HasHelmet = false;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error: {ex}", ex);
            }
        }
        else
        {
            playerPawn.ArmorValue = 100;
        }
    }
    
    public ClassAttribute? GetClassByName(string classname)
    {
        if (string.IsNullOrEmpty(classname))
            return null;

        if (!classesData.TryGetValue(classname, out var targetClass))
            return null;

        return targetClass;
    }

    public ClassAttribute? GetMotherZombieClass()
    {
        return classesData.Values.FirstOrDefault(c => c.MotherZombie);
    }

    private void InitialHeathRegen(IPlayerPawn playerPawn, ClassAttribute classAttribute, Player? player)
    {
        if (player == null)
        {
            return;
        }

        if(player.RegenerationTimer != Guid.Empty)
        {
            _modSharp.StopTimer(player.RegenerationTimer);
            player.RegenerationTimer = Guid.Empty;
        }

        if (classAttribute.HealthRegen > 0 && classAttribute.HealthRegenInterval > 0)
        {
            player.RegenerationTimer = _modSharp.PushTimer(new Func<TimerAction>(() =>
            {
                if (!playerPawn.IsAlive)
                {
                    return TimerAction.Stop;
                }

                var currentHealth = playerPawn.Health;
                var maxHealth = classAttribute.Health;

                if (currentHealth < maxHealth)
                {
                    var newHealth = Math.Min(currentHealth + classAttribute.HealthRegen, maxHealth);
                    playerPawn.Health = newHealth;
                }

                return TimerAction.Continue;
            }), classAttribute.HealthRegenInterval, GameTimerFlags.Repeatable | GameTimerFlags.StopOnRoundEnd | GameTimerFlags.StopOnMapEnd);
        }
    }
}