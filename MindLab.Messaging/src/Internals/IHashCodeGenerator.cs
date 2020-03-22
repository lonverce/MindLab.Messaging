namespace MindLab.Messaging.Internals
{
    internal interface IHashCodeGenerator<in T>
    {
        int GetHashCode(T item);
    }
}