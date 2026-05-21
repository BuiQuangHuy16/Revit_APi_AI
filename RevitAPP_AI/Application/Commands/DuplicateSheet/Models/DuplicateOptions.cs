namespace Aplication.Commands.DuplicateSheet.Models
{
    public class DuplicateOptions
    {
        public int CopiesPerSheet { get; set; } = 1;
        public bool DuplicateLegends { get; set; }
        public bool DuplicateSchedules { get; set; }
    }
}
