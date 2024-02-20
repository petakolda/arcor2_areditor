using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using Hololens;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.UI;
using IO.Swagger.Model;
using Newtonsoft.Json;
using Microsoft.MixedReality.Toolkit.Utilities;
using Base;

using System.Threading.Tasks;
using Microsoft.MixedReality.Toolkit.Input;
using UnityEngine.XR.WSA.Input;
using System.Drawing;

public class HActionPickerMenu : Singleton<HActionPickerMenu>
{

    public GameObject actinsList;
    public GameObject actionPrefab;
    public GameObject closeButton;
    public GameObject previewAP;

    public GameObject actionsMenu;

    private bool deleteActionPointOnCancel;
    public Dictionary<string, GameObject> listOfActions =  new Dictionary<string, GameObject>();

    private HActionPoint currentActionPoint;
   
  

    void Start()
    {

//        GestureRecognizer gesture = new GestureRecognizer();

        closeButton.GetComponent<Interactable>().OnClick.AddListener(() =>{ cancelAction(); HSelectorManager.Instance.clickedAddAPButton();});
        HProjectManager.Instance.OnActionAddedToScene += OnActionAddedToScene;
        //SceneManagerH.Instance.OnSceneStateEvent += OnSceneStateEvent;
    }


    public void OnActionAddedToScene(object sender, HololensActionEventArgs args){
        currentActionPoint = null;

        closeActions();

    }


    //public async void Show(HActionPoint actionPoint, bool deleteActionPointOnCancel) {
    //    this.deleteActionPointOnCancel = deleteActionPointOnCancel;
    //    currentActionPoint = actionPoint;
    //    actionsMenu.transform.parent = actionPoint.transform;
    //    actionsMenu.transform.localPosition = new Vector3(0, 0, -0.05f);
    //    actionsMenu.SetActive(true);
    //}

    public async void Show(HActionPoint actionPoint, bool deleteActionPointOnCancel) {
        this.deleteActionPointOnCancel = deleteActionPointOnCancel;
        currentActionPoint = actionPoint;

        // Calculate the position one meter away from the finger in the direction it's pointing
        Vector3 actionPointPosition = actionPoint.transform.position + actionPoint.transform.right * -1.0f;

        // Set the position of the action point
        actionPoint.transform.position = actionPointPosition;

        //previewAP = Instantiate(HProjectManager.Instance.ActionPointPrefab, actionPoint.transform);
        //previewAP.transform.localScale = new Vector3(1f, 1f, 1f);
        //previewAP.SetActive(true);

        actionsMenu.transform.parent = actionPoint.transform;
        actionsMenu.transform.localPosition = new Vector3(0, 0, -0.05f); // Adjust this offset as needed
        actionsMenu.SetActive(true);
    }

    public void clearList(){
        foreach(KeyValuePair<string, GameObject> kvp in listOfActions){
            Destroy(kvp.Value);
        }
        
        listOfActions.Clear (); 
    }

    public void closeActions(){
        clearList();
        actionsMenu.transform.parent = null;
       actionsMenu.SetActive(false);
    }

    public void cancelAction(){
        if(deleteActionPointOnCancel && currentActionPoint != null){
            actionsMenu.transform.parent = null;
            HSelectorManager.Instance.removeObjectFromLocked(currentActionPoint);
            currentActionPoint.Remove();
        }
        closeActions();


    }
    public void actionObjectClicked(ActionObjectH actionObject){

        clearList();
        
      if( ActionsManagerH.Instance.ActionObjectsMetadata.TryGetValue(actionObject.Data.Type, out ActionObjectMetadataH aom)){
          aom.ActionsMetadata.Values.ToList();
          foreach (ActionMetadataH am in aom.ActionsMetadata.Values.ToList()){

              GameObject button = Instantiate(actionPrefab);
              button.transform.parent = actinsList.transform;
              button.transform.eulerAngles = actinsList.transform.eulerAngles;
              Interactable interact = button.GetComponent<Interactable>();
              ButtonConfigHelper buttonConfigHelper = button.GetComponent<ButtonConfigHelper>();
              buttonConfigHelper.MainLabelText = am.Name;
              actinsList.GetComponent<GridObjectCollection>().UpdateCollection();
              listOfActions.Add(am.Name, button);
              interact.OnClick.AddListener(() => CreateNewAction(am.Name, actionObject));


          }

      }

    }

