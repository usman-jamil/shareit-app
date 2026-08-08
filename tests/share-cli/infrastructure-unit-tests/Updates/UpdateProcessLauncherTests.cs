using Share.Infrastructure.Updates;
using SharedKernel;
using Shouldly;
using Xunit;

namespace Share.Infrastructure.UnitTests.Updates;

/// <summary>
/// Covers the waiting half of the launcher. Starting is deliberately not exercised here:
/// it clones whatever executable the current process is running from and runs it, which in
/// a test host means spawning the test runner again. What that would prove is covered
/// instead by publishing the CLI and running a real update.
/// </summary>
public sealed class UpdateProcessLauncherTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(250);

    private readonly UpdateProcessLauncher _launcher =
        new(new StubApplicationEnvironment());

    [Fact]
    public async Task WaitForExitAsync_Should_Succeed_WhenThereIsNothingToWaitFor()
    {
        // Zero is what `update-apply` is given when it is run by hand rather than by an
        // update, and it must not block or fail.
        Result result = await _launcher.WaitForExitAsync(
            0,
            Timeout,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task WaitForExitAsync_Should_Succeed_WhenTheProcessHasAlreadyGone()
    {
        // The normal case: the process that started the updater was on its way out as it
        // did so, and is usually gone before the updater gets here.
        Result result = await _launcher.WaitForExitAsync(
            int.MaxValue,
            Timeout,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task WaitForExitAsync_Should_Fail_WhenTheProcessOutlastsTheTimeout()
    {
        // This process is not going anywhere, which is exactly the situation the updater
        // must refuse to replace a binary in.
        Result result = await _launcher.WaitForExitAsync(
            Environment.ProcessId,
            Timeout,
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.CallerStillRunning");
        result.Error.Description.ShouldContain(
            Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
