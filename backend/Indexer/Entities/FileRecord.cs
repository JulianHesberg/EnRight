using System.ComponentModel.DataAnnotations;

namespace Indexer.Entities;

public class FileRecord {
    [Key]
    public int FileId {get; set;}
    public string FileName {get; set;}
    public byte[] Content {get; set;}

    public ICollection<Occurrence> Occurrences {get; set;}
}