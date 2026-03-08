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
