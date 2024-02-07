using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using IO.Swagger.Model;
using System;
using Base;


namespace Hololens {
    public abstract class ActionObjectH : HInteractiveObject, IActionProviderH, IActionPointParentH
    {
       public GameObject ActionPointsSpawn;
        [System.NonSerialized]
        public int CounterAP = 0;
        protected float visibility;

        public Collider Collider;

        public IO.Swagger.Model.SceneObject Data = new IO.Swagger.Model.SceneObject(id: "", name: "", pose: DataHelper.CreatePose(new Vector3(), new Quaternion()), type: "");
        public ActionObjectMetadataH ActionObjectMetadata;

        public Dictionary<string, Base.Parameter> ObjectParameters = new Dictionary<string,Base.Parameter>();
        public Dictionary<string, Base.Parameter> Overrides = new Dictionary<string, Base.Parameter>();


        public virtual void InitActionObject(IO.Swagger.Model.SceneObject sceneObject, Vector3 position, Quaternion orientation, ActionObjectMetadataH actionObjectMetadata, IO.Swagger.Model.CollisionModels customCollisionModels = null, bool loadResuources = true) {
            Data.Id = sceneObject.Id;
            Data.Type = sceneObject.Type;
            name = sceneObject.Name; // show actual object name in unity hierarchy
            ActionObjectMetadata = actionObjectMetadata;
          
            if (actionObjectMetadata.HasPose) {
                SetScenePosition(position);
                SetSceneOrientation(orientation);
            }
            CreateModel(customCollisionModels);
            enabled = true;
          //  SelectorItem = SelectorMenu.Instance.CreateSelectorItem(this);
            //if (VRModeManager.Instance.VRModeON) {
                SetVisibility(PlayerPrefsHelper.LoadFloat("AOVisibilityVR", 1f));
         //   } else {
        //        SetVisibility(PlayerPrefsHelper.LoadFloat("AOVisibilityAR", 0f));
       //     }

            if (PlayerPrefsHelper.LoadBool($"ActionObject/{GetId()}/blocklisted", false)) {
                Enable(false, true, false);
            }

        }
        
        public virtual void UpdateObjectName(string newUserId) {
            Data.Name = newUserId;
        //    SelectorItem.SetText(newUserId);
        }

        protected virtual void Update() {
            if (ActionObjectMetadata != null && ActionObjectMetadata.HasPose && gameObject.transform.hasChanged) {
                transform.hasChanged = false;
            }
        }

        public virtual void ActionObjectUpdate(IO.Swagger.Model.SceneObject actionObjectSwagger) {
            if (Data != null & Data.Name != actionObjectSwagger.Name)
                UpdateObjectName(actionObjectSwagger.Name);
            Data = actionObjectSwagger;
            foreach (IO.Swagger.Model.Parameter p in Data.Parameters) {

                if (!ObjectParameters.ContainsKey(p.Name)) {
                    if (TryGetParameterMetadata(p.Name, out ParameterMeta parameterMeta)) {
                        ObjectParameters[p.Name] = new Base.Parameter(parameterMeta, p.Value);
                    } else {
                        Debug.LogError("Failed to load metadata for parameter " + p.Name);
                        HNotificationManager.Instance.ShowNotification("Critical error + Failed to load parameter's metadata.");
                        return;
                    }

                } else {
                    ObjectParameters[p.Name].Value = p.Value;
                }
                
            }
            Show();
            //TODO: update all action points and actions.. ?
                
            // update position and rotation based on received data from swagger
            //if (visibility)
            //    Show();
            //else
            //    Hide();

            
        }

        public void ResetPosition() {
            transform.localPosition = GetScenePosition();
            transform.localRotation = GetSceneOrientation();
        }

        public bool TryGetParameter(string id, out IO.Swagger.Model.Parameter parameter) {
            foreach (IO.Swagger.Model.Parameter p in Data.Parameters) {
                if (p.Name == id) {
                    parameter = p;
                    return true;
                }
            }
            parameter = null;
            return false;
        }
                
        public bool TryGetParameterMetadata(string id, out IO.Swagger.Model.ParameterMeta parameterMeta) {
            foreach (IO.Swagger.Model.ParameterMeta p in ActionObjectMetadata.Settings) {
                if (p.Name == id) {
                    parameterMeta = p;
                    return true;
                }
            }
            parameterMeta = null;
            return false;
        }
                
        public abstract Vector3 GetScenePosition();

        public abstract void SetScenePosition(Vector3 position);

        public abstract Quaternion GetSceneOrientation();

        public abstract void SetSceneOrientation(Quaternion orientation);

        public void SetWorldPosition(Vector3 position) {
            Data.Pose.Position = DataHelper.Vector3ToPosition(position);
        }

        public Vector3 GetWorldPosition() {
            return DataHelper.PositionToVector3(Data.Pose.Position);
        }
        public void SetWorldOrientation(Quaternion orientation) {
            Data.Pose.Orientation = DataHelper.QuaternionToOrientation(orientation);
        }

        public Quaternion GetWorldOrientation() {
            return DataHelper.OrientationToQuaternion(Data.Pose.Orientation);
        }

        public string GetProviderName() {
            return Data.Name;
        }


