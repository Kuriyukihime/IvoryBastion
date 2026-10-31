using UnityEngine;
namespace IvoryBastion
{
    internal enum BastionResourceKind { Fuel,Missiles,Heavy,Fabrication }
    internal static class BastionResourcePalette
    {
        internal static Color32 Tint(BastionResourceKind kind)
        {
            switch(kind)
            {
                case BastionResourceKind.Missiles:return new Color32(238,99,105,255);
                case BastionResourceKind.Heavy:return new Color32(255,159,67,255);
                case BastionResourceKind.Fabrication:return new Color32(145,215,102,255);
                default:return new Color32(51,220,203,255);
            }
        }
    }
}
