using Sharp.Shared.Objects;

namespace ZombieModSharp.Abstractions;

public interface ICommand
{
    public void PostInit();
    public void ReplyToCommand(IGameClient client, string text);
}