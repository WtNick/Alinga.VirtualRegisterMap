namespace Alinga.VirtualRegisterMap;

public static class RegisterMapBuilder
{
    static readonly object Default = new ();
        
    static Dictionary<Type, object> maps = new ();
    internal static void Register(Type t, object map)
    {
        maps[t] = map;
    }
    internal static IRegisterMap<TContext> Register<TContext>(IRegisterMap<TContext> map)
    {
        Register(typeof(TContext), map);
        return map;
    }

    /// <summary>
    /// Helper function to create a map for the given type without any generics.
    /// </summary>
    /// <param name="t"></param>
    /// <returns></returns>
    internal static object GetMap(Type tcontext)
    {
        if (!maps.TryGetValue(tcontext, out var map))
        {
            var mt = typeof(ContextMapLoader<>).MakeGenericType(tcontext);
            map = mt.GetField(nameof(ContextMapLoader<int>.Map))?.GetValue(null);
            if (map == null)
            {
                // this should never happen, but is here to remove nullable warnings
                throw new Exception($"Could not load map for context type {tcontext}");
            }
            maps[tcontext] = Default;
        }
        return map;
        
    }

    /// <summary>
    /// Create a <see cref="IRegisterMap"/> from an object.
    /// The context map is only made once, so this is a very cheap operation
    /// </summary>
    /// <typeparam name="TContent"></typeparam>
    /// <param name="content"></param>
    /// <returns></returns>
    public static IRegisterMap CreateFromObject<TContent>(TContent content) => ContextMapLoader<TContent>.Map.Capture(content);
    
    /// <summary>
    /// Get the context aware register map for a given type.
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    /// <returns></returns>
    public static IRegisterMap<TContext> CreateForContext<TContext>() => ContextMapLoader<TContext>.Map;
}