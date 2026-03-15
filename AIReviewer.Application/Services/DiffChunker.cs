using AIReviewer.Application.Interfaces;
using AIReviewer.Domain.Entities;

namespace AIReviewer.Application.Services;

public class DiffChunker : IDiffChunker
{
    private const int MaxLinesPerChunk = 800;

    public Task<IEnumerable<DiffChunk>> ChunkAsync(string diff)
    {
        var chunks = new List<DiffChunk>();
        var fileSections = SplitByFile(diff);

        foreach (var (fileName, content, startLine) in fileSections)
        {
            var lines = content.Split('\n');

            if (lines.Length <= MaxLinesPerChunk)
            {
                chunks.Add(new DiffChunk
                {
                    FileName = fileName,
                    Content = content,
                    StartLine = startLine,
                    EndLine = startLine + lines.Length - 1
                });
            }
            else
            {
                // Split large files into sub-chunks preserving file boundary
                for (var i = 0; i < lines.Length; i += MaxLinesPerChunk)
                {
                    var chunkLines = lines.Skip(i).Take(MaxLinesPerChunk).ToArray();
                    chunks.Add(new DiffChunk
                    {
                        FileName = fileName,
                        Content = string.Join('\n', chunkLines),
                        StartLine = startLine + i,
                        EndLine = startLine + i + chunkLines.Length - 1
                    });
                }
            }
        }

        return Task.FromResult<IEnumerable<DiffChunk>>(chunks);
    }

    private static List<(string FileName, string Content, int StartLine)> SplitByFile(string diff)
    {
        var results = new List<(string, string, int)>();
        var lines = diff.Split('\n');
        var currentFile = "unknown";
        var currentLines = new List<string>();
        var currentStart = 1;

        foreach (var line in lines)
        {
            if (line.StartsWith("diff --git"))
            {
                if (currentLines.Count > 0)
                {
                    results.Add((currentFile, string.Join('\n', currentLines), currentStart));
                    currentLines.Clear();
                }

                // Extract filename from "diff --git a/path b/path"
                var parts = line.Split(' ');
                currentFile = parts.Length >= 4
                    ? parts[3].TrimStart('b', '/').Trim()
                    : "unknown";
                currentStart = 1;
            }
            else if (line.StartsWith("@@"))
            {
                // Parse hunk header for line number: @@ -x,y +z,w @@
                var match = System.Text.RegularExpressions.Regex.Match(line, @"\+(\d+)");
                if (match.Success)
                    currentStart = int.Parse(match.Groups[1].Value);
            }

            currentLines.Add(line);
        }

        if (currentLines.Count > 0)
            results.Add((currentFile, string.Join('\n', currentLines), currentStart));

        return results;
    }
}
