using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.PowerShell.PSReadLine;

public static class Singletons
{
    private static Renderer __renderer;
    public static History _hs => History.Singleton;
    public static PSConsoleReadLine _rl => PSConsoleReadLine.Singleton;
    public static HistorySearcherReadLine SearcherReadLine => HistorySearcherReadLine.Singleton;

    public static Renderer _renderer
    {
        get => __renderer ??= new Renderer();
        set => __renderer = value;
    }

    public static Type[] FunctionProvider = { typeof(PSConsoleReadLine), typeof(HistorySearcherReadLine), typeof(History) };

    private static IEnumerable<MethodInfo> _bindableFunctions;
    private static IOrderedEnumerable<string> _bindableFunctionNames;

    public static IEnumerable<MethodInfo> BindableFunctions
    {
        get
        {
            if (_bindableFunctions is null)
            {
                List<MethodInfo> methods = new List<MethodInfo>();

                foreach (var type in FunctionProvider)
                {
                    methods.AddRange(type.GetMethods(BindingFlags.Public | BindingFlags.Static));
                }

                _bindableFunctions = methods.Where(method =>
                {
                    var parameters = method.GetParameters();
                    return parameters.Length == 2
                           && parameters[0].ParameterType == typeof(ConsoleKeyInfo?)
                           && parameters[1].ParameterType == typeof(object);
                });
            }

            return _bindableFunctions;
        }
    }
    public static IOrderedEnumerable<string> BindableFunctionNames
    {
        get
        {
            if (_bindableFunctionNames is null)
            {
                _bindableFunctionNames = BindableFunctions.Select(method => method.Name)
                    .OrderBy(name => name); ;
            }
            return _bindableFunctionNames;
        }
    }
}