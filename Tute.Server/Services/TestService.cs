using MagicOnion;
using MagicOnion.Server;

using Tute.Shared;

namespace Tute.Server.Services;

public class TestService : ServiceBase<ITestService>, ITestService
{
    public async UnaryResult<int> SumAsync(int x, int y)
    {
        return x + y;
    }
}
