using ZombieModSharp.Core.Modules;

namespace ZombieModSharp.Abstractions;

public interface IWeapons
{
    public void LoadConfig(string path);
    public float GetWeaponKnockback(string weaponentity);
    public WeaponAmmo? GetWeaponAmmo(string weaponentity);
}