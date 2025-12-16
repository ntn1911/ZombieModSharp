using Sharp.Shared.Objects;

namespace ZombieModSharp.Abstractions;

public interface IHitmarkerServices
{
    void OnPlayerHurt(IGameClient attacker, bool headshot);
    void OnPlayerSpawn(IGameClient client);
}