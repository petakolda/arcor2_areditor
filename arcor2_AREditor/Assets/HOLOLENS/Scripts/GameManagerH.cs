using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.UI;
using IO.Swagger.Model;
using UnityEngine.XR.ARFoundation;
using UnityEngine.Events;
using System.Collections;
using Newtonsoft.Json;
using Base;
using RosSharp.Urdf;
using Hololens;
public class GameManagerH : Singleton<GameManagerH>
{

    private bool updatingPackageState;

    private Boolean calibrationSet = false; 

    /// <summary>
    /// Called when editor is trying to connect to server. Contains server URI
    /// </summary>
    public event AREditorEventArgs.StringEventHandler OnConnectingToServer;

    /// <summary>
    /// Temp storage for delayed scene
    /// </summary>
    private IO.Swagger.Model.Scene newScene;

    /// <summary>
    /// Api version
    /// </summary>        
    public const string ApiVersion = "1.0.0";

    /// <summary>
    /// GameObject of scene
    /// </summary>
    public GameObject Scene;

    /// <summary>
    /// Holds current editor state
    /// </summary>
    private EditorStateEnum editorState;

    /// <summary>
    /// Indicates that scene should be opened with delay (waiting for action objects)
    /// </summary>
    private bool openScene = false;

    /// <summary>
    /// Indicates that project should be opened with delay (waiting for scene or action objects)
    /// </summary>
    private bool openProject = false;

    /// <summary>
    /// Holds info of connection status
    /// </summary>
    private ConnectionStatusEnum connectionStatus;

    /// <summary>
    /// Called when editor connected to server. Contains server URI
    /// </summary>
    public event AREditorEventArgs.StringEventHandler OnConnectedToServer;

    /// <summary>
    /// Invoked when scene editor is opened
    /// </summary>
    public event EventHandler OnOpenSceneEditor;

    /// <summary>
    /// Enum specifying connection states
    /// </summary>
    public enum ConnectionStatusEnum {
        Connected, Disconnected, Connecting
    }

    /// <summary>
    /// Called when list of scenes is changed (new, removed, renamed)
    /// </summary>
    public event EventHandler OnScenesListChanged;

    /// <summary>
    /// Holds current application state (opened screen)
    /// </summary>
    private GameStateEnum gameState;

    public GameObject LoadingScreen;
    /// <summary>
    /// Holds info about server (version, supported RPCs, supported parameters etc.)
    /// </summary>
    public IO.Swagger.Model.SystemInfoResponseData SystemInfo;

    /// <summary>
    /// Temp storage for delayed project
    /// </summary>
    private IO.Swagger.Model.Project newProject;

 /// <summary>
        /// Invoked when game state changed. Contains new state
        /// </summary>
    public event AREditorEventArgs.HololensGameStateEventHandler OnGameStateChanged;
    public List<IO.Swagger.Model.ListScenesResponseData> Scenes = new List<IO.Swagger.Model.ListScenesResponseData>();

     /// <summary>
    /// List of projects metadata
    /// </summary>
    public List<IO.Swagger.Model.ListProjectsResponseData> Projects = new List<IO.Swagger.Model.ListProjectsResponseData>();


    /// <summary>
    /// Called when list of projects is changed (new project, removed project, renamed project)
    /// </summary>
    public event EventHandler OnProjectsListChanged;

    /// <summary>
    /// Called when scene is closed
    /// </summary>
    public event EventHandler OnCloseScene;

    /// <summary>
    /// Invoked when project editor is opened
    /// </summary>
    public event EventHandler OnOpenProjectEditor;

    /// <summary>
    /// Called when project is closed
    /// </summary>
    public event EventHandler OnCloseProject;

    /// <summary>
    /// Holds whether delayed openning of main screen is requested
    /// </summary>
    private bool openMainScreenRequest = false;

    /// <summary>
    /// Holds info about what part of main screen should be displayd
    /// </summary>
    private ShowMainScreenData openMainScreenData;

