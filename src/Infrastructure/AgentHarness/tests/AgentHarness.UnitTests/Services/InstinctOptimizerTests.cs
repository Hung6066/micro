using FluentAssertions;
using Moq;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Application.Services;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.UnitTests.Services;

public class InstinctOptimizerTests
{
    [Fact]
    public async Task Optimize_ShouldBoostRecentlyUsedEntries()
    {
        var store = new Mock<IStateStore>();
        var recent = MemoryEntry.Create("error A", "build", "dotnet", "fix A");
        recent.GetType().GetProperty("LastUsedAt")!.SetValue(recent, DateTime.UtcNow.AddHours(-1));
        var stale = MemoryEntry.Create("error B", "build", "dotnet", "fix B");
        stale.GetType().GetProperty("LastUsedAt")!.SetValue(stale, DateTime.UtcNow.AddDays(-30));

        store.Setup(s => s.GetMemoryEntriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry> { recent, stale });

        var optimizer = new InstinctOptimizer(store.Object);
        var result = await optimizer.OptimizeAsync(CancellationToken.None);

        result.BoostedCount.Should().Be(1);
        result.DecayedCount.Should().Be(1);
        result.MergedCount.Should().Be(0);
        result.RecordedCount.Should().Be(2); // all entries saved (boosted + decayed)
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Optimize_ShouldDecayStaleEntries()
    {
        var store = new Mock<IStateStore>();
        var oldEntry = MemoryEntry.Create("error", "runtime", "angular", "fix");
        oldEntry.GetType().GetProperty("LastUsedAt")!.SetValue(oldEntry, DateTime.UtcNow.AddDays(-14));
        // Set a starting confidence
        var confidenceProp = oldEntry.GetType().GetProperty("ConfidenceScore")!;
        confidenceProp.SetValue(oldEntry, 0.9m);

        store.Setup(s => s.GetMemoryEntriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry> { oldEntry });
        store.Setup(s => s.SaveMemoryEntryAsync(It.IsAny<MemoryEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var optimizer = new InstinctOptimizer(store.Object);
        await optimizer.OptimizeAsync(CancellationToken.None);

        // After decay, confidence should be lower
        var savedEntry = (MemoryEntry)store.Invocations
            .First(i => i.Method.Name == nameof(IStateStore.SaveMemoryEntryAsync))
            .Arguments[0];
        savedEntry.ConfidenceScore.Should().BeLessThan(0.9m);
    }

    [Fact]
    public async Task Optimize_ShouldMergeDuplicatePatterns()
    {
        var store = new Mock<IStateStore>();
        var entry1 = MemoryEntry.Create("same error", "build", "dotnet", "fix v1");
        entry1.GetType().GetProperty("ConfidenceScore")!.SetValue(entry1, 0.7m);
        entry1.GetType().GetProperty("LastUsedAt")!.SetValue(entry1, DateTime.UtcNow.AddDays(-5));

        var entry2 = MemoryEntry.Create("same error", "build", "dotnet", "fix v2");
        entry2.GetType().GetProperty("ConfidenceScore")!.SetValue(entry2, 0.9m);
        entry2.GetType().GetProperty("LastUsedAt")!.SetValue(entry2, DateTime.UtcNow.AddHours(-2));

        store.Setup(s => s.GetMemoryEntriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry> { entry1, entry2 });
        store.Setup(s => s.SaveMemoryEntryAsync(It.IsAny<MemoryEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.DeleteMemoryEntryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var optimizer = new InstinctOptimizer(store.Object);
        var result = await optimizer.OptimizeAsync(CancellationToken.None);

        result.MergedCount.Should().Be(1);
        result.RemovedCount.Should().Be(1); // duplicate was deleted
        // Survivor should have combined UseCount
        store.Verify(s => s.SaveMemoryEntryAsync(
            It.Is<MemoryEntry>(m => m.UseCount >= 2), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        // Duplicate was actually removed
        store.Verify(s => s.DeleteMemoryEntryAsync(entry2.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Optimize_ShouldNotBoostUnsuccessfulRecentEntries()
    {
        var store = new Mock<IStateStore>();
        var failedEntry = MemoryEntry.Create("error X", "build", "dotnet", "fix X", success: false);
        failedEntry.GetType().GetProperty("LastUsedAt")!.SetValue(failedEntry, DateTime.UtcNow.AddHours(-1));

        store.Setup(s => s.GetMemoryEntriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry> { failedEntry });

        var optimizer = new InstinctOptimizer(store.Object);
        var result = await optimizer.OptimizeAsync(CancellationToken.None);

        // Unsuccessful entry should NOT be boosted despite being recent
        result.BoostedCount.Should().Be(0);
    }

    [Fact]
    public async Task Optimize_ShouldDeleteMergedDuplicates()
    {
        var store = new Mock<IStateStore>(MockBehavior.Strict);
        var entry1 = MemoryEntry.Create("same error", "build", "dotnet", "fix v1");
        var entry2 = MemoryEntry.Create("same error", "build", "dotnet", "fix v2");

        store.Setup(s => s.GetMemoryEntriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry> { entry1, entry2 });
        store.Setup(s => s.SaveMemoryEntryAsync(It.IsAny<MemoryEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.DeleteMemoryEntryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var optimizer = new InstinctOptimizer(store.Object);
        var result = await optimizer.OptimizeAsync(CancellationToken.None);

        result.MergedCount.Should().Be(1);
        result.RecordedCount.Should().Be(1); // only survivor saved
        result.RemovedCount.Should().Be(1);  // duplicate deleted

        // Verify duplicate was deleted
        store.Verify(s => s.DeleteMemoryEntryAsync(entry2.Id, It.IsAny<CancellationToken>()), Times.Once);
        // Verify survivor was saved
        store.Verify(s => s.SaveMemoryEntryAsync(
            It.Is<MemoryEntry>(m => m.Id == entry1.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Optimize_ShouldPersistSurvivorAndRemoveDuplicates()
    {
        var store = new Mock<IStateStore>();
        var entry1 = MemoryEntry.Create("dup error", "runtime", "angular", "fix v1");
        var entry2 = MemoryEntry.Create("dup error", "runtime", "angular", "fix v2");

        store.Setup(s => s.GetMemoryEntriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry> { entry1, entry2 });
        store.Setup(s => s.SaveMemoryEntryAsync(It.IsAny<MemoryEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.DeleteMemoryEntryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var optimizer = new InstinctOptimizer(store.Object);
        var result = await optimizer.OptimizeAsync(CancellationToken.None);

        result.MergedCount.Should().Be(1);
        result.RemovedCount.Should().Be(1);

        // After merge, duplicate should no longer be queryable
        // Simulate: GetMemoryEntryAsync for merged entry returns null
        store.Setup(s => s.GetMemoryEntryAsync(entry2.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemoryEntry?)null);

        var queried = await store.Object.GetMemoryEntryAsync(entry2.Id);
        queried.Should().BeNull();
    }

    [Fact]
    public void BoostConfidence_ShouldIncreaseScore()
    {
        var entry = MemoryEntry.Create("error", "test", "qa", "fix");
        entry.GetType().GetProperty("ConfidenceScore")!.SetValue(entry, 0.5m);

        entry.BoostConfidence(0.2m);

        entry.ConfidenceScore.Should().Be(0.7m);
    }

    [Fact]
    public void BoostConfidence_ShouldCapAtOne()
    {
        var entry = MemoryEntry.Create("error", "test", "qa", "fix");
        entry.GetType().GetProperty("ConfidenceScore")!.SetValue(entry, 0.95m);

        entry.BoostConfidence(0.1m);

        entry.ConfidenceScore.Should().Be(1.0m);
    }

    [Fact]
    public void DecayConfidence_ShouldDecreaseScore()
    {
        var entry = MemoryEntry.Create("error", "test", "qa", "fix");
        entry.GetType().GetProperty("ConfidenceScore")!.SetValue(entry, 0.9m);

        entry.DecayConfidence(0.5m);

        entry.ConfidenceScore.Should().Be(0.45m);
    }

    [Fact]
    public void DecayConfidence_ShouldFloorAtZero()
    {
        var entry = MemoryEntry.Create("error", "test", "qa", "fix");
        entry.GetType().GetProperty("ConfidenceScore")!.SetValue(entry, 0.3m);

        entry.DecayConfidence(0.0m);

        entry.ConfidenceScore.Should().Be(0.0m);
    }

    [Fact]
    public void MergeFrom_ShouldCombineUseCountsAndKeepHigherConfidence()
    {
        var survivor = MemoryEntry.Create("error", "runtime", "dotnet", "best fix");
        survivor.GetType().GetProperty("ConfidenceScore")!.SetValue(survivor, 0.8m);
        survivor.GetType().GetProperty("UseCount")!.SetValue(survivor, 3);
        survivor.RecordHit(); // use count = 4

        var other = MemoryEntry.Create("error", "runtime", "dotnet", "alternative fix");
        other.GetType().GetProperty("ConfidenceScore")!.SetValue(other, 0.9m);
        other.GetType().GetProperty("UseCount")!.SetValue(other, 2);

        survivor.MergeFrom(other);

        survivor.UseCount.Should().Be(6); // survivor 4 + other 2
        survivor.ConfidenceScore.Should().Be(0.9m); // Keep higher confidence
        survivor.FixDescription.Should().Be("best fix"); // Keep original description
    }

    [Fact]
    public async Task Optimize_WithNoEntries_ShouldReturnZeroCounts()
    {
        var store = new Mock<IStateStore>();
        store.Setup(s => s.GetMemoryEntriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());

        var optimizer = new InstinctOptimizer(store.Object);
        var result = await optimizer.OptimizeAsync(CancellationToken.None);

        result.BoostedCount.Should().Be(0);
        result.DecayedCount.Should().Be(0);
        result.MergedCount.Should().Be(0);
        result.RecordedCount.Should().Be(0);
        result.RemovedCount.Should().Be(0);
    }

    [Fact]
    public void CreateEntry_ShouldHaveDefaultConfidence()
    {
        var entry = MemoryEntry.Create("error", "build", "dotnet", "fix description");

        entry.ConfidenceScore.Should().Be(0.85m);
    }
}
