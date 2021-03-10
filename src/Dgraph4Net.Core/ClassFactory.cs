using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Dgraph4Net
{
    public static class ClassFactory
    {
        public static Dictionary<Type, Type> Proxies { get; set; } = new Dictionary<Type, Type>();

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
        public static void MapAssembly(Assembly assembly)
        {
            var iet = typeof(IEntity);
            foreach (var baseType in assembly.GetTypes().Where(tp => iet.IsAssignableFrom(tp) && !tp.IsAbstract && tp.IsClass && !tp.IsSealed))
            {
                try
                {
                    Proxies.Add(baseType, CompileResultType(baseType));
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine(ex);
                    Console.Error.WriteLine();
                    Console.Error.WriteLine();
                    Console.ResetColor();
                }
            }
        }

        public static Type GetDerivedType(Type type)
        {
            if (Proxies.Keys.Any(type.IsAssignableFrom))
                return Proxies.Keys.FirstOrDefault(type.IsAssignableFrom);

            return Proxies.ContainsKey(type) ? type : null;
        }

        public static Type MapType(Type type)
        {
            if (!Proxies.ContainsKey(type) && CompileResultType(type) is { } t)
                Proxies.Add(type, t);

            return Proxies[type];
        }

        public static T CreateNewObject<T>() where T : class, IEntity, new()
        {
            if (!Proxies.ContainsKey(typeof(T)) && CompileResultType(typeof(T)) is { } t)
                Proxies.Add(typeof(T), t);

            if (!Proxies.ContainsKey(typeof(T)))
                throw new InvalidCastException("Can't create a new type");

            if (!(Activator.CreateInstance(Proxies[typeof(T)]) is T instance))
                return null;

            var methods = instance.GetType().GetMethods();

            if (Array.Find(methods, m => m.Name == "Populate" && m.IsPublic) is { } mi)
                (mi.IsGenericMethod ? mi.MakeGenericMethod(typeof(T)) : mi).Invoke(instance, new object[] { new T() });

            return instance;
        }

        public static Type CompileResultType(Type baseType)
        {
            if (baseType is null)
                throw new ArgumentNullException(nameof(baseType));

            var tb = GetTypeBuilder(baseType);

            tb.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);

            foreach (var prop in baseType
                .GetProperties(BindingFlags.Public)
                .Where(prop => prop.CanWrite && prop.CanRead &&
                               !(prop.GetGetMethod() is null) &&
                               !(prop.GetSetMethod() is null) &&
                               !prop.GetGetMethod().IsFinal &&
                               !prop.GetSetMethod().IsFinal &&
                               !prop.GetGetMethod().IsAbstract &&
                               !prop.GetSetMethod().IsAbstract &&
                               prop.GetGetMethod().IsVirtual &&
                               prop.GetSetMethod().IsVirtual))
            {
                CreateProperty(tb, prop);
            }

            return tb.CreateTypeInfo()?.AsType();
        }

        private static TypeBuilder GetTypeBuilder(Type baseType)
        {
            var typeSignature = baseType.Name + "_Proxy";
            var an = new AssemblyName(baseType.Assembly.GetName().Name + ".Proxies");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(an, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("ProxyModule");
            return moduleBuilder.DefineType(typeSignature,
                    TypeAttributes.Public |
                    TypeAttributes.Class |
                    TypeAttributes.AutoClass |
                    TypeAttributes.AnsiClass |
                    TypeAttributes.BeforeFieldInit |
                    TypeAttributes.AutoLayout,
                    baseType);
        }

        private static void CreateProperty(TypeBuilder tb, PropertyInfo baseProperty)
        {
            var pb = tb.DefineProperty(baseProperty.Name, PropertyAttributes.None, baseProperty.PropertyType, null);

            var getter = baseProperty.GetGetMethod();

            const MethodAttributes attrs = (MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Virtual
                | MethodAttributes.Virtual) & (~MethodAttributes.Final | ~MethodAttributes.Static);

            var gets = tb.DefineMethod(getter.Name, attrs, getter.ReturnType, Type.EmptyTypes);

            var il = gets.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, getter);
            il.Emit(OpCodes.Ret);

            tb.DefineMethodOverride(gets, getter);

            var setter = baseProperty.GetSetMethod();

            var sets = tb.DefineMethod(setter.Name, attrs, null, new[] { getter.ReturnType });

            il = sets.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, setter);

            il.Emit(OpCodes.Ret);

            tb.DefineMethodOverride(sets, setter);

            pb.SetGetMethod(gets);
            pb.SetSetMethod(sets);
        }
    }
}
