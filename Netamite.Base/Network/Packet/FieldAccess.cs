using Netamite.Logging;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Netamite.Network.Packet {
    /// <summary>
    /// Getters, setters and constructors for packet fields.
    ///
    /// FieldInfo.GetValue / SetValue box every value and are slow, so where the runtime has a JIT (Mono, .NET) the
    /// accessors get compiled once into real code. On AOT runtimes (Unity IL2CPP) compiling isn't possible, there
    /// the same accessors just go through reflection.
    /// </summary>
    internal static class FieldAccess {
        private static readonly bool compile = CanCompile();

        /// <summary>
        /// False falls back to plain reflection, even where compiling would work
        /// </summary>
        internal static bool UseCompiledAccessors = true;

        /// <summary>
        /// Is the runtime able to turn an expression into machine code?
        /// Runtimes without a JIT either throw or run the expression in an interpreter, which is slower than reflection.
        /// </summary>
        private static bool CanCompile() {
            try {
                ParameterExpression x = Expression.Parameter(typeof(int), "x");
                Func<int, int> probe = Expression.Lambda<Func<int, int>>(Expression.Add(x, Expression.Constant(1)), x).Compile();
                if(probe(41) != 42) return false;

                object target = probe.Target;
                if(target != null && target.GetType().FullName.IndexOf("Interpret", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                return true;
            } catch(Exception) {
                return false;
            }
        }

        public static bool IsCompiling { get { return compile && UseCompiledAccessors; } }

        /// <summary>
        /// Reads the field of an object. T is the field type, or the underlying type if the field is an enum.
        /// </summary>
        public static Func<object, T> Getter<T>(FieldInfo field) {
            if(IsCompiling && CanCompileFor(field)) {
                try {
                    ParameterExpression target = Expression.Parameter(typeof(object), "target");
                    Expression value = Expression.Field(Expression.Convert(target, field.DeclaringType), field);
                    if(field.FieldType != typeof(T)) value = Expression.Convert(value, typeof(T));
                    return Expression.Lambda<Func<object, T>>(value, target).Compile();
                } catch(Exception e) {
                    Log.Debug(Defines.NETAMITE, $"Using reflection to read {field.DeclaringType}.{field.Name}: {e.Message}");
                }
            }
            return target => (T) field.GetValue(target);
        }

        /// <summary>
        /// Writes the field of an object. T is the field type, or the underlying type if the field is an enum.
        /// </summary>
        public static Action<object, T> Setter<T>(FieldInfo field) {
            if(IsCompiling && CanCompileFor(field) && !field.IsInitOnly) {
                try {
                    ParameterExpression target = Expression.Parameter(typeof(object), "target");
                    ParameterExpression value = Expression.Parameter(typeof(T), "value");
                    Expression converted = field.FieldType != typeof(T) ? (Expression) Expression.Convert(value, field.FieldType) : value;
                    Expression assign = Expression.Assign(Expression.Field(Expression.Convert(target, field.DeclaringType), field), converted);
                    return Expression.Lambda<Action<object, T>>(assign, target, value).Compile();
                } catch(Exception e) {
                    Log.Debug(Defines.NETAMITE, $"Using reflection to write {field.DeclaringType}.{field.Name}: {e.Message}");
                }
            }
            return (target, value) => field.SetValue(target, value);
        }

        /// <summary>
        /// Creates objects of the type with its parameterless constructor
        /// </summary>
        public static Func<object> Constructor(Type type) {
            if(IsCompiling && !type.IsValueType && !type.IsAbstract && type.IsVisible) {
                try {
                    ConstructorInfo constructor = type.GetConstructor(Type.EmptyTypes);
                    if(constructor != null) {
                        return Expression.Lambda<Func<object>>(Expression.New(constructor)).Compile();
                    }
                } catch(Exception e) {
                    Log.Debug(Defines.NETAMITE, $"Using reflection to create {type}: {e.Message}");
                }
            }
            return () => Activator.CreateInstance(type);
        }

        private static bool CanCompileFor(FieldInfo field) {
            // Structs are handled as boxed objects, a compiled accessor would only change a copy of them
            if(field.IsStatic || field.DeclaringType.IsValueType || field.DeclaringType.ContainsGenericParameters) return false;

            // Compiled code has to follow the same access rules as any other code. Reflection doesn't,
            // so everything that isn't public stays with reflection instead of failing on first use.
            return field.IsPublic && field.DeclaringType.IsVisible && field.FieldType.IsVisible;
        }
    }
}