    /// <summary>
    /// Enum specifying editor states
    ///
    /// For selecting states - other interaction than selecting of requeste object is disabled
    /// </summary>
    public enum EditorStateEnum {
        /// <summary>
        /// No editor (scene or project) opened
        /// </summary>
        Closed,
        /// <summary>
        /// Normal state
        /// </summary>
        Normal,
        /// <summary>
        /// Indicates that user should select action object
        /// </summary>
        SelectingActionObject,
        /// <summary>
        /// Indicates that user should select action point
        /// </summary>
        SelectingActionPoint,
        /// <summary>
        /// Indicates that user should select action 
        /// </summary>
        SelectingAction,
        /// <summary>
        /// Indicates that user should select action input
        /// </summary>
        SelectingActionInput,
        /// <summary>
        /// Indicates that user should select action output
        /// </summary>
        SelectingActionOutput,
        /// <summary>
        /// Indicates that user should select action object or another action point
        /// </summary>
        SelectingActionPointParent,
        /// <summary>
        /// Indicates that user should select orientation of action point
        /// </summary>
        SelectingAPOrientation,
        /// <summary>
        /// Indicates that user should select end effector
        /// </summary>
        SelectingEndEffector,
        /// <summary>
        /// Indicates that all interaction is disabled
        /// </summary>
        InteractionDisabled
    }

    /// <summary>
    /// Enum specifying aplication states
    /// </summary>
    public enum GameStateEnum {
        /// <summary>
        /// Not connected to server
        /// </summary>
        Disconnected,
        /// <summary>
        /// Screen with list of scenes, projects and packages
        /// </summary>
        MainScreen,
        /// <summary>
        /// Scene editor
        /// </summary>
        SceneEditor,
        /// <summary>
        /// Project editor
        /// </summary>
        ProjectEditor,
        /// <summary>
        /// Visualisation of running package
        /// </summary>
        PackageRunning,
        LoadingScene,
        LoadingProject,
        LoadingPackage,
        ClosingScene,
        ClosingProject,
        ClosingPackage,
        None
    }

    /// <summary>
    /// Change game state and invoke coresponding event
    /// </summary>
    /// <param name="value">New game state</param>
    public void SetGameState(GameStateEnum value) {
        gameState = value;            
        OnGameStateChanged?.Invoke(this, new HololensGameStateEventArgs(gameState));            
    }

    /// <summary>
    /// Invoked when editor state changed. Contains new state
    /// </summary>
    public event AREditorEventArgs.EditorStateEventHandler OnEditorStateChanged;

    /// <summary>
    /// Holds connection status and invokes callback when status changed
    /// </summary>
    public ConnectionStatusEnum ConnectionStatus {
        get => connectionStatus; set {
            if (connectionStatus != value) {
                OnConnectionStatusChanged(value);
            }
        }
    }

    /// <summary>
    /// Binds events and sets initial state of app
    /// </summary>
    void Start()
    {
       
        Scene.SetActive(false);

        WebSocketManagerH.Instance.OnConnectedEvent += OnConnected;
        WebSocketManagerH.Instance.OnDisconnectEvent += OnDisconnected;
        WebSocketManagerH.Instance.OnShowMainScreen += OnShowMainScreen;
        ActionsManagerH.Instance.OnActionsLoaded += OnActionsLoaded;
     /*   WebsocketManager.Instance.OnProjectRemoved += OnProjectRemoved;
        WebsocketManager.Instance.OnProjectBaseUpdated += OnProjectBaseUpdated;
        WebsocketManager.Instance.OnSceneRemoved += OnSceneRemoved;
        WebsocketManager.Instance.OnSceneBaseUpdated += OnSceneBaseUpdated;*/
    //    ConnectToSever("192.168.104.111", 6789);
    }


