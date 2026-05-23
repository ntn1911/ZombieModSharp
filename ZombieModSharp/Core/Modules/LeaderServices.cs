using Microsoft.Extensions.Logging;
using Sharp.Extensions.GameEventManager;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using ZombieModSharp.Abstractions;
using ZombieModSharp.Shared;

namespace ZombieModSharp.Core.Modules;

public class LeaderServices : ILeaderServices
{
    private readonly List<IPlayerController> _leaders = new();
    private ISharedSystem? _sharedSystem;
    private ILogger<LeaderServices> _logger;
    private readonly IGlowServices _glowMethod;
    private readonly IGameEventManager _gameEventManager;

    //Vote logics for leader side
    private readonly Dictionary<string, HashSet<string>> _voteMap = new();
    private readonly object _voteLock = new();

    public LeaderServices(ISharedSystem sharedSystem, ILogger<LeaderServices> logger, IGlowServices glowMethod, IGameEventManager gameEventManager)
    {
        _sharedSystem = sharedSystem;
        _logger = logger;
        _glowMethod = glowMethod;
        _gameEventManager = gameEventManager;
    }

    public (bool becameLeader, int votes, int votesNeeded, string message) VoteLeader(IGameClient voter, IGameClient target)
    {
        if (voter == null || target == null || !voter.IsValid || !target.IsValid || !target.IsConnected)
            return (false, 0, 0, "Invalid voter or target");

        if (voter.IsFakeClient || voter.IsHltv)
            return (false, 0, 0, "Bots/HLTV cannot vote");

        var targetName = target.Name;
        if (string.IsNullOrEmpty(targetName))
            return (false, 0, 0, "Target name unavailable");

        // get connected player count for vote threshold
        var players = _sharedSystem?.GetEntityManager().FindPlayerControllers() ?? Enumerable.Empty<IPlayerController>();
        var connectedCount = players.Count(p => p != null && p.IsValid() && p.IsConnected());

        // if player count >= 5, need 75% votes; else only 1 vote needed
        var votesNeeded = connectedCount >= 5 ? (int)Math.Ceiling(connectedCount * 0.75) : 1;

        // if already 2 leaders and target not leader, reject immediately
        if (connectedCount < 5 && _leaders.Count >= 2)
            return (false, 0, votesNeeded, "Leader limit reached (max 2)");

        var voterKey = voter.SteamId.ToString();

        lock (_voteLock)
        {
            if (!_voteMap.TryGetValue(targetName, out var voters))
            {
                voters = new HashSet<string>();
                _voteMap[targetName] = voters;
            }

            if (!voters.Add(voterKey))
                return (false, voters.Count, votesNeeded, "You already voted for this player");

            if (voters.Count < votesNeeded)
                return (false, voters.Count, votesNeeded, $"Vote registered {voters.Count}/{votesNeeded}");

            // 已達門檻，確認 leader 上限與 controller 有效性
            if (_leaders.Count >= 2)
            {
                _voteMap.Remove(targetName);
                return (false, voters.Count, votesNeeded, "Leader limit reached (max 2)");
            }

            var controller = target.GetPlayerController();
            if (controller == null || !controller.IsValid())
            {
                _voteMap.Remove(targetName);
                return (false, voters.Count, votesNeeded, "Target controller invalid");
            }

            var assigned = AssignLeader(controller);

            // 非關鍵操作：設定 clan tag、更新 tag、嘗試 glow、廣播；錯誤不會中斷流程
            try { controller.SetClanTag(" [Leader]  "); UpdateClientClanTags(); } catch { }
            try
            {
                var pawn = controller.GetPlayerPawn();
                if (pawn != null && pawn.IsValid())
                {
                    var mode = IGlowServices.GlowVisibleMode.ExceptTarget;
                    _glowMethod.CreateGlow(target, pawn, new Color32(0, 255, 0, 255), 5000, mode);
                }
            }
            catch { }

            _voteMap.Remove(targetName);

            return (assigned, 0, votesNeeded, "Leader assigned");
        }
    }

    public bool AssignLeader(IPlayerController controller)
    {
        if (!controller.IsValid())
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

    
    public bool IsClientLeader(IPlayerController? controller)
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
