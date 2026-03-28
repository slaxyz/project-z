using System.Collections.Generic;
using System.Linq;

namespace ProjectZ.Combat
{
    public class CardCost
    {
        public CardCost(Dictionary<ElementType, int> requirements)
        {
            Requirements = requirements ?? new Dictionary<ElementType, int>();
        }

        public Dictionary<ElementType, int> Requirements { get; }

        public bool TryGetPrimaryElement(out ElementType element)
        {
            element = default;
            if (Requirements == null || Requirements.Count == 0)
            {
                return false;
            }

            var primary = Requirements
                .Where(pair => pair.Value > 0)
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key)
                .FirstOrDefault();

            if (primary.Value <= 0)
            {
                return false;
            }

            element = primary.Key;
            return true;
        }

        public bool IsMonoElement
        {
            get
            {
                if (Requirements == null || Requirements.Count == 0)
                {
                    return false;
                }

                ElementType? firstElement = null;
                foreach (var requirement in Requirements)
                {
                    if (requirement.Value <= 0)
                    {
                        continue;
                    }

                    if (!firstElement.HasValue)
                    {
                        firstElement = requirement.Key;
                        continue;
                    }

                    if (firstElement.Value != requirement.Key)
                    {
                        return false;
                    }
                }

                return firstElement.HasValue;
            }
        }

        public bool CanAfford(IReadOnlyDictionary<ElementType, int> availableGems)
        {
            foreach (var requirement in Requirements)
            {
                if (!availableGems.TryGetValue(requirement.Key, out var available) || available < requirement.Value)
                {
                    return false;
                }
            }

            return true;
        }

        public string ToDisplayString()
        {
            return string.Join(", ", Requirements
                .OrderBy(pair => pair.Key)
                .Select(pair => pair.Value + " " + pair.Key));
        }
    }
}
