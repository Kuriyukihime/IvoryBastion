using RoR2;
using UnityEngine;
namespace IvoryBastion
{
    internal static class BastionBodyStates
    {
        internal static void Bind(GameObject body)
        {
            // Unity serialization drops the private Type cache when cloning a
            // prefab. Rebind on each live body before leaving its spawn state.
            foreach(var machine in body.GetComponents<EntityStateMachine>())
                if(machine.customName=="Body")machine.mainStateType=BastionStateType.For<BastionMain>();
            var death=body.GetComponent<CharacterDeathBehavior>();
            if(death)death.deathState=BastionStateType.For<BastionDeath>();
        }
    }
}
