using MagicOnion;
using MagicOnion.Server;
using Tute.Shared;

namespace Tute.Server
{
    public class TestService : ServiceBase<ITestService>, ITestService
    {
        public async UnaryResult<int> SumAsync(int x, int y)
        {
            Console.WriteLine($"Received:{x}, {y}");
            return x + y;
        }
    }
}
