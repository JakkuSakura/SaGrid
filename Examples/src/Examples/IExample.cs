using System;
using Avalonia.Controls;

namespace Examples.Examples;

public interface IExample
{
    string Name { get; }
    string Description { get; }
    ExampleHost Create();
}

public sealed record ExampleHost(Control Content, Action? Cleanup = null);
