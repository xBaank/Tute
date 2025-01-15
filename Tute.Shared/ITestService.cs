using MagicOnion;

namespace Tute.Shared
{
    public interface ITestService : IService<ITestService>
    {
        UnaryResult<int> SumAsync(int x, int y);
    }
}
