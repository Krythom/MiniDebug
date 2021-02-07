using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MiniDebug.Util
{
    public static class ReflectionExtensions
    {
        private static readonly Dictionary<Type, List<FieldInfo>> FieldDict
            = new Dictionary<Type, List<FieldInfo>>();

        public static TField GetField<TField>(this Type t, string name)
            => (TField)GetFieldInternal(t, null, name);

        public static TField GetField<TType, TField>(this TType obj, string name)
            => (TField)GetFieldInternal(typeof(TType), obj, name);

        public static void SetField<TField>(this Type t, string name, TField value)
            => SetFieldInternal(t, null, name, value);

        public static void SetField<TType, TField>(this TType obj, string name, TField value)
            => SetFieldInternal(typeof(TType), obj, name, value);

        private static object GetFieldInternal(Type t, object obj, string name)
            => GetFieldInfo(t, name, obj == null).GetValue(obj);

        private static void SetFieldInternal(Type t, object obj, string name, object value)
            => GetFieldInfo(t, name, obj == null).SetValue(obj, value);

        private static FieldInfo GetFieldInfo(Type t, string name, bool isStatic)
        {
            if (t == null || name == null)
            {
                throw new ArgumentNullException();
            }

            if (!FieldDict.TryGetValue(t, out List<FieldInfo> fields))
            {
                fields = new List<FieldInfo>();
                FieldDict[t] = fields;
            }

            FieldInfo field = fields.FirstOrDefault(f => f.Name == name);
            if (field == null)
            {
                BindingFlags flags = BindingFlags.NonPublic | (isStatic
                    ? BindingFlags.Static
                    : BindingFlags.Instance);
                field = t.GetField(name, flags);

                if (field == null)
                {
                    throw new NullReferenceException($"Field '{name}' on type '{t.FullName}' not found");
                }

                fields.Add(field);
            }

            return field;
        }
    }
}
