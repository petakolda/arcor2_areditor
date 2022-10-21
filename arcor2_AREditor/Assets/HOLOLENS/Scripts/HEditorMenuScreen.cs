using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.MixedReality.Toolkit.UI;
using System;
using Base;
using IO.Swagger.Model;
using Newtonsoft.Json;
using System.Linq;



public class HEditorMenuScreen : Singleton<HEditorMenuScreen>
{

    public Interactable closeSceneButton;

    public Interactable notificationButton;
    public Interactable switchSceneState;

    // Start is called before the first frame update
    void Start()
    {
       // HideEditorSceneMenu();

       // GameManagerH.Instance.OnOpenSceneEditor += OnOpenEditorSceneMenu;
       // GameManagerH.Instance.OnOpenProjectEditor += OnOpenEditorSceneMenu;
    
     //   closeSceneButton.OnClick.AddListener(() => CloseScene());
        notificationButton.OnClick.AddListener(() => HNotificationManager.Instance.ShowNotificationScreen());
        switchSceneState.OnClick.AddListener(() => SwitchSceneState());
        SceneManagerH.Instance.OnSceneStateEvent += OnSceneStateEvent;


  //  #if !UNITY_EDITOR
      //  recalibrateButton.ButtonPressed.AddListener(() => QRTracking.QRCodesManager.Instance.StartQRTracking());
   // #else
  //      recalibrateButton.enabled = false;
  //  #endif

    }

     public async void SaveScene() {
       // SaveButton.SetInteractivity(false, "Saving scene...");
        IO.Swagger.Model.SaveSceneResponse saveSceneResponse = await GameManagerH.Instance.SaveScene();
        if (!saveSceneResponse.Result) {
            saveSceneResponse.Messages.ForEach(Debug.LogError);
            HNotificationManager.Instance.ShowNotification("Scene save failed: " + ( saveSceneResponse.Messages.Count > 0 ? saveSceneResponse.Messages[0] : "Failed to save scene"));
            return;
        } else {
             HNotificationManager.Instance.ShowNotification("There are no unsaved changes");
        }
    }

    public async void SaveProject(){


          IO.Swagger.Model.SaveProjectResponse saveProjectResponse = await WebSocketManagerH.Instance.SaveProject();
            if (saveProjectResponse != null && !saveProjectResponse.Result) {
           
                saveProjectResponse.Messages.ForEach(Debug.LogError);
                HNotificationManager.Instance.ShowNotification("Failed to save project " + (saveProjectResponse.Messages.Count > 0 ? saveProjectResponse.Messages[0] : ""));
                return;
            }
    }


    public async void CloseScene() {

        if(GameManagerH.Instance.GetGameState() == GameManagerH.GameStateEnum.SceneEditor){
               SaveScene();
        }
         else if (GameManagerH.Instance.GetGameState() == GameManagerH.GameStateEnum.ProjectEditor){
             SaveProject();
        }


        if (SceneManagerH.Instance.SceneStarted)
            WebSocketManagerH.Instance.StopScene(false, null);

        bool success = false;
        string message;
        if(GameManagerH.Instance.GetGameState() == GameManagerH.GameStateEnum.SceneEditor){
            (success, message) = await GameManagerH.Instance.CloseScene(true);
        }
        else if (GameManagerH.Instance.GetGameState() == GameManagerH.GameStateEnum.ProjectEditor){
            (success, message) = await GameManagerH.Instance.CloseProject(true);
        }

    }

    private void OnSceneStateEvent(object sender, SceneStateEventArgs args) {
        if (args.Event.State == IO.Swagger.Model.SceneStateData.StateEnum.Started) {
            switchSceneState.IsToggled = true;
        } 
        else {
            switchSceneState.IsToggled = false;
        }
    }


        public void SwitchSceneState() {
        if (SceneManagerH.Instance.SceneStarted)
            StopScene();
        else
            StartScene();
    }

    public async void StartScene() {
        try {
            await WebSocketManagerH.Instance.StartScene(false);
        } catch (RequestFailedException e) {
           HNotificationManager.Instance.ShowNotification("Going online failed " +  e.Message);
        }
    }

    private void StopSceneCallback(string _, string data) {
        CloseProjectResponse response = JsonConvert.DeserializeObject<CloseProjectResponse>(data);
        if (!response.Result)
            HNotificationManager.Instance.ShowNotification("Going offline failed " +  response.Messages.FirstOrDefault());
    }

    public void StopScene() {
        WebSocketManagerH.Instance.StopScene(false, StopSceneCallback);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}