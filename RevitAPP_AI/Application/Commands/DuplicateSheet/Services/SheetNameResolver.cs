using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Aplication.Commands.DuplicateSheet.Services
{
    public class SheetNameResolver
    {
        private readonly HashSet<string> _existingNumbers;

        public SheetNameResolver(Document doc)
        {
            _existingNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>();

            foreach (var sheet in sheets)
            {
                if (!string.IsNullOrEmpty(sheet.SheetNumber))
                    _existingNumbers.Add(sheet.SheetNumber);
            }
        }

        public string ResolveUniqueNumber(string originalNumber)
        {
            for (var i = 1; i < 10000; i++)
            {
                var candidate = $"{originalNumber} Copy {i}";
                if (_existingNumbers.Contains(candidate)) continue;
                _existingNumbers.Add(candidate);
                return candidate;
            }

            throw new InvalidOperationException(
                $"Cannot resolve unique sheet number for '{originalNumber}' after 10000 attempts.");
        }
    }
}
