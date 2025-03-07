using System.ComponentModel.DataAnnotations;

public class Words
{
    [Key]
    public int WordId { get; set; }
    public string Word { get; set; }

    public ICollection<Occurrence> Occurrences { get; set; }
}