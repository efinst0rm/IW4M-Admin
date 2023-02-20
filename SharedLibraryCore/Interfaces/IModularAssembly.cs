﻿namespace SharedLibraryCore.Interfaces;

public interface IModularAssembly
{
    string Name { get; }
    string Author { get; }
    string Version => string.Empty;
    string Scope => string.Empty;
    string Role => string.Empty;
    string[] Claims => System.Array.Empty<string>();
}
