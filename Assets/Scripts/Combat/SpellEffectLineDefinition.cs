using System;

namespace ProjectZ.Combat
{
    public enum SpellEffectKind
    {
        Deal,
        Heal,
        Shield,
        Burn,
        Increase,
        Poison,
        Freeze,
        Special
    }

    [Serializable]
    public class SpellEffectLineDefinition
    {
        public SpellEffectKind kind = SpellEffectKind.Deal;
        public int amount = 1;
        public ElementType element = ElementType.Fire;

        public bool UsesElement
        {
            get
            {
                switch (kind)
                {
                    case SpellEffectKind.Deal:
                    case SpellEffectKind.Increase:
                    case SpellEffectKind.Freeze:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public bool UsesFixedNeutralElement => kind == SpellEffectKind.Freeze;

        public bool UsesTrailingElement => kind == SpellEffectKind.Freeze;

        public string BuildLabel()
        {
            switch (kind)
            {
                case SpellEffectKind.Deal:
                    return "Deal";
                case SpellEffectKind.Heal:
                    return "Heal";
                case SpellEffectKind.Shield:
                    return "Shield";
                case SpellEffectKind.Burn:
                    return "Burn";
                case SpellEffectKind.Increase:
                    return "Increase";
                case SpellEffectKind.Poison:
                    return "Poison";
                case SpellEffectKind.Freeze:
                    return "Freeze";
                case SpellEffectKind.Special:
                    return "Special";
                default:
                    return kind.ToString();
            }
        }

        public string BuildAmountText()
        {
            return "(" + amount + ")";
        }

        public string BuildDisplayText()
        {
            if (UsesElement && !UsesTrailingElement)
            {
                return BuildLabel() + " [" + BuildElementLabel() + "] " + BuildAmountText();
            }

            if (UsesElement && UsesTrailingElement)
            {
                return BuildLabel() + " " + BuildAmountText() + " [" + BuildElementLabel() + "]";
            }

            return BuildLabel() + " " + BuildAmountText();
        }

        private string BuildElementLabel()
        {
            if (kind == SpellEffectKind.Freeze)
            {
                return "Neutral";
            }

            switch (element)
            {
                case ElementType.Fire:
                    return "Fire";
                case ElementType.Water:
                    return "Water";
                case ElementType.Ground:
                    return "Ground";
                case ElementType.Mystic:
                    return "Mystic";
                case ElementType.Nature:
                    return "Nature";
                case ElementType.Poison:
                    return "Poison";
                default:
                    return element.ToString();
            }
        }
    }
}
