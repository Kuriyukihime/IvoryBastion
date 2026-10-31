using System;
using System.Reflection;
using EntityStates;
namespace IvoryBastion
{
    internal static class BastionStateType
    {
        private static readonly FieldInfo cachedType=typeof(SerializableEntityStateType).GetField("_stateType",BindingFlags.Instance|BindingFlags.NonPublic);
        internal static SerializableEntityStateType For<T>() where T:EntityState=>For(typeof(T));
        internal static SerializableEntityStateType For(Type type)
        {
            if(type==null||!type.IsSubclassOf(typeof(EntityState)))throw new ArgumentException("An EntityState subclass is required.",nameof(type));
            // Unity/Mono can resolve an assembly-qualified name against the
            // first loaded DLL even when its version differs. Preserve the
            // actual registered Type in the native struct's existing cache.
            object state=new SerializableEntityStateType(type);
            cachedType?.SetValue(state,type);
            return (SerializableEntityStateType)state;
        }
    }
}
