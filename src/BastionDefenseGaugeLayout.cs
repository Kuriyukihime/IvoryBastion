using System;
using System.Collections.Generic;
namespace IvoryBastion
{
    internal static class BastionDefenseGaugeLayout
    {
        internal const float Height=30,Gap=6;
        internal const float ReservedSpace=Height+Gap;
        // All bounds are expressed in the health bar's local plane. Projecting
        // them onto the flat HUD first discards its perspective and spring tilt.
        internal static HudPlacement.Box AboveHealth(HudPlacement.Box health,IReadOnlyList<HudPlacement.Box> obstacles)
        {
            float width=Math.Max(0,health.Right-health.Left),bottom=health.Top+Gap;
            // The native layout reserves a gap. Alternate HUDs without that
            // layout safely fall back to the first clear space above their UI.
            for(int pass=0;pass<=obstacles.Count;pass++)
            {
                bool moved=false;
                foreach(var item in obstacles)
                {
                    if(item.Right<=health.Left||item.Left>=health.Left+width||
                       item.Top<=bottom||item.Bottom>=bottom+Height)continue;
                    bottom=item.Top+Gap;moved=true;break;
                }
                if(!moved)break;
            }
            return new HudPlacement.Box(health.Left,bottom,health.Left+width,bottom+Height);
        }
    }
}
