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
        if (voter == null || target == null)
            return (false, 0, 0, "Invalid voter or target");

        if (!voter.IsValid || !target.IsValid)
            return (false, 0, 0, "Voter or target is not valid");

        if (!target.IsConnected)
            return (false, 0, 0, "Target is not connected");

        var targetName = target.Name;
        if (string.IsNullOrEmpty(targetName))
            return (false, 0, 0, "Target name unavailable");

        // 計算當前連線玩家數
        var players = _sharedSystem?.GetEntityManager().FindPlayerControllers() ?? Enumerable.Empty<IPlayerController>();
        
        var connectedCount = players.Count(p => p != null && p.IsValid() && p.IsConnected());

        int votesNeeded;
        if (connectedCount >= 5)
        {
            votesNeeded = (int)Math.Ceiling(connectedCount * 0.75);
        }
        else
        {
            votesNeeded = 1;
        }

        // 若少於 5 人，整個伺服器 leader 最多 2 人
        if (connectedCount < 5 && _leaders.Count >= 2)
        {
            return (false, 0, votesNeeded, "Leader limit reached (max 2)");
        }

        lock (_voteLock)
        {
            if (!_voteMap.TryGetValue(targetName, out var voters))
            {
                voters = new HashSet<string>();
                _voteMap[targetName] = voters;
            }

            var voterKey = voter.UserId.ToString();

            if (!voters.Add(voterKey))
            {
                return (false, voters.Count, votesNeeded, "You already voted for this player");
            }

            // 若達到門檻
            if (voters.Count >= votesNeeded)
            {
                // 再次確認 leader 人數限制
                if (_leaders.Count >= 2)
                {
                    // 清除該目標的票（避免殘留）
                    _voteMap.Remove(targetName);
                    return (false, voters.Count, votesNeeded, "Leader limit reached (max 2)");
                }

                // 取得目標 controller 並指定 leader
                var controller = target.GetPlayerController();
                if (controller == null || !controller.IsValid())
                {
                    // 清票避免殘留
                    _voteMap.Remove(targetName);
                    return (false, voters.Count, votesNeeded, "Target controller invalid");
                }

                var assigned = AssignLeader(controller);
                // 設定 clan tag（與 OnLeaderCommand 行為一致）
                try
                {
                    controller.SetClanTag(" [Leader]  ");
                    UpdateClientClanTags();
                }
                catch
                {
                    // 忽略任何錯誤，仍繼續流程
                }

                // 嘗試建立 glow（非必要）
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

                // 清除該目標的票
                _voteMap.Remove(targetName);

                return (assigned, 0, votesNeeded, "Leader assigned");
            }

            // 尚未達成門檻
            return (false, voters.Count, votesNeeded, $"Vote registered {voters.Count}/{votesNeeded}");
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