    /// <summary>
    /// When connected to server, checks for requests for delayd scene, project, package or main screen openning
    /// </summary>
    private async Task Update() {
        // Only when connected to server
        if (ConnectionStatus != ConnectionStatusEnum.Connected)
            return;
       
        // request for delayed openning of scene to allow loading of action objects and their actions
        if (openScene) {
            openScene = false;
            if (newScene != null) {
                Scene scene = newScene;
                newScene = null;
                await SceneOpened(scene);
            }
            // request for delayed openning of project to allow loading of action objects and their actions
        } else if (openProject) {
            openProject = false;
            if (newProject != null && newScene != null) {
                Scene scene = newScene;
                Project project = newProject;
                newScene = null;
                newProject = null;
                ProjectOpened(scene, project);
            }
            // request for delayed openning of package to allow loading of action objects and their actions
        }/* else if (openPackage) {
            openPackage = false;
            updatingPackageState = true;
            UpdatePackageState(newPackageState);
        }*/
  /*      if (nextPackageState != null && !updatingPackageState && (GameManager.Instance.GetGameState() == GameStateEnum.PackageRunning || GameManager.Instance.GetGameState() == GameStateEnum.LoadingPackage || GameManager.Instance.GetGameState() == GameStateEnum.ClosingPackage)) {
            updatingPackageState = true;
            UpdatePackageState(nextPackageState);
            nextPackageState = null;
        }*/
        // request for delayed openning of main screen to allow loading of action objects and their actions
        if (openMainScreenRequest && ActionsManagerH.Instance.ActionsReady) {
            openMainScreenRequest = false;
            await OpenMainScreen(openMainScreenData.What, openMainScreenData.Highlight);
        }
   /*     if (openPackageRunningScreenFlag && GetGameState() != GameStateEnum.PackageRunning) {
            openPackageRunningScreenFlag = false;
            OpenPackageRunningScreen();
        }*/

    }

    /// <summary>
    /// When actions are loaded, enables all menus
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnActionsLoaded(object sender, EventArgs e) {
        Debug.Log("OnActionLoaded");
    }


    /// <summary>
    /// Shows loading screen
    /// </summary>
    public void ShowLoadingScreen() {

        LoadingScreen.SetActive(true);

    }

    /// <summary>
    /// Connects to server
    /// </summary>
    /// <param name="domain">hostname or IP address</param>
    /// <param name="port">Port of ARServer</param>
    public async void ConnectToSever(string domain, int port) {
       // ShowLoadingScreen("Connecting to server");
       
        HNotificationManager.Instance.ShowNotification("Connectig to server");
        OnConnectingToServer?.Invoke(this, new StringEventArgs(WebSocketManagerH.Instance.GetWSURI(domain, port)));
        WebSocketManagerH.Instance.ConnectToServer(domain, port);
    }

    /// <summary>
    /// Asks server to open scene
    /// </summary>
    /// <param name="id">Scene id</param>
    public async Task OpenScene(string id) {
        ShowLoadingScreen();
        try {
            await WebSocketManagerH.Instance.OpenScene(id);
        } catch (RequestFailedException e) {
            HNotificationManager.Instance.ShowNotification("Open scene failed " + e);
            HideLoadingScreen();
            return;
        }    
        try {
            await Task.Run(() => WaitForSceneReady(5000));
            return;
        } catch (TimeoutException e) {
             HNotificationManager.Instance.ShowNotification("Open scene failed");
             HideLoadingScreen();
        }
        
    }

            
    /// <summary>
    /// Asks server to open project
    /// </summary>
    /// <param name="id">Project id</param>
    public async void OpenProject(string id) {
        ShowLoadingScreen();
        try {
            await WebSocketManagerH.Instance.OpenProject(id);
            await Task.Run(() => WaitForProjectReady(5000));
        } catch (RequestFailedException ex) {
            HNotificationManager.Instance.ShowNotification("Failed to open project  :" + ex.Message);
            HideLoadingScreen();
        } catch (TimeoutException e) {
            HNotificationManager.Instance.ShowNotification("Open project failed - Failed to load project");
            HideLoadingScreen();
        } 
    }

    
    /// <summary>
    /// Waits until scene is loaded
    /// </summary>
    /// <param name="timeout">TimeoutException is thrown after timeout ms when scene is not loaded</param>
    public void WaitForSceneReady(int timeout) {
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        while (SceneManagerH.Instance.SceneMeta == null) {
            if (sw.ElapsedMilliseconds > timeout)
                throw new TimeoutException();
            System.Threading.Thread.Sleep(100);
        }
        return;
    }

