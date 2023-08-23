using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Alinga.VirtualRegisterMap
{

    /// <summary>
    /// Helper class for method parameter footprint detection
    /// </summary>
    internal class Extended
    {
        public interface IRegmapExtended
        {
            void Write(uint address, ReadOnlySpan<byte> data, IORequestFlags flags);
            void Read(uint address, Span<byte> data, IORequestFlags flags);
        }
        public static ParameterInfo[] ExtendedWriteParameters { get; } = Loadmethodparameters<IRegmapExtended>(nameof(IRegmapExtended.Write));
        public static ParameterInfo[] ExtendedReadParameters { get; } = Loadmethodparameters<IRegmapExtended>(nameof(IRegmapExtended.Read));

        /// <summary>
        /// Helper method to extract the parameters of a local template function
        /// </summary>
        /// <param name="methodname"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static ParameterInfo[] Loadmethodparameters<Type>(string methodname)
        {
            var mi = typeof(Type).GetMethod(methodname);
            if (mi == null)
            {
                throw new Exception($"internal error: static method {methodname} not found on type {typeof(Type)}");
            } else
            {
                return mi.GetParameters();
            }
        }
    }

    internal class CapturedDeferredContextRegisterMap<TContext, TChildContext> : IRegisterMap<TContext> 
    {
        Func<TContext, TChildContext> Fetch { get; }
        IRegisterMap<TChildContext> Map { get; }
        public CapturedDeferredContextRegisterMap(IRegisterMap<TChildContext> map, Func<TContext, TChildContext> fetch)
        {
            this.Map = map;
            this.Fetch = fetch;
        }

        public void Read(TContext context, uint offset, Span<byte> output, IORequestFlags flags) => Map.Read(Fetch(context), offset, output, flags);

        public void Write(TContext context, uint offset, ReadOnlySpan<byte> input, IORequestFlags flags) => Map.Write(Fetch(context), offset, input, flags);
    }

    /// <summary>
    /// Static helper class to create and cache context aware register map from an class definition containing properties and methods with the <see cref="RegisterAttribute"/>
    /// Access the map via the <see cref="Map"/> property
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    internal class ContextMapLoader<TContext>
    {
        static ParameterExpression contextParam { get; } = Expression.Parameter(typeof(TContext));
        static ParameterExpression addressParam { get; } = Expression.Parameter(typeof(uint));

        static ParameterExpression inputParam { get; } = Expression.Parameter(typeof(ReadOnlySpan<byte>));
        static ParameterExpression outputParam { get; } = Expression.Parameter(typeof(Span<byte>));
        static ParameterExpression flagsParam { get; } = Expression.Parameter(typeof(IORequestFlags));

        /// <summary>
        /// Default setter function for readonly members
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="address"></param>
        /// <param name="value"></param>
        static void defaultsetter<TValue>(TContext obj, uint address, TValue value) where TValue: unmanaged
        { }
        

        /// <summary>
        /// Lazy created context aware map for the <see cref="TContext"/> type
        /// </summary>
        public static IRegisterMap<TContext> Map { get; } = LoadMap(); // using lazy type loading


        /// <summary>
        /// Build a context aware register map that loads the property on demand
        /// </summary>
        /// <typeparam name="TChildType"></typeparam>
        /// <param name="childcontextproperty"></param>
        /// <returns></returns>
        public static IRegisterMap<TContext> BuildChildMap<TChildType>(PropertyInfo childcontextproperty)
        {
            var map = ContextMapLoader<TChildType>.Map;
            var fn = Expression.Lambda<Func<TContext, TChildType>>(Expression.Property(contextParam, childcontextproperty), contextParam).Compile();
            return new CapturedDeferredContextRegisterMap<TContext, TChildType>(map, fn);
        }

        static IRegisterMap<TContext> BuildRegisterHandler<TValue>(RegisterAttribute regattr, PropertyInfo pi) where TValue : unmanaged
        {
            var registertype = typeof(TValue);

            LambdaMap<TContext, TValue>.SetterFn setter = defaultsetter;
            // make setter if field is not marked readonly, property is 'CanWrite' and the setter is public
            if ((!regattr.ReadOnly) && (pi.CanWrite) && (pi.GetSetMethod()?.IsPublic ?? false))
            {
                var valueParam = Expression.Parameter(registertype);
                var propexpr = Expression.Property(contextParam, pi);

                Expression assignvalue = valueParam;

                // if the type does not match, try to convert
                if (!propexpr.Type.Equals(assignvalue.Type))
                {
                    assignvalue = Expression.Convert(assignvalue, propexpr.Type);
                }

                setter = Expression.Lambda<LambdaMap<TContext, TValue>.SetterFn>(Expression.Assign(propexpr, assignvalue), contextParam, addressParam, valueParam).Compile();
            }

            // make getter if field is not marked readonly, property is 'CanWrite' and the setter is public
            Expression getexpression;
            if ((!regattr.WriteOnly) || (pi.CanRead) && (pi.GetGetMethod()?.IsPublic ?? false))
            {
                getexpression = Expression.Property(contextParam, pi);

                // if the type does not match, try to convert
                if (!getexpression.Type.Equals(registertype))
                {
                    getexpression = Expression.Convert(getexpression, registertype);
                }
            }
            else
            {
                // get the default return value
                getexpression = Expression.Constant(Expression.Default(registertype)); // Note: since we are using generics here, we fall back to the 'default' value of the type, not of the regattr
            }

            var getter = Expression.Lambda<LambdaMap<TContext, TValue>.GetterFn>(getexpression, contextParam, addressParam).Compile();
            var map = new LambdaMap<TContext, TValue>(getter, setter);
            return map;
        }

        static IRegisterMap<TContext> BuildFieldMap(PropertyInfo pi, RegisterAttribute regattr)
        {
            // special handler in case the property is a string
            // strings are expected to be readonly and in ASCII encoding
            if (pi.PropertyType == typeof(string))
            {
                var propexpr = Expression.Property(contextParam, pi);
                var getstringfn = Expression.Lambda<Func<TContext, string>>(propexpr, contextParam).Compile();
                var stringfieldreader = new StringReader<TContext>(getstringfn);
                return stringfieldreader.ToReadOnlyMap();
            }

            // special handler in case the property is a class. 
            // In this case, the property value is loaded on demand and used in a context aware register map
            if (pi.PropertyType.IsClass)
            {
                var mi = typeof(ContextMapLoader<TContext>)?.GetMethod(nameof(BuildChildMap))?.MakeGenericMethod(pi.PropertyType);
                if (mi != null)
                {
                    if (mi.Invoke(null, new object[] { pi }) is IRegisterMap<TContext> childmap)
                    {
                        return childmap;
                    }
                }
                // this should never happen,
                throw new NullReferenceException("internal error: could not locate method");
            }

            var targettype = pi.PropertyType;
            if (pi.PropertyType.IsEnum)
            {
                targettype = Enum.GetUnderlyingType(pi.PropertyType);
            }

            switch(Type.GetTypeCode(targettype))
            {
                case TypeCode.Byte: return RegisterMapBuilder.Register(BuildRegisterHandler<byte>(regattr, pi));
                case TypeCode.SByte: return RegisterMapBuilder.Register(BuildRegisterHandler<sbyte>(regattr, pi));
                case TypeCode.Int16: return RegisterMapBuilder.Register(BuildRegisterHandler<Int16>(regattr, pi));
                case TypeCode.UInt16: return RegisterMapBuilder.Register(BuildRegisterHandler<UInt16>(regattr, pi));
                case TypeCode.Int32: return RegisterMapBuilder.Register(BuildRegisterHandler<Int32>(regattr, pi));
                case TypeCode.UInt32: return RegisterMapBuilder.Register(BuildRegisterHandler<UInt32>(regattr, pi));
                case TypeCode.Int64: return RegisterMapBuilder.Register(BuildRegisterHandler<Int64>(regattr, pi));
                case TypeCode.UInt64: return RegisterMapBuilder.Register(BuildRegisterHandler<UInt64>(regattr, pi));
                default:
                    return RegisterMapBuilder.Register(BuildRegisterHandler<UInt32>(regattr, pi));

            }

        }

        /// <summary>
        /// Create a context aware map for the <see cref="TContext"/> type
        /// <note>This is only called once, and cached in the type's static <see cref="Map"/>field</note>
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        static IRegisterMap<TContext> LoadMap()
        {
            var map = new CompositeRegisterMap<TContext>();
            var type = typeof(TContext);

            // get all public properties tagged with 'RegisterAttribute'
            foreach (var pi in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (pi.GetCustomAttribute<RegisterAttribute>() is RegisterAttribute regattr)
                {
                    var fieldlength = regattr.Length;
                    if (fieldlength == 0)
                    {
                        throw new ArgumentException("Register field length should be >0");
                    }

                    map.Insert(regattr.Address, fieldlength, BuildFieldMap(pi, regattr));
                }
            }

            // get all public methods tagged with 'RegisterAttribute'
            foreach(var mi in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (mi.GetCustomAttribute<RegisterAttribute>() is RegisterAttribute regattr)
                {
                    var parameters = mi.GetParameters();
                    if (IsMatch(parameters, Extended.ExtendedWriteParameters))
                    {
                        //
                        // Extended write (uint address, ReadOnlySpan<byte> data, IORequestFlags flags)
                        //
                        var setter = Expression.Lambda<LambdaWriter<TContext>.SetterFn>(Expression.Call(contextParam, mi, addressParam, inputParam, flagsParam), contextParam, addressParam, inputParam, flagsParam).Compile();

                        map.Insert(regattr.Address, regattr.Length, new LambdaWriter<TContext>(setter));
                        continue;
                    }

                    if (IsMatch(parameters, Extended.ExtendedReadParameters))
                    {
                        //
                        // Extended read (uint address, Span<byte> data, IORequestFlags flags)
                        //
                        var getter = Expression.Lambda<LambdaReader<TContext>.GetterFn>(Expression.Call(contextParam, mi, addressParam, outputParam, flagsParam), contextParam, addressParam, outputParam, flagsParam).Compile();
                        map.Insert(regattr.Address, regattr.Length, new LambdaReader<TContext>(getter));

                        continue;
                    }
                    
                    
                    if (parameters.Length != 1)
                    {
                        throw new Exception("Invalid method signature. Callable method must have 1 parameter");
                    }

                    if (parameters[0].ParameterType == typeof(UInt32))
                    {
                        // uint32 register write
                        map.Insert(regattr.Address, regattr.Length, CreateMethodMap<UInt32>(regattr.DefaultValue, mi));
                    }
                    if (parameters[0].ParameterType == typeof(int))
                    {
                        // uint32 register write
                        map.Insert(regattr.Address, regattr.Length, CreateMethodMap<int>(regattr.DefaultValue, mi));
                    }
                    if (parameters[0].ParameterType == typeof(byte))
                    {
                        // uint32 register write
                        map.Insert(regattr.Address, regattr.Length, CreateMethodMap<byte>(regattr.DefaultValue, mi));
                    }


                }
            }
            return map;
        }

        /// <summary>
        /// Convert uint32 to generic type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        static T Convert<T>(UInt32 value) where T : unmanaged
        {
            var result = default(T);
            var src = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1));
            var dst = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref result, 1));
            if (src.Length > dst.Length)
            {
                src[..dst.Length].CopyTo(dst);
            } else
            {
                src.CopyTo(dst);
            }

            return result;
        }

        static IRegisterWrite<TContext> CreateMethodMap<TParameter>(UInt32 returnvalue, MethodInfo setter_mi) where TParameter:unmanaged
        {
            var valueParam = Expression.Parameter(typeof(TParameter));

            var setter = Expression.Lambda<LambdaMap<TContext, TParameter>.SetterFn>(Expression.Call(contextParam, setter_mi, valueParam), contextParam, addressParam, valueParam).Compile();
            var getter = Expression.Lambda<LambdaMap<TContext, TParameter>.GetterFn>(Expression.Constant(Convert<TParameter>(returnvalue)), contextParam, addressParam).Compile();
            return new LambdaMap<TContext, TParameter>(getter, setter);
        }

        static bool IsMatch(ParameterInfo p1, ParameterInfo p2)
        {
            if (p1.ParameterType != p2.ParameterType) return false;
            if (p1.IsOut != p2.IsOut) return false;
            if (p1.IsIn != p2.IsIn) return false;
            return true;
        }

        static bool IsMatch(ParameterInfo[] p1, ParameterInfo[] p2)
        {
            if (p1.Length!=p2.Length) return false;
            return p1.Zip(p2).All(t=>IsMatch(t.First,t.Second));
        }
    }
}