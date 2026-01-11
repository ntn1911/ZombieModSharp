using Sharp.Shared.Objects;
using ZombieModSharp.Shared;

namespace ZombieModSharp.Abstractions;

public interface IInfect : IInfectShared
{
    public void OnRoundPreStart();
    public void OnRoundStart();
    public void OnRoundEnd();
    public void OnRoundFreezeEnd();
    public void CheckGameStatus();
    public bool IsInfectStarted();
    public void SetInfectStarted(bool result);
}