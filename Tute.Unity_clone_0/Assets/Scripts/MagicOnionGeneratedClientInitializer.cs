using Tute.Shared;

namespace Assets.Scripts
{
    using MagicOnion.Client;
    using Tute.Shared.GamingHub;

    [MagicOnionClientGeneration(
        typeof(ITestService),
        typeof(IGamingHubReceiver)
    )]
    internal partial class MagicOnionGeneratedClientInitializer
    { }
}
