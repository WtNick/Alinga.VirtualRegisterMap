using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

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

    /// <summary>
    /// Create for a non generic object for an explicit type.
    /// </summary>
    /// <param name="t">target type for the map. this must be compatible with the object</param>
    /// <param name="obj">object captured in the map</param>
    /// <returns></returns>
    public static IRegisterMap CreateFromObject(Type t, object obj) => NonGenericMapBuildHelper.Build(t, obj);

    static class NonGenericMapBuildHelper
    {
        class EmptyRegmap : IRegisterMap
        {
            public static IRegisterMap Singleton { get; } = new EmptyRegmap();
            private EmptyRegmap() { }
            public void Read(uint offset, Span<byte> output, IORequestFlags flags) { }
            public void Write(uint offset, ReadOnlySpan<byte> input, IORequestFlags flags) { }
        }

        class Internal {
            public static IRegisterMap CreateMap<T>(object value)
            {
                if (value is T castvalue)
                {
                    return CreateForContext<T>().Capture(castvalue);
                }
                return EmptyRegmap.Singleton;
            }
        }

        static MethodInfo createmap_mi = typeof(Internal).GetMethod(nameof(Internal.CreateMap), BindingFlags.Static | BindingFlags.Public);

        static ConcurrentDictionary<Type, Func<object, IRegisterMap>> activation_cache { get; } = new();
        public static IRegisterMap Build(Type type, object obj)
        {
            var fn = activation_cache.GetOrAdd(type, (t) =>
            {
                var p_obj = Expression.Parameter(typeof(object));
                var fn = Expression.Lambda<Func<object, IRegisterMap>>(Expression.Call(null, createmap_mi.MakeGenericMethod(type), p_obj), p_obj).Compile();
                return fn;
            });
            return fn(obj);
        }
    }
}