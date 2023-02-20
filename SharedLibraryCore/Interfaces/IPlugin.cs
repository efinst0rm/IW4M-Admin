using System.Threading.Tasks;

namespace SharedLibraryCore.Interfaces
{
    public interface IPlugin : IModularAssembly
    {
        new float Version { get; }
        bool IsParser => false;
        Task OnLoadAsync(IManager manager);
        Task OnUnloadAsync();
        Task OnEventAsync(GameEvent gameEvent, Server server);
        Task OnTickAsync(Server S);
    }
}
