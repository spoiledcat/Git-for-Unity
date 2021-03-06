using System;
using System.Threading.Tasks;
using FluentAssertions;
using Unity.VersionControl.Git;
using NUnit.Framework;

namespace IntegrationTests
{
    [TestFixture]
    class A_GitClientTests : BaseGitTestWithHttpServer
    {
        protected override int Timeout { get; set; } = 5 * 60 * 1000;

        [Test]
        public void AaSetupGitFirst()
        {
            InitializePlatformAndEnvironment(TestRepoMasterCleanSynchronized);
        }

        [Test]
        public void ShouldGetGitVersion()
        {
            if (!DefaultEnvironment.OnWindows)
                return;

            InitializePlatformAndEnvironment(TestRepoMasterCleanSynchronized);

            var result = GitClient.Version().RunSynchronously();
            var expected = TheVersion.Parse("2.21.0");
            result.Major.Should().Be(expected.Major);
            result.Minor.Should().Be(expected.Minor);
            result.Patch.Should().Be(expected.Patch);
        }

        [Test]
        public void ShouldGetGitLfsVersion()
        {
            if (!DefaultEnvironment.OnWindows)
                return;

            InitializePlatformAndEnvironment(TestRepoMasterCleanSynchronized);

            var result = GitClient.LfsVersion().RunSynchronously();
            var expected = TheVersion.Parse("2.6.1");
            result.Should().Be(expected);
        }
    }
}
