using Sharp.Shared;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using ZombieModSharp.Abstractions;

namespace ZombieModSharp.Core.Modules;

public class HitmarkerServices : IHitmarkerServices
{
    private readonly ISharedSystem _sharedSystem;
    private readonly ICvarServices _cvarServices;
    private readonly IModSharp _modsharp;

    public HitmarkerServices(ISharedSystem sharedSystem, ICvarServices cvarServices)
    {
        _sharedSystem = sharedSystem;
        _cvarServices = cvarServices;
        _modsharp = _sharedSystem.GetModSharp();
    }

    public void OnPlayerHurt(IGameClient attacker, bool headshot)
    {
        if(attacker == null)
            return;

        var pawn = attacker.GetPlayerController()?.GetPlayerPawn();

        if(pawn == null || !pawn.IsAlive)
            return;

        if(headshot)
        {
            _modsharp.DispatchParticleEffect(_cvarServices.CvarList["Cvar_HitmarkerHead"]!.GetString(), new(), new(), new(attacker));
        }

        else
        {
            _modsharp.DispatchParticleEffect(_cvarServices.CvarList["Cvar_HitmarkerBody"]!.GetString(), new(), new(), new(attacker));
        }
    }
}