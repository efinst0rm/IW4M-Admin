using System;
using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore.Interfaces.Events;

namespace SharedLibraryCore.Interfaces;


public interface IPluginV2 : IModularAssembly
{
    static void RegisterDependencies(IServiceCollection serviceProvider)
    {
    }
}
