using Sharp.Shared.GameEntities;

namespace ZombieModSharp.Abstractions;

public interface IGrenadeEffect
{
    public bool IgnitePawn(IPlayerPawn? playerPawn, int damage = 1, float duration = 1);
    public void ApplyFreeze(IBaseEntity grenade, float distanceLimit, float duration);
    public void ApplyLightGrenade(IBaseEntity grenade, float duration);
}