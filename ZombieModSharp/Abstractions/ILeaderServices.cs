using Sharp.Shared;
using Sharp.Shared.GameEntities;
using System;
using System.Collections.Generic;
using System.Text;

namespace ZombieModSharp.Abstractions
{
    public interface ILeaderServices
    {
        public bool AssignLeader(IPlayerController controller);
        public bool RemoveLeader(IPlayerController controller);

        public bool IsLeader(IPlayerController? controller);
        public IEnumerable<IPlayerController> GetAllLeaders();

        public void ReloadLeaderList(ISharedSystem sharedSystem);

        public void UpdateClientClanTags();
    }
}
