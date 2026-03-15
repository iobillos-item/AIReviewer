using AIReviewer.Application.Services;
using Xunit;

namespace AIReviewer.Application.Tests.Services;

public class DiffChunkerTests
{
    private readonly DiffChunker _sut = new();

    [Fact]
    public async Task ChunkAsync_SingleFile_ReturnsSingleChunk()
    {
        var diff = "diff --git a/src/Service.cs b/src/Service.cs\n@@ -1,5 +10,5 @@\n+ public void DoWork() { }";

        var chunks = (await _sut.ChunkAsync(diff)).ToList();

        Assert.Single(chunks);
        Assert.Equal("src/Service.cs", chunks[0].FileName);
        Assert.Equal(10, chunks[0].StartLine);
    }

    [Fact]
    public async Task ChunkAsync_MultipleFiles_ReturnsChunkPerFile()
    {
        var diff = "diff --git a/File1.cs b/File1.cs\n@@ -1,3 +1,3 @@\n+ line1\ndiff --git a/File2.cs b/File2.cs\n@@ -1,3 +5,3 @@\n+ line2";

        var chunks = (await _sut.ChunkAsync(diff)).ToList();

        Assert.Equal(2, chunks.Count);
        Assert.Equal("File1.cs", chunks[0].FileName);
        Assert.Equal("File2.cs", chunks[1].FileName);
    }

    [Fact]
    public async Task ChunkAsync_LargeFile_SplitsIntoMultipleChunks()
    {
        var lines = Enumerable.Range(1, 1200).Select(i => $"+ line {i}");
        var diff = $"diff --git a/Big.cs b/Big.cs\n@@ -1,0 +1,1200 @@\n{string.Join('\n', lines)}";

        var chunks = (await _sut.ChunkAsync(diff)).ToList();

        Assert.True(chunks.Count >= 2);
        Assert.All(chunks, c => Assert.Equal("Big.cs", c.FileName));
    }

    [Fact]
    public async Task ChunkAsync_EmptyDiff_ReturnsSingleUnknownChunk()
    {
        var chunks = (await _sut.ChunkAsync(string.Empty)).ToList();

        Assert.Single(chunks);
        Assert.Equal("unknown", chunks[0].FileName);
    }

    [Fact]
    public async Task ChunkAsync_PreservesFileMetadata()
    {
        var diff = "diff --git a/src/App.cs b/src/App.cs\n@@ -5,3 +20,3 @@\n+ new code";

        var chunks = (await _sut.ChunkAsync(diff)).ToList();

        Assert.Single(chunks);
        Assert.Equal("src/App.cs", chunks[0].FileName);
        Assert.Equal(20, chunks[0].StartLine);
    }

    [Fact]
    public async Task ChunkAsync_LargeFile_SubChunksHaveCorrectStartLines()
    {
        var lines = Enumerable.Range(1, 1600).Select(i => $"+ line {i}");
        var diff = $"diff --git a/Big.cs b/Big.cs\n@@ -1,0 +1,1600 @@\n{string.Join('\n', lines)}";

        var chunks = (await _sut.ChunkAsync(diff)).ToList();

        // First chunk starts at 1, second at 801
        Assert.Equal(1, chunks[0].StartLine);
        Assert.True(chunks[1].StartLine > chunks[0].StartLine);
    }

    [Fact]
    public async Task ChunkAsync_NoDiffHeaders_TreatsAsUnknownFile()
    {
        var diff = "+ some random line\n+ another line";

        var chunks = (await _sut.ChunkAsync(diff)).ToList();

        Assert.Single(chunks);
        Assert.Equal("unknown", chunks[0].FileName);
    }

    [Fact]
    public async Task ChunkAsync_IsDeterministic()
    {
        var diff = "diff --git a/A.cs b/A.cs\n@@ -1,3 +1,3 @@\n+ code";

        var result1 = (await _sut.ChunkAsync(diff)).ToList();
        var result2 = (await _sut.ChunkAsync(diff)).ToList();

        Assert.Equal(result1.Count, result2.Count);
        Assert.Equal(result1[0].FileName, result2[0].FileName);
        Assert.Equal(result1[0].Content, result2[0].Content);
        Assert.Equal(result1[0].StartLine, result2[0].StartLine);
    }
}
