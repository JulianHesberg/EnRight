namespace Indexer.Models;

public class FileSearchResult
{
    public int FileId { get; set; }
    public string FileName { get; set; }
    public byte[] Content { get; set; }
    public int OccurrenceSum { get; set; }
}