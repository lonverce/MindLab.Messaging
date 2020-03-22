using System;

namespace MindLab.Messaging.Internals
{
    class DelegateHashCodeGenerator<TDelegate> : IHashCodeGenerator<TDelegate>
        where TDelegate: Delegate
    {
        public int GetHashCode(TDelegate item)
        {
            return item == null ? 0 : item.GetHashCode();
        }

        public static DelegateHashCodeGenerator<TDelegate> Default { get; } = new DelegateHashCodeGenerator<TDelegate>();
    }
}