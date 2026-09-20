using LH.Main.Unity.Networking;
using NUnit.Framework;

public sealed class MovementValidatorTests
{
    [Test]
    public void ValidateAcceptsNormalInputAndAdvancesState()
    {
        var state = MovementState.Initial;
        var command = new MovementCommand(1, 1f, 0f, 1d);

        MovementValidationResult result = MovementValidator.Validate(command, state, 0.25f);

        Assert.That(result.Accepted, Is.True);
        Assert.That(result.Reason, Is.EqualTo(string.Empty));
        Assert.That(result.NextState.LastSequence, Is.EqualTo(1u));
        Assert.That(result.NextState.PositionX, Is.GreaterThan(0f));
        Assert.That(result.NextState.PositionY, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void ValidateRejectsNonFiniteAxis()
    {
        var command = new MovementCommand(1, float.NaN, 0f, 1d);

        MovementValidationResult result = MovementValidator.Validate(command, MovementState.Initial, 0.25f);

        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Reason, Is.EqualTo("axis_non_finite"));
    }

    [Test]
    public void ValidateRejectsOutOfRangeAxis()
    {
        var command = new MovementCommand(1, 1.01f, 0f, 1d);

        MovementValidationResult result = MovementValidator.Validate(command, MovementState.Initial, 0.25f);

        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Reason, Is.EqualTo("axis_out_of_range"));
    }

    [Test]
    public void ValidateRejectsDuplicateSequence()
    {
        var state = MovementState.Initial.WithLastSequence(7);
        var command = new MovementCommand(7, 1f, 0f, 1d);

        MovementValidationResult result = MovementValidator.Validate(command, state, 0.25f);

        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Reason, Is.EqualTo("sequence_not_newer"));
    }

    [Test]
    public void ValidateRejectsExcessiveCommandRate()
    {
        var state = MovementState.Initial.WithTiming(1, 10d);
        var command = new MovementCommand(2, 1f, 0f, 100d);

        MovementValidationResult result = MovementValidator.Validate(command, state, 0.01f);
        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Reason, Is.EqualTo("command_rate_exceeded"));
    }

    [Test]
    public void ValidateAcceptsFirstCommandFromPositionedInitialStateNearClientTimeZero()
    {
        var state = MovementState.InitialAt(3f, 4f);
        var command = new MovementCommand(1, 1f, 0f, 0.01d);

        MovementValidationResult result = MovementValidator.Validate(command, state, 0.25f);

        Assert.That(result.Accepted, Is.True);
        Assert.That(result.NextState.LastSequence, Is.EqualTo(1u));
        Assert.That(result.NextState.PositionX, Is.GreaterThan(3f));
        Assert.That(result.NextState.PositionY, Is.EqualTo(4f).Within(0.0001f));
    }

    [Test]
    public void ValidateClampsVelocityToSpeedLimit()
    {
        var state = new MovementState(0f, 0f, 100f, 0f, 1, 1d);
        var command = new MovementCommand(2, 1f, 0f, 2d);

        MovementValidationResult result = MovementValidator.Validate(command, state, 1f);

        Assert.That(result.Accepted, Is.True);
        Assert.That(result.NextState.VelocityX, Is.EqualTo(MovementValidator.MaxSpeed).Within(0.0001f));
        Assert.That(result.NextState.VelocityY, Is.EqualTo(0f).Within(0.0001f));
    }
}