    /// <summary>
    /// Waits until project is loaded
    /// </summary>
    /// <param name="timeout">TimeoutException is thrown after timeout ms when project is not loaded</param>
    public void WaitForProjectReady(int timeout) {
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        while (HProjectManager.Instance.ProjectMeta == null) {
            if (sw.ElapsedMilliseconds > timeout)
                throw new TimeoutException();
            System.Threading.Thread.Sleep(100);
        }
        return;
    }

    
    /// <summary>
    /// Hides loading screen
    /// </summary>
    public void HideLoadingScreen(bool force = false) {

        LoadingScreen.SetActive(false);
   
    }


    /// <summary>
    /// Sets initial state of app
    /// </summary>
    private void Awake() {
        ConnectionStatus = ConnectionStatusEnum.Disconnected;
       // OpenDisconnectedScreen();
    }

    /// <summary>
    /// Disconnects from server
    /// </summary>
    public void DisconnectFromSever() {
        ConnectionStatus = ConnectionStatusEnum.Disconnected;
        WebSocketManagerH.Instance.DisconnectFromSever();
    }


    /// <summary>
    /// Event called when disconnected from server
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void OnDisconnected(object sender, EventArgs e) {
        
    }

    /// <summary>
    /// Event called when connected to server
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void OnConnected(object sender, EventArgs args) {
        // initialize when connected to the server
        ConnectionStatus = ConnectionStatusEnum.Connected;
        HNotificationManager.Instance.ShowNotification("Connected");
        //Debug.LogError("onConnected triggered");
    }


    /// <summary>
    /// Event called when connections status chanched
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private async void OnConnectionStatusChanged(ConnectionStatusEnum newState) {
        switch (newState) {
            case ConnectionStatusEnum.Connected:
                IO.Swagger.Model.SystemInfoResponseData systemInfo;
                try {
                    systemInfo = await WebSocketManagerH.Instance.GetSystemInfo();
                    await WebSocketManagerH.Instance.RegisterUser(HLandingManager.Instance.GetUsername());
                } catch (RequestFailedException ex) {
                    DisconnectFromSever();
                    HNotificationManager.Instance.ShowNotification("Connection failed " + ex.Message);
                    return;
                }
                if (!CheckApiVersion(systemInfo)) {
                    DisconnectFromSever();
                    return;
                }

                SystemInfo = systemInfo;
                Debug.Log(systemInfo.ToJson());
                OnConnectedToServer?.Invoke(this, new StringEventArgs(WebSocketManagerH.Instance.APIDomainWS));

                await UpdateRobotsMeta();
                await UpdateActionObjects();

                try {
                    await Task.Run(() => ActionsManagerH.Instance.WaitUntilActionsReady(15000));
                } catch (TimeoutException e) {
                    HNotificationManager.Instance.ShowNotification("Connection failed Some actions were not loaded within timeout");
                    DisconnectFromSever();
                    return;
                }

                connectionStatus = newState;
                break;
            case ConnectionStatusEnum.Disconnected:
                connectionStatus = ConnectionStatusEnum.Disconnected;
               // OpenDisconnectedScreen();
              //  OnDisconnectedFromServer?.Invoke(this, EventArgs.Empty);
                Projects = new List<IO.Swagger.Model.ListProjectsResponseData>();
                Scenes = new List<IO.Swagger.Model.ListScenesResponseData>();

                HProjectManager.Instance.DestroyProject();
                SceneManagerH.Instance.DestroyScene();
                Scene.SetActive(false);
                break;
        }
    }


    /// <summary>
    /// Updates action objects and their actions from server
    /// </summary>
    /// <param name="highlightedObject">When set, object with this ID will gets highlighted for a few seconds in menu
    /// to inform user about it</param>
    /// <returns></returns>
    public async Task UpdateActionObjects() {
        try {
            List<IO.Swagger.Model.ObjectTypeMeta> objectTypeMetas = await WebSocketManagerH.Instance.GetObjectTypes();
            ActionsManagerH.Instance.UpdateObjects(objectTypeMetas);
        } catch (RequestFailedException ex) {
            Debug.LogError(ex);
            HNotificationManager.Instance.ShowNotification("Failed to update action objects");
            GameManagerH.Instance.DisconnectFromSever();
        }
        
    }

    
    /// <summary>
    /// Updates robot metadata from server
    /// </summary>
    /// <returns></returns>
    private async Task UpdateRobotsMeta() {
        ActionsManagerH.Instance.UpdateRobotsMetadata(await WebSocketManagerH.Instance.GetRobotMeta());
    }

