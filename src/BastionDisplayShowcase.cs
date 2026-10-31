using RoR2;
using UnityEngine;
namespace IvoryBastion
{
    // Display prefab only. A camera-relative offset keeps screen-left consistent
    // with the native selection camera, independently of the mannequin's yaw.
    public sealed class BastionDisplayShowcase:MonoBehaviour
    {
        private BastionAnimation model;private float began;private Vector3 rest;
        private bool soundPlayed,positioned;private BastionPcmVoice unfolding;
        private void OnEnable()
        {
            began=Time.unscaledTime;rest=transform.localPosition;soundPlayed=positioned=false;
            model=GetComponent<BastionAnimation>();SceneCamera.onSceneCameraPreCull+=BeforeCull;
        }
        private void Update()
        {
            if(!model)model=GetComponent<BastionAnimation>();
            if(!model||model.Owner)return;
            model.Showcase=true;float age=Time.unscaledTime-began;model.ShowcaseUnfold=BastionDisplayRules.Unfold(age);
            var view=SceneCamera._camera?SceneCamera._camera:Camera.main;if(view)MoveLeft(view);
            if(!soundPlayed&&age>=BastionDisplayRules.Hold)
            {soundPlayed=true;unfolding=BastionAudio.PreviewTransformation();}
        }
        private void BeforeCull(SceneCamera scene)
        {if(model&&!model.Owner&&scene&&scene.camera)MoveLeft(scene.camera);}
        private void MoveLeft(Camera view)
        {
            var anchor=transform.parent?transform.parent.TransformPoint(rest):rest;
            transform.position=anchor-view.transform.right*BastionDisplayRules.LeftShift;positioned=true;
        }
        private void OnDisable()
        {
            SceneCamera.onSceneCameraPreCull-=BeforeCull;
            if(positioned)transform.localPosition=rest;
            if(model&&!model.Owner)model.Showcase=false;
            unfolding?.Dispose();unfolding=null;
        }
    }
}
