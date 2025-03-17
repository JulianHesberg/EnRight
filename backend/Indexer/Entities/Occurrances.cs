namespace Indexer.Entities;

public class Occurrence
{
    public int WordId { get; set; }
    public Words Word { get; set; }

    public int FileId { get; set; }
    public FileRecord File { get; set; }

    public int Count { get; set; } // Number of times this word appears in this file
}
