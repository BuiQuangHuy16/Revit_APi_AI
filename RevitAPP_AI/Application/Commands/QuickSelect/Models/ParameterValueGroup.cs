using System.Collections.Generic;

namespace Aplication.Commands.QuickSelect.Models
{
    public class ParameterValueGroup
    {
        public string ParameterName { get; set; }
        public bool IsTypeParameter { get; set; }
        public List<ValueBucket> Values { get; } = new List<ValueBucket>();
    }
}