    /// <summary>
    /// Checks if api version of the connected server is compatibile with editor
    /// </summary>
    /// <param name="systemInfo">Version string in format 0.0.0 (major.minor.patch)</param>
    /// <returns>True if versions are compatibile</returns>
    public bool CheckApiVersion(IO.Swagger.Model.SystemInfoResponseData systemInfo) {
        
        if (systemInfo.ApiVersion == ApiVersion)
            return true;

        if (GetMajorVersion(systemInfo.ApiVersion) != GetMajorVersion(ApiVersion) ||
            (GetMajorVersion(systemInfo.ApiVersion) == 0 && (GetMinorVersion(systemInfo.ApiVersion) != GetMinorVersion(ApiVersion)))) {
            HNotificationManager.Instance.ShowNotification("Incompatibile api versions Editor API version: " + ApiVersion + ", server API version: " + systemInfo.ApiVersion);
            return false;
        }
    //    Notifications.Instance.ShowNotification("Different api versions", "Editor API version: " + ApiVersion + ", server API version: " + systemInfo.ApiVersion + ". It can cause problems, you have been warned.");

        return true;
    }

    /// <summary>
    /// Parses version string and returns major version
    /// </summary>
    /// <param name="versionString">Version string in format 0.0.0 (major, minor, patch)</param>
    /// <returns>First number (major version)</returns>
    public int GetMajorVersion(string versionString) {
        return int.Parse(SplitVersionString(versionString)[0]);
    }

    
    /// <summary>
    /// Parses version string and returns minor version
    /// </summary>
    /// <param name="versionString">Version string in format 0.0.0 (major, minor, patch)</param>
    /// <returns>Second number (minor version)</returns>
    public int GetMinorVersion(string versionString) {
        return int.Parse(SplitVersionString(versionString)[1]);
    }


    /// <summary>
    /// Splits version string and returns list of components
    /// </summary>
    /// <param name="versionString">Version string in format 0.0.0 (major.minor.patch)</param>
    /// <returns>List of components of the version string</returns>
    public List<string> SplitVersionString(string versionString) {
        List<string> version = versionString.Split('.').ToList<string>();
        Debug.Assert(version.Count == 3, versionString);
        return version;
    }


    
    /// <summary>
    /// Opens scene editor
    /// </summary>
    public void OpenSceneEditor() {
#if !UNITY_EDITOR
             
        if (CalibrationManagerH.Instance.Calibrated) {
            Scene.SetActive(true);
        }
#else
      
        Scene.SetActive(true);
        
#endif
    
        SetGameState(GameStateEnum.SceneEditor);
        OnOpenSceneEditor?.Invoke(this, EventArgs.Empty);
        SetEditorState(EditorStateEnum.Normal);

        HideLoadingScreen();
    }

    
    /// <summary>
    /// Opens project editor
    /// </summary>
    public void OpenProjectEditor() {
#if !UNITY_EDITOR
        if (CalibrationManagerH.Instance.Calibrated) {
            Scene.SetActive(true);
        }
#else
        Scene.SetActive(true);
#endif
      
        SetGameState(GameStateEnum.ProjectEditor);
        OnOpenProjectEditor?.Invoke(this, EventArgs.Empty);
        SetEditorState(EditorStateEnum.Normal);
        HideLoadingScreen();
    }


    /// <summary>
    /// Returns editor state
    /// </summary>
    /// <returns>Editor state</returns>
    public EditorStateEnum GetEditorState() {
        return editorState;
    }


    /// <summary>
    /// Change editor state and enable / disable UI elements based on the new state
    /// and invoke corresponding event
    /// </summary>
    /// <param name="newState">New state</param>
    public void SetEditorState(EditorStateEnum newState) {
        editorState = newState;
   //     OnEditorStateChanged?.Invoke(this, new EditorStateEventArgs(newState));
    /*    switch (newState) {
           
        }*/
    }


