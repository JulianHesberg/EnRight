using System.ComponentModel.DataAnnotations;

namespace Indexer.Entities;

public class Words
{
    [Key]
    public int WordId { get; set; }
    public string Word { get; set; }

    public ICollection<Occurrence> Occurrences { get; set; }
}