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
    private readonly IPlayerManager _playerManager;
    private readonly IEntityManager _entityManager;
    private readonly ICvarServices _cvarServices;
    private readonly ITransmitManager _transmitManager;

    public HitmarkerServices(ISharedSystem sharedSystem, IPlayerManager playerManager, IEntityManager entityManager, ICvarServices cvarServices)
    {
        _sharedSystem = sharedSystem;
        _playerManager = playerManager;
        _entityManager = entityManager;
        _cvarServices = cvarServices;
        _transmitManager = _sharedSystem.GetTransmitManager();
    }

    public void OnPlayerSpawn(IGameClient client)
    {
        var player = _playerManager.GetOrCreatePlayer(client);

        if (player == null)
            return;

        CreateNewHitmarker(player);
    }

    public void OnPlayerHurt(IGameClient attacker, bool headshot)
    {
        if(attacker == null)
            return;

        var pawn = attacker.GetPlayerController()?.GetPlayerPawn();

        if(pawn == null || !pawn.IsAlive)
            return;

        var player = _playerManager.GetOrCreatePlayer(attacker);

        if(player == null)
            return;

        if(player.HitmarkerBody == null || player.HitmarkerHead == null)
            CreateNewHitmarker(player);

        if(headshot)
        {
            player.HitmarkerHead?.AcceptInput("Stop");
            player.HitmarkerHead?.AddIOEvent(0.1f, "Start", null, null);
        }

        else
        {
            player.HitmarkerBody?.AcceptInput("Stop");
            player.HitmarkerBody?.AddIOEvent(0.1f, "Start", null, null);
        }
    }

    
    private void CreateNewHitmarker(Player player)
    {
        RemoveAllHitmarker(player);

        // we re-create it here.
        if(!string.IsNullOrEmpty(_cvarServices.CvarList["Cvar_HitmarkerBody"]?.GetString()))
        {
            player.HitmarkerBody = _entityManager.CreateEntityByName<IBaseParticle>("info_particle_system");
            var kv = new Dictionary<string, KeyValuesVariantValueItem>
            {
                { "effect_name", _cvarServices.CvarList["Cvar_HitmarkerBody"]!.GetString() }
            };

            player.HitmarkerBody?.DispatchSpawn(kv);

            _transmitManager.AddEntityHooks(player.HitmarkerBody!, true);
        }

        if(!string.IsNullOrEmpty(_cvarServices.CvarList["Cvar_HitmarkerHead"]?.GetString()))
        {
            player.HitmarkerHead = _entityManager.CreateEntityByName<IBaseParticle>("info_particle_system");
            var kv = new Dictionary<string, KeyValuesVariantValueItem>
            {
                { "effect_name", _cvarServices.CvarList["Cvar_HitmarkerHead"]!.GetString() }
            };

            player.HitmarkerHead?.DispatchSpawn(kv);

            _transmitManager.AddEntityHooks(player.HitmarkerHead!, true);
        }

        if(!string.IsNullOrEmpty(_cvarServices.CvarList["Cvar_HitmarkerProp"]?.GetString()))
        {
            player.HitmarkerProp = _entityManager.CreateEntityByName<IBaseParticle>("info_particle_system");
            var kv = new Dictionary<string, KeyValuesVariantValueItem>
            {
                { "effect_name", _cvarServices.CvarList["Cvar_HitmarkerProp"]!.GetString() }
            };

            player.HitmarkerProp?.DispatchSpawn(kv);

            _transmitManager.AddEntityHooks(player.HitmarkerProp!, true);
        }
    }

    private void RemoveAllHitmarker(Player player)
    {
        // we destroy it first.
        if(player.HitmarkerBody != null || (player.HitmarkerBody?.IsValid() ?? false))
            player.HitmarkerBody.Kill();

        if(player.HitmarkerHead != null || (player.HitmarkerHead?.IsValid() ?? false))
            player.HitmarkerHead.Kill();

        if(player.HitmarkerProp != null || (player.HitmarkerProp?.IsValid() ?? false))
            player.HitmarkerProp.Kill();
    }
}