    /// <summary>
    /// Opens main screen
    /// </summary>
    /// <param name="what">Defines what list should be displayed (scenes/projects/packages)</param>
    /// <param name="highlight">ID of element to highlight (e.g. when scene is closed, it is highlighted for a few seconds</param>
    /// <returns></returns>
    public async Task OpenMainScreen(ShowMainScreenData.WhatEnum what, string highlight) {

        Scene.SetActive(false);
        SetGameState(GameStateEnum.MainScreen);
        SetEditorState(EditorStateEnum.Closed);
        HideLoadingScreen();
    }

    /// <summary>
    /// Asks server to save scene
    /// </summary>
    /// <returns></returns>
    public async Task<IO.Swagger.Model.SaveSceneResponse> SaveScene() {
      //  ShowLoadingScreen("Saving scene...");
        IO.Swagger.Model.SaveSceneResponse response = await WebSocketManagerH.Instance.SaveScene();
     //   HideLoadingScreen();
        return response;
    }


    /// <summary>
    /// Create visual elements of opened scene and open scene editor
    /// </summary>
    /// <param name="scene">Scene desription from the server</param>
    /// <returns></returns>
    internal async Task SceneOpened(Scene scene) {
      //  SetGameState(GameStateEnum.LoadingScene);
        if (!ActionsManagerH.Instance.ActionsReady || !HActionObjectPickerMenu.Instance.Loaded) {
            newScene = scene;
            openScene = true;
            return;
        }
        try {
            if (await SceneManagerH.Instance.CreateScene(scene, true)) {   
                OpenSceneEditor();                    
            } else {
                Debug.LogError("Failed to initialize scene");
        //         HideLoadingScreen();
            }
        } catch (TimeoutException ex) {
            Debug.LogError(ex);
       //     HideLoadingScreen();
        } 
        

    }


    public void EnableInteractWthActionObjects(bool isEnable){
        RobotActionObjectH[] robots = Scene.GetComponentsInChildren<RobotActionObjectH>();
        ActionObject3DH[] models =   Scene.GetComponentsInChildren<ActionObject3DH>();
        foreach(RobotActionObjectH robot in robots){
            UrdfRobot urdfRobot = robot.RobotModel.RobotModelGameObject.GetComponentInChildren<UrdfRobot>();
            urdfRobot.SetCollidersConvex(isEnable);
        }
        foreach(ActionObject3DH model in models){
             BoxCollider collider =  model.Model.GetComponent<BoxCollider>();
             if(collider != null)
                collider.isTrigger = !isEnable;
        }
        
    }

    /// <summary>
    /// Create visual elements of opened scene and project and open project editor
    /// </summary>
    /// <param name="project">Project desription from the server</param>
    /// <returns></returns>
    internal async void ProjectOpened(Scene scene, Project project) {
        var state = GetGameState();
        if (!ActionsManagerH.Instance.ActionsReady || !HActionObjectPickerMenu.Instance.Loaded) {
            newProject = project;
            newScene = scene;
            openProject = true;
            return;
        }
        if (GetGameState() == GameStateEnum.SceneEditor) {
            SetEditorState(EditorStateEnum.InteractionDisabled);
            SceneManagerH.Instance.DestroyScene();
        }
    //    SetGameState(GameStateEnum.LoadingProject);
        try {
         
           // ListScenes.Instance.createMenuScene(scene,project);
            if (!await SceneManagerH.Instance.CreateScene(scene, true)) {
                HNotificationManager.Instance.ShowNotification("Failed to initialize scene Project CreateScene");
           
             //   HideLoadingScreen();
                return;
            }
            if (await HProjectManager.Instance.CreateProject(project, true)) {
                OpenProjectEditor();
            } else {
                HNotificationManager.Instance.ShowNotification("Failed to initialize scene Project CreateProject");

               // Notifications.Instance.SaveLogs(scene, project, "Failed to initialize project");
             //   HideLoadingScreen();
            }
        } catch (TimeoutException ex) {
            Debug.LogError(ex);
            HNotificationManager.Instance.ShowNotification("Failed to initialize scene Project TimeOut");
         //   HideLoadingScreen();
        }
    }




