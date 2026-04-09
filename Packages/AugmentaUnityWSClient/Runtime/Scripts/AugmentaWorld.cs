using System.Collections.Generic;
using UnityEngine.Assertions;

namespace AugmentaWebsocketClient
{
    public class AugmentaWorld : AugmentaContainer
    {
        // TODO: This is not actually necessary since in theory any container could have childs of any types
        public List<AugmentaScene> scenes = new();

        public AugmentaScene GetScene(int sceneIdx = 0)
        {
            Assert.IsTrue(sceneIdx < this.scenes.Count);
            return this.scenes[sceneIdx];
        }

        public AugmentaScene GetSceneByName(string name)
        {
            foreach(var scene in scenes)
            {
                if (scene.name == name)
                { 
                    return scene; 
                }
            }
            return null;
        }

        protected override void AddChildScene(AugmentaScene sceneComponent)
        {
            base.AddChildScene(sceneComponent);
            this.scenes.Add(sceneComponent);
        }
    }
}
