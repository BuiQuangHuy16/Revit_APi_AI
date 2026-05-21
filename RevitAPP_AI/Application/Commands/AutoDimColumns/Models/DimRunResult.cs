using System.Collections.Generic;

namespace Aplication.Commands.AutoDimColumns.Models
{
    public class DimRunResult
    {
        public int DimensionsCreated { get; set; }
        public int ColumnsProcessed { get; set; }
        public int ColumnsSkipped { get; set; }
        public int OldDimensionsDeleted { get; set; }
        public List<string> SkipReasons { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();
    }
}
