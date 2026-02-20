using BPOPlatform.Application.Processes.Commands;
using FluentAssertions;

namespace BPOPlatform.UnitTests.Application;

/// <summary>Tests for FluentValidation validators on commands.</summary>
public class CommandValidatorTests
{
    // ── CreateProcessCommandValidator ─────────────────────────────────────────

    [Fact]
    public void CreateProcess_ValidCommand_PassesValidation()
    {
        var validator = new CreateProcessCommandValidator();
        var result = validator.Validate(new CreateProcessCommand("Invoice AP", "desc", "Finance", "user-1"));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "desc", "Finance", "user-1")]      // empty name
    [InlineData("Name", "desc", "", "user-1")]          // empty department
    [InlineData("Name", "desc", "Finance", "")]         // empty ownerId
    public void CreateProcess_InvalidCommand_FailsValidation(string name, string desc, string dept, string owner)
    {
        var validator = new CreateProcessCommandValidator();
        var result = validator.Validate(new CreateProcessCommand(name, desc, dept, owner));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateProcess_NameExceedsMaxLength_FailsValidation()
    {
        var validator = new CreateProcessCommandValidator();
        var longName = new string('A', 201);
        var result = validator.Validate(new CreateProcessCommand(longName, "desc", "Finance", "user-1"));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateProcess_DescriptionExceedsMaxLength_FailsValidation()
    {
        var validator = new CreateProcessCommandValidator();
        var longDesc = new string('D', 2001);
        var result = validator.Validate(new CreateProcessCommand("Name", longDesc, "Finance", "user-1"));
        result.IsValid.Should().BeFalse();
    }

    // ── UpdateProcessCommandValidator ─────────────────────────────────────────

    [Fact]
    public void UpdateProcess_ValidCommand_PassesValidation()
    {
        var validator = new UpdateProcessCommandValidator();
        var result = validator.Validate(new UpdateProcessCommand(Guid.NewGuid(), "Name", "desc", "Finance"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateProcess_EmptyId_FailsValidation()
    {
        var validator = new UpdateProcessCommandValidator();
        var result = validator.Validate(new UpdateProcessCommand(Guid.Empty, "Name", "desc", "Finance"));
        result.IsValid.Should().BeFalse();
    }
}
