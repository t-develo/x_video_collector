# TDD

Enforce test-driven development with the Red-Green-Refactor cycle.

## Cycle

```
RED → GREEN → REFACTOR → VERIFY
```

## Process

1. **RED** — Write a failing test first
   - Name: `MethodName_Condition_ExpectedResult`
   - Use AAA (Arrange-Act-Assert) with blank lines between sections
   - Run: `dotnet test --filter "FullyQualifiedName~{TestName}"` — confirm it fails

2. **GREEN** — Implement minimal code to pass
   - Write only what's needed for the test to pass
   - Run: `dotnet test --filter "FullyQualifiedName~{TestName}"` — confirm it passes

3. **REFACTOR** — Improve while keeping tests green
   - Apply project conventions (sealed, file-scoped namespace, primary constructor)
   - Run: `dotnet test` — confirm all tests still pass

4. **VERIFY** — Check coverage and quality
   - Run full test suite: `dotnet test`
   - Check for boundary tests: 0 items, 1 item, pageSize items

## Edge Cases to Always Cover

For every feature, include tests for:
- Empty collection / null input
- Single item
- Exactly `pageSize` items (pagination boundary)
- Duplicate detection (same TweetUrl registered twice)
- Authorization (user can only access own data)

## C# Template

```csharp
public class RegisterVideoUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenUrlIsValid_ShouldAddVideoAndSaveChanges()
    {
        // Arrange
        var mockRepo = new Mock<IVideoRepository>();
        var mockUow = new Mock<IUnitOfWork>();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var sut = new RegisterVideoUseCase(mockRepo.Object, mockUow.Object, timeProvider);

        // Act
        var result = await sut.ExecuteAsync(new RegisterVideoCommand("https://x.com/user/status/123"));

        // Assert
        mockRepo.Verify(r => r.AddAsync(It.IsAny<Video>()), Times.Once);
        mockUow.Verify(u => u.SaveChangesAsync(default), Times.Once);
        Assert.NotEqual(Guid.Empty, result.VideoId);
    }
}
```
