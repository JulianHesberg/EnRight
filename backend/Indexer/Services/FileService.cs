using Indexer.Models;
using Microsoft.EntityFrameworkCore;

namespace Indexer.Services;

public class FileService
{
    private readonly IndexerContext _context;

    public FileService(IndexerContext context)
    {
        _context = context;
    }
    public async Task< List<FileSearchResult>> GetTop20Files(string searchQuery)
    {
        // Split the searchQuery into individual words
        var searchWords = searchQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var results = await (from word in _context.Words
                where searchWords.Contains(word.Word) // Match words in the searchWords array
                join occurrence in _context.Occurrences on word.WordId equals occurrence.WordId
                join file in _context.Files on occurrence.FileId equals file.FileId
                group occurrence by new { file.FileId, file.FileName, file.Content } into fileGroup
                select new
                {
                    FileId = fileGroup.Key.FileId,
                    FileName = fileGroup.Key.FileName,
                    Content = fileGroup.Key.Content,
                    OccurrenceSum = fileGroup.Sum(o => o.Count) // Calculate the sum of occurrences
                })
            .OrderByDescending(result => result.OccurrenceSum) // Sort by OccurrenceSum in descending order
            .Take(20) // Limit to the top 20 files
            .Select(x => new FileSearchResult
            {
                FileId = x.FileId,
                FileName = x.FileName,
                Content = x.Content,
                OccurrenceSum = x.OccurrenceSum
            })
            .ToListAsync();

        return results;
    }
}