    /// <summary>
    /// Gets name of scene based on its ID
    /// </summary>
    /// <param name="sceneId">ID of scene</param>
    /// <returns>Name of scene</returns>
    public string GetSceneName(string sceneId) {
        foreach (ListScenesResponseData scene in Scenes) {
            if (scene.Id == sceneId)
                return scene.Name;
        }
        throw new ItemNotFoundException("Scene with id: " + sceneId + " not found");
    }



    /// <summary>
    /// Event called when request to open main screen come from server
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private async void OnShowMainScreen(object sender, ShowMainScreenEventArgs args) {
        if (ActionsManagerH.Instance.ActionsReady){
             await OpenMainScreen(args.Data.What, args.Data.Highlight);
        }
           
        else {
            openMainScreenRequest = true;
            openMainScreenData = args.Data;
        }
            
    }

    
    /// <summary>
    /// Callback when project was closed
    /// </summary>
    internal void ProjectClosed() {
        HNotificationManager.Instance.ShowNotification("CLOSE PROJECT");
        SetGameState(GameStateEnum.ClosingProject);
      //  ShowLoadingScreen();
        HProjectManager.Instance.DestroyProject();
        SceneManagerH.Instance.DestroyScene();
        OnCloseProject?.Invoke(this, EventArgs.Empty);
        SetGameState(GameStateEnum.None);
    }

    
    /// <summary>
    /// Callback when scene was closed
    /// </summary>
    internal void SceneClosed() {
         HNotificationManager.Instance.ShowNotification("CLOSE SCENE");
        SetGameState(GameStateEnum.ClosingScene);
      //  ShowLoadingScreen();
        SceneManagerH.Instance.DestroyScene();
        OnCloseScene?.Invoke(this, EventArgs.Empty);
        SetGameState(GameStateEnum.None);
    }

    /// <summary>
    /// Asks server to close currently opened scene
    /// </summary>
    /// <param name="force">True if the server should close scene with unsaved changes</param>
    /// <param name="dryRun">Only check if the scene could be closed without forcing</param>
    /// <returns>True if request was successfull. If not, message describing error is attached</returns>
    public async Task<RequestResult> CloseScene(bool force, bool dryRun = false) {
       
        try {
            await WebSocketManagerH.Instance.CloseScene(force, dryRun);
            return (true, "");
        } catch (RequestFailedException ex) {
            if (!dryRun && force) {
                HNotificationManager.Instance.ShowNotification("Failed to close scene");
             //   HideLoadingScreen();                   
            }
            return (false, ex.Message);
        }          
        
    }

    /// <summary>
    /// Asks server to close currently opened project
    /// </summary>
    /// <param name="force">True if the server should close project with unsaved changes</param>
    /// <param name="dryRun">Only check if the project could be closed without forcing</param>
    /// <returns></returns>
    public async Task<RequestResult> CloseProject(bool force, bool dryRun = false) {
    
        try {
            await WebSocketManagerH.Instance.CloseProject(force, dryRun: dryRun);
            return (true, "");
        } catch (RequestFailedException ex) {
            if (!dryRun && force) {
                HNotificationManager.Instance.ShowNotification("Failed to close project - " + ex.Message);
              //  HideLoadingScreen();
            }                
            return (false, ex.Message);
        }           
        
    }

    /// <summary>
    /// Returns current game state
    /// </summary>
    /// <returns>Current game state</returns>
    public GameStateEnum GetGameState() {
        return gameState;
    }

    /// <summary>
    /// Activates/Disactivates the Scene and calls all necessary methods (Selector menu update).
    /// </summary>
    /// <param name="active"></param>
    public void SceneSetActive(bool active) {
        Scene.SetActive(active);
    }

    public void SceneSetParent(Transform transform){
        Scene.transform.parent = transform;
        Scene.transform.localPosition = new Vector3(0f, 0f, 0f);
        Scene.transform.localEulerAngles =  new Vector3(0f, 90f, 90f);
    }


    public void InvokeScenesListChanged() {
        OnScenesListChanged?.Invoke(this, EventArgs.Empty);
    }

    public void InvokeProjectsListChanged() {
        OnProjectsListChanged?.Invoke(this, EventArgs.Empty);
    }
}
