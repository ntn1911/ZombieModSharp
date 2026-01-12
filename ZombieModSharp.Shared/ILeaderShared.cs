using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;

namespace ZombieModSharp.Shared;

public interface ILeaderShared
{
    const string Identity = nameof(ILeaderShared);

    bool IsClientLeader(IPlayerController? controller);
}