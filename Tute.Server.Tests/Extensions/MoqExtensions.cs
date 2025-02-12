using System.Diagnostics;
using System.Linq.Expressions;
using Moq;

namespace Tute.Server.Tests.Extensions;

internal static class MoqExtensions
{
    public static async Task SomeVerifyWithTimeout(
        Action action,
        TimeSpan timeout,
        CancellationToken token = default
    )
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();
        Exception? verificationException = null;

        while (stopWatch.Elapsed < timeout)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                action();
                return;
            }
            catch (MockException mockException)
            {
                verificationException = mockException;
                await Task.Yield();
            }
        }
        throw new TimeoutException(
            $"Verification expression always failed within given timespan of {timeout}.",
            verificationException
        );
    }

    public static async Task AsyncVerify<T>(
        this Mock<T> mock,
        Expression<Action<T>> expression,
        Times times,
        TimeSpan timeout,
        CancellationToken token = default
    )
        where T : class =>
        await SomeVerifyWithTimeout(() => mock.Verify(expression, times), timeout, token);

    public static async Task AsyncVerify<T, TExpressionResult>(
        this Mock<T> mock,
        Expression<Func<T, TExpressionResult>> expression,
        Times times,
        TimeSpan timeout,
        CancellationToken token = default
    )
        where T : class =>
        await SomeVerifyWithTimeout(() => mock.Verify(expression, times), timeout, token);
}