    public string InitActionValue(HActionPoint actionPoint, ParameterMetadataH actionParameterMetadata){
        object value = null;
            switch (actionParameterMetadata.Type) {

                case "string":
                    value = actionParameterMetadata.GetDefaultValue<string>();
                    break;
                case "integer":
                    value = actionParameterMetadata.GetDefaultValue<int>();
                    break;
                case "double":
                    value = actionParameterMetadata.GetDefaultValue<double>();
                    break;
                case "boolean":
                    value = actionParameterMetadata.GetDefaultValue<bool>();
                    break;
                case "pose":
                    try {
                        value = actionPoint.GetFirstOrientation().Id;
                    } catch (ItemNotFoundException) {
                        // there is no orientation on this action point
                        try {
                            value = actionPoint.GetFirstOrientationFromDescendants().Id;
                        } catch (ItemNotFoundException) {
                            try {
                                value = HProjectManager.Instance.GetAnyNamedOrientation().Id;
                            } catch (ItemNotFoundException) {                                
                                    
                            }
                        }                         
                    }
                    break;
                case "joints":
                    try {
                        value = actionPoint.GetFirstJoints().Id;
                    } catch (ItemNotFoundException) {
                        // there are no valid joints on this action point
                        try {
                            value = actionPoint.GetFirstJointsFromDescendants().Id;
                        } catch (ItemNotFoundException) {
                            try {
                                value = HProjectManager.Instance.GetAnyJoints().Id;
                            } catch (ItemNotFoundException) {
                                // there are no valid joints in the scene
                            }
                        }
                        
                    }
                    break;
                case "string_enum":
                    value =  ((ARServer.Models.StringEnumParameterExtra) actionParameterMetadata.ParameterExtra).AllowedValues.First();
                    break;
                case "integer_enum":
                    value = ((ARServer.Models.IntegerEnumParameterExtra) actionParameterMetadata.ParameterExtra).AllowedValues.First().ToString();
                    break;
                    /*
                        case "relative_pose":
                parameter = InitializeRelativePoseParameter(actionParameterMetadata, handler, Parameter.GetValue<IO.Swagger.Model.Pose>(value), linkable);
                break;*/
            }
            if (value != null) {
                value = JsonConvert.SerializeObject(value);
            }


        return (string) value;
    }

    public async void CreateNewAction(string action_id, IActionProviderH actionProvider, string newName = null) {
        
    ActionMetadataH actionMetadata = actionProvider.GetActionMetadata(action_id);
    List<IO.Swagger.Model.ActionParameter> parameters = new List<IO.Swagger.Model.ActionParameter>();

    foreach (ParameterMetadataH parameterMetadata in   actionMetadata.ParametersMetadata.Values.ToList()) {
        string value = InitActionValue(currentActionPoint, parameterMetadata);
        IO.Swagger.Model.ActionParameter ap = new IO.Swagger.Model.ActionParameter(name: parameterMetadata.Name, value: value, type: parameterMetadata.Type);
        parameters.Add(ap);
    }
    string newActionName;

    if (string.IsNullOrEmpty(newName))
        newActionName = HProjectManager.Instance.GetFreeActionName(actionMetadata.Name);
    else
        newActionName = HProjectManager.Instance.GetFreeActionName(newName);

    try {

        await WebSocketManagerH.Instance.AddAction(currentActionPoint.GetId(), parameters, Base.Action.BuildActionType(
                actionProvider.GetProviderId(), actionMetadata.Name), newActionName, actionMetadata.GetFlows(newActionName));

    } catch (Base.RequestFailedException e) {
            // Base.Notifications.Instance.ShowNotification("Failed to add action", e.Message);
    }
          
    
    }
}