        public ActionMetadataH GetActionMetadata(string action_id) {
            if (ActionObjectMetadata.ActionsLoaded) {
                if (ActionObjectMetadata.ActionsMetadata.TryGetValue(action_id, out ActionMetadataH actionMetadata)) {
                    return actionMetadata;
                } else {
                    throw new Exception("Metadata not found");
                }
            }
            return null; //TODO: throw exception
        }


        public bool IsRobot() {
            return ActionObjectMetadata.Robot;
        }

        public bool IsCamera() {
            return ActionObjectMetadata.Camera;
        }

        public virtual void DeleteActionObject() {
            // Remove all actions of this action point
            RemoveActionPoints();
            
            // Remove this ActionObject reference from the scene ActionObject list
            SceneManagerH.Instance.ActionObjects.Remove(this.Data.Id);

            DestroyObject();
            Destroy(gameObject);
        }

        public  void DestroyObject() {
      
       // LockingEventsCache.Instance.OnObjectLockingEvent -= OnObjectLockingEvent;
        }

        public void RemoveActionPoints() {
            // Remove all action points of this action object
            List<Base.ActionPoint> actionPoints = GetActionPoints();
            foreach (Base.ActionPoint actionPoint in actionPoints) {
                actionPoint.DeleteAP();
            }
        }


        public virtual void SetVisibility(float value, bool forceShaderChange = false) {
            //Debug.Assert(value >= 0 && value <= 1, "Action object: " + Data.Id + " SetVisibility(" + value.ToString() + ")");
            visibility = value;
            //PlayerPrefsHelper.SaveFloat(SceneManager.Instance.SceneMeta.Id + "/ActionObject/" + Data.Id + "/visibility", value);
        }

        public float GetVisibility() {
            return visibility;
        }

        public abstract void Show();

        public abstract void Hide();

        public abstract void SetInteractivity(bool interactive);


        public virtual void ActivateForGizmo(string layer) {
            gameObject.layer = LayerMask.NameToLayer(layer);
        }

        public string GetProviderId() {
            return Data.Id;
        }

        public abstract void UpdateModel();

        //TODO: is this working?
        public List<Base.ActionPoint> GetActionPoints() {
            List<Base.ActionPoint> actionPoints = new List<Base.ActionPoint>();
         /*   foreach (ActionPoint actionPoint in ProjectManager.Instance.ActionPoints.Values) {
                if (actionPoint.Data.Parent == Data.Id) {
                    actionPoints.Add(actionPoint);
                }
            }*/
            return actionPoints;
        }

        public override string GetName() {
            return Data.Name;
        }

      
        public bool IsActionObject() {
            return true;
        }

        public Hololens.ActionObjectH GetActionObject() {
            return this;
        }


        public Transform GetTransform() {
            return transform;
        }

        public string GetProviderType() {
            return Data.Type;
        }

        public GameObject GetGameObject() {
            return gameObject;
        }

        public override string GetId() {
            return Data.Id;
        }

        public async override Task<RequestResult> Movable() {
            if (!ActionObjectMetadata.HasPose)
                return new RequestResult(false, "Selected action object has no pose");
            else if (GameManagerH.Instance.GetGameState() != GameManagerH.GameStateEnum.SceneEditor) {
                return new RequestResult(false, "Action object could be moved only in scene editor");
            } else {
                return new RequestResult(true);
            }
        }

        public abstract void CreateModel(IO.Swagger.Model.CollisionModels customCollisionModels = null);
        public abstract GameObject GetModelCopy();

    public IO.Swagger.Model.Pose GetPose() {
        if (ActionObjectMetadata.HasPose)
            return new IO.Swagger.Model.Pose(position: DataHelper.Vector3ToPosition(TransformConvertor.UnityToROS(transform.localPosition)),
                orientation: DataHelper.QuaternionToOrientation(TransformConvertor.UnityToROS(transform.localRotation)));
        else
            return new IO.Swagger.Model.Pose(orientation: new IO.Swagger.Model.Orientation(), position: new IO.Swagger.Model.Position());
    }
    public async override Task Rename(string name) {
        try {
            await WebSocketManagerH.Instance.RenameObject(GetId(), name);
         //   Notifications.Instance.ShowToastMessage("Action object renamed");
        } catch (RequestFailedException e) {
         //   Notifications.Instance.ShowNotification("Failed to rename action object", e.Message);
            throw;
        }
    }
    public async override Task<RequestResult> Removable() {
        if (GameManagerH.Instance.GetGameState() != GameManagerH.GameStateEnum.SceneEditor) {
            return new RequestResult(false, "Action object could be removed only in scene editor");
        } else if (SceneManagerH.Instance.SceneStarted) {
            return new RequestResult(false, "Scene online");
        } else {
                IO.Swagger.Model.RemoveFromSceneResponse response = await WebSocketManagerH.Instance.RemoveFromScene(GetId(), false, true);
            if (response.Result)
                return new RequestResult(true);
            else
                return new RequestResult(false, response.Messages[0]);
        }
    }


    public async override void Remove() {
            IO.Swagger.Model.RemoveFromSceneResponse response =
            await WebSocketManagerH.Instance.RemoveFromScene(GetId(), false, false);
        if (!response.Result) {
            HNotificationManager.Instance.ShowNotification("Failed to remove object " + GetName() + " " +  response.Messages[0]);
            return;
        }
    }

        public Transform GetSpawnPoint() {
            return transform;
        }
    }
}
