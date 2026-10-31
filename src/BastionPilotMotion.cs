using UnityEngine;

namespace IvoryBastion
{
    internal static class BastionPilotMotion
    {
        internal static Vector3 Direction(Vector3 input,Vector3 aim,Vector3 facing,bool ascend,bool boost)
        {
            // Ignore vertical input noise; pitch contributes only while moving forward/back.
            input=Vector3.ProjectOnPlane(input,Vector3.up);
            if(input.sqrMagnitude<.0025f)input=Vector3.zero;
            aim=aim.sqrMagnitude>.001f?aim.normalized:facing.normalized;
            var flat=Vector3.ProjectOnPlane(aim,Vector3.up).normalized;
            if(flat.sqrMagnitude<.01f)flat=Vector3.ProjectOnPlane(facing,Vector3.up).normalized;
            if(flat.sqrMagnitude<.01f)flat=Vector3.forward;
            float forward=Vector3.Dot(input,flat);
            var desired=input-flat*forward+aim*forward;
            if(ascend)desired+=Vector3.up;
            if(boost&&desired.sqrMagnitude<.01f)desired=aim;
            return Vector3.ClampMagnitude(desired,1);
        }
    }
}
