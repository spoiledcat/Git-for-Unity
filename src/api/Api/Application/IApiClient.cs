using System;

namespace Unity.VersionControl.Git
{
    public interface IApiClient
    {
        HostAddress HostAddress { get; }
    }
}
