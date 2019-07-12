using System.Runtime.CompilerServices;

namespace Unity.VersionControl.Git
{
    interface IAwaitable
    {
        IAwaiter GetAwaiter();
    }

    interface IAwaiter : INotifyCompletion
    {
        bool IsCompleted { get; }
        void GetResult();
    }
}
