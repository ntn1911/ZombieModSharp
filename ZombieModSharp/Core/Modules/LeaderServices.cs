using Microsoft.Extensions.Logging;
using Sharp.Extensions.GameEventManager;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using ZombieModSharp.Abstractions;

namespace ZombieModSharp.Core.Modules;

public class LeaderServices : ILeaderServices
{
    private static readonly List<IPlayerController> _leaders = new();
    private static ISharedSystem? _sharedSystem;
    private static ILogger<LeaderServices> _logger;
    private readonly IGlowServices _glowMethod;
    private readonly IGameEventManager _gameEventManager;

    public LeaderServices(ISharedSystem sharedSystem, ILogger<LeaderServices> logger, IGlowServices glowMethod, IGameEventManager gameEventManager)
    {
        _sharedSystem = sharedSystem;
        _logger = logger;
        _glowMethod = glowMethod;
        _gameEventManager = gameEventManager;
    }

    public bool AssignLeader(IPlayerController controller)
    {
        if (controller == null || !controller.IsValid())
            return false;

        if (!_leaders.Contains(controller))
        {
            _leaders.Add(controller);
            return true;
        }
        return false;
    }

    
    public bool RemoveLeader(IPlayerController controller)
    {
        if (controller == null || !controller.IsValid())
            return false;

        return _leaders.Remove(controller);
    }

    
    public void ClearAllLeaders()
    {
        _leaders.Clear();
    }

    
    public bool IsLeader(IPlayerController? controller)
    {
        if (controller == null || !controller.IsValid())
            return false;

        return _leaders.Contains(controller);
    }

    
    public IEnumerable<IPlayerController> GetAllLeaders()
    {
        return _leaders.ToList();
    }

    
    public void ReloadLeaderList(ISharedSystem sharedSystem)
    {
        var refreshed = new List<IPlayerController>();

        foreach (var controller in sharedSystem.GetEntityManager().FindPlayerControllers())
        {
            if (controller != null && controller.IsValid() && _leaders.Contains(controller))
            {
                refreshed.Add(controller);
            }
        }

        _leaders.Clear();
        _leaders.AddRange(refreshed);
    }

    private IPlayerController? FindTargetPlayer(StringCommand command, IGameClient self)
    {
        if (command.ArgCount == 0)
            return self.GetPlayerController();

        var nameArg = command.GetArg(1).Trim();
        var players = _sharedSystem!.GetEntityManager().FindPlayerControllers();

        var exact = players.FirstOrDefault(p =>
            p.IsValid() &&
            p.IsConnected() &&
            !string.IsNullOrEmpty(p.GetGameClient()?.Name) &&
            p.GetGameClient()!.Name.Equals(nameArg, StringComparison.OrdinalIgnoreCase));

        if (exact != null)
            return exact;

        var partial = players.FirstOrDefault(p =>
            p.IsValid() &&
            p.IsConnected() &&
            !string.IsNullOrEmpty(p.GetGameClient()?.Name) &&
            p.GetGameClient()!.Name.Contains(nameArg, StringComparison.OrdinalIgnoreCase));

        return partial;
    }

    public void UpdateClientClanTags()
    {
        try
        {
            if (_gameEventManager.CreateEvent("nextlevel_changed", false) is { } evt)
            {
                evt.FireToClients();
                evt.Dispose();
            }
        }
        catch (Exception)
        {
            // 忽略錯誤，避免伺服器崩潰
        }
    }
    
}
