using Sharp.Shared.Objects;

namespace ZombieModSharp.Shared;

public interface IInfectShared
{
    const string Identity = nameof(IInfectShared);

    void InfectPlayer(IGameClient client, IGameClient? attacker = null, bool motherzombie = false, bool force = false);
    void HumanizeClient(IGameClient client, bool force = false);
    bool IsClientInfect(IGameClient client);
    bool IsClientHuman(IGameClient client);
}
