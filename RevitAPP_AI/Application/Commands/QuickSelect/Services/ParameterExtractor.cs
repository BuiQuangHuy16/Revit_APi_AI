using System.Collections.Generic;
using System.Linq;
using Aplication.Commands.QuickSelect.Models;
using Autodesk.Revit.DB;

namespace Aplication.Commands.QuickSelect.Services
{
    internal static class ParameterExtractor
    {
        private static readonly HashSet<BuiltInParameter> Denylist = new HashSet<BuiltInParameter>
        {
            BuiltInParameter.ID_PARAM,
            BuiltInParameter.SYMBOL_ID_PARAM,
            BuiltInParameter.ELEM_TYPE_PARAM,
            BuiltInParameter.ELEM_CATEGORY_PARAM,
            BuiltInParameter.ELEM_CATEGORY_PARAM_MT,
            BuiltInParameter.ELEM_FAMILY_PARAM,
            BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM,
            BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM,
            BuiltInParameter.SYMBOL_FAMILY_AND_TYPE_NAMES_PARAM,
            BuiltInParameter.SYMBOL_NAME_PARAM,
            BuiltInParameter.HOST_ID_PARAM
        };

        public static IReadOnlyList<ParameterValueGroup> Run(Document doc, ICollection<ElementId> ids)
        {
            var byKey = new Dictionary<string, ParameterValueGroup>();

            foreach (var id in ids)
            {
                var element = doc.GetElement(id);
                if (element == null) continue;

                AddMeta(byKey, "Category", element.Category?.Name, id);

                if (element is FamilyInstance fi)
                {
                    var familyName = fi.Symbol?.Family?.Name;
                    var typeName = fi.Symbol?.Name;
                    AddMeta(byKey, "Family", familyName, id);
                    AddMeta(byKey, "Family Name", familyName, id);
                    AddMeta(byKey, "Family and Type",
                        familyName != null && typeName != null ? familyName + " : " + typeName : null, id);
                    AddMeta(byKey, "Host Id", fi.Host?.Id?.ToString(), id);
                }

                var typeId = element.GetTypeId();
                var typeElem = (typeId != null && typeId != ElementId.InvalidElementId)
                    ? doc.GetElement(typeId)
                    : null;
                AddMeta(byKey, "Type Name", typeElem?.Name, id);

                CollectParameters(byKey, element.Parameters, id, isType: false);
                if (typeElem != null)
                    CollectParameters(byKey, typeElem.Parameters, id, isType: true);
            }

            foreach (var group in byKey.Values)
                group.Values.Sort((a, b) => string.Compare(a.DisplayValue, b.DisplayValue, System.StringComparison.OrdinalIgnoreCase));

            return byKey.Values
                .Where(g => g.Values.Count > 0)
                .OrderBy(g => g.ParameterName, System.StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void CollectParameters(Dictionary<string, ParameterValueGroup> byKey,
            ParameterSet parameters, ElementId id, bool isType)
        {
            foreach (Parameter p in parameters)
            {
                if (p == null || !p.HasValue) continue;

                var bip = p.Definition is InternalDefinition def ? def.BuiltInParameter : BuiltInParameter.INVALID;
                if (bip != BuiltInParameter.INVALID && Denylist.Contains(bip)) continue;

                var name = p.Definition?.Name;
                if (string.IsNullOrWhiteSpace(name)) continue;

                var display = FormatValue(p);
                if (string.IsNullOrWhiteSpace(display)) continue;

                AddBucket(byKey, name, isType, display, display, id);
            }
        }

        private static string FormatValue(Parameter p)
        {
            switch (p.StorageType)
            {
                case StorageType.String:
                    return p.AsString();
                case StorageType.Integer:
                    return p.AsValueString() ?? p.AsInteger().ToString();
                case StorageType.Double:
                    return p.AsValueString() ?? p.AsDouble().ToString("0.###");
                case StorageType.ElementId:
                    var v = p.AsValueString();
                    if (!string.IsNullOrEmpty(v)) return v;
                    var elemId = p.AsElementId();
                    return (elemId != null && elemId != ElementId.InvalidElementId) ? elemId.ToString() : null;
                default:
                    return p.AsValueString();
            }
        }

        private static void AddMeta(Dictionary<string, ParameterValueGroup> byKey,
            string paramName, string value, ElementId id)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            AddBucket(byKey, paramName, false, value, value, id);
        }

        private static void AddBucket(Dictionary<string, ParameterValueGroup> byKey,
            string paramName, bool isType, string display, string rawKey, ElementId id)
        {
            var groupKey = paramName + "" + (isType ? "T" : "I");
            if (!byKey.TryGetValue(groupKey, out var group))
            {
                group = new ParameterValueGroup
                {
                    ParameterName = paramName,
                    IsTypeParameter = isType
                };
                byKey[groupKey] = group;
            }

            var bucket = group.Values.FirstOrDefault(b => b.RawKey == rawKey);
            if (bucket == null)
            {
                bucket = new ValueBucket { DisplayValue = display, RawKey = rawKey };
                group.Values.Add(bucket);
            }
            bucket.ElementIds.Add(id);
        }
    }
}
