
using System;

namespace SharedLibraryCore.Interfaces;


public interface IScriptPluginFactory
{
    IModularAssembly CreateScriptPlugin(Type type, string fileName);
}
