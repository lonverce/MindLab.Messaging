using System;

namespace MindLab.Messaging.Internals
{
    class StringHashCodeGenerator : IHashCodeGenerator<string>
    {
        private readonly StringComparer m_stringComparer;

        public StringHashCodeGenerator(StringComparer stringComparer)
        {
            m_stringComparer = stringComparer;
        }

        public int GetHashCode(string item)
        {
            return m_stringComparer.GetHashCode(item);
        }
    }
}