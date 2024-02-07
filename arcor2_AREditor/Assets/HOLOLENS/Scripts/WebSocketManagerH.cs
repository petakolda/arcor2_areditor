using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json;
using System.Threading.Tasks;
using NativeWebSocket;
using IO.Swagger.Model;
using UnityEditor;
using UnityEngine.Events;
using Base;

public class WebSocketManagerH : Singleton<WebSocketManagerH> {


    /// <summary>
    /// ARServer URI
    /// </summary>
    public string APIDomainWS = "";
    // Start is called before the first frame update
    /// <summary>
    /// Websocket context
    /// </summary>
    private WebSocket websocket;

    /// <summary>
    /// Requset id pool
    /// </summary>
    private int requestID = 1;

    /// <summary>
    /// Invoked when connected to server
    /// </summary>
    public event EventHandler OnConnectedEvent;
    /// <summary>
    /// Invoked when disconnected from server.
    /// </summary>
    public event EventHandler OnDisconnectEvent;

    /// <summary>
    /// Dictionary of unprocessed responses
    /// </summary>
    private Dictionary<int, string> responses = new Dictionary<int, string>();
    private Dictionary<int, Tuple<string, UnityAction<string, string>>> responsesCallback = new Dictionary<int, Tuple<string, UnityAction<string, string>>>();
    /// <summary>
    /// Invoked when new end effector pose recieved from server. Contains eef pose.
    /// </summary>
    public event AREditorEventArgs.RobotEefUpdatedEventHandler OnRobotEefUpdated;
    /// <summary>
    /// Invoked when new joints values recieved from server. Contains joints values.
    /// </summary>
    public event AREditorEventArgs.RobotJointsUpdatedEventHandler OnRobotJointsUpdated;

    public event AREditorEventArgs.StringListEventHandler OnObjectTypeRemoved;
    public event AREditorEventArgs.ObjectTypesHandler OnObjectTypeAdded;
    public event AREditorEventArgs.ObjectTypesHandler OnObjectTypeUpdated;
    public event AREditorEventArgs.SceneStateHandler OnSceneStateEvent;

    public event AREditorEventArgs.ProjectActionPointEventHandler OnActionPointAdded;
    public event AREditorEventArgs.ProjectActionPointEventHandler OnActionPointUpdated;
    public event AREditorEventArgs.BareActionPointEventHandler OnActionPointBaseUpdated;
    public event AREditorEventArgs.StringEventHandler OnActionPointRemoved;

    public event AREditorEventArgs.ActionPointOrientationEventHandler OnActionPointOrientationAdded;
    public event AREditorEventArgs.ActionPointOrientationEventHandler OnActionPointOrientationUpdated;
    public event AREditorEventArgs.ActionPointOrientationEventHandler OnActionPointOrientationBaseUpdated;
    public event AREditorEventArgs.StringEventHandler OnActionPointOrientationRemoved;
    /// <summary>
    /// Invoked when action item added. Contains info about the logic item.
    /// </summary>
    public event AREditorEventArgs.LogicItemChangedEventHandler OnLogicItemAdded;
    /// <summary>
    /// Invoked when logic item removed. Contains UUID of removed item.
    /// </summary>
    public event AREditorEventArgs.StringEventHandler OnLogicItemRemoved;
    /// <summary>
    /// Invoked when logic item updated. Contains info of updated logic item. 
    /// </summary>
    public event AREditorEventArgs.LogicItemChangedEventHandler OnLogicItemUpdated;
    /// <summary>
    /// Invoked when main screen should be opened. Contains info of which list (scenes, projects, packages)
    /// should be opened and which tile should be highlighted.
    /// </summary>
    public event AREditorEventArgs.ShowMainScreenEventHandler OnShowMainScreen;

    /// <summary>
    /// ARServer domain or IP address
    /// </summary>
    private string serverDomain;

    /// <summary>
    /// Create websocket URI from domain name and port
    /// </summary>
    /// <param name="domain">Domain name or IP address</param>
    /// <param name="port">Server port</param>
    /// <returns></returns>
    public string GetWSURI(string domain, int port) {
        return "ws://" + domain + ":" + port.ToString();
    }

    /// <summary>
    /// Callbeck when connection to the server is closed
    /// </summary>
    /// <param name="closeCode"></param>
    private void OnClose(WebSocketCloseCode closeCode) {
        Debug.Log("Connection closed!");
        //    CleanupAfterDisconnect();
        OnDisconnectEvent?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Callback when some connection error occures
    /// </summary>
    /// <param name="errorMsg"></param>
    private void OnError(string errorMsg) {
        Debug.LogError(errorMsg);
    }
    /// <summary>
    /// Callback when connected to the server
    /// </summary>
    private void OnConnected() {
        Debug.Log("On connected");
        OnConnectedEvent?.Invoke(this, EventArgs.Empty);
    }


    /// <summary>
    /// Disconnects from server
    /// </summary>
    async public void DisconnectFromSever() {
        Debug.Log("Disconnecting");
        GameManagerH.Instance.ConnectionStatus = GameManagerH.ConnectionStatusEnum.Disconnected;
        try {
            await websocket.Close();
        } catch (WebSocketException e) {
            //already closed probably..
        }
    }

    /// <summary>
    /// Tries to connect to server
    /// </summary>
    /// <param name="domain">Domain name or IP address of server</param>
    /// <param name="port">Server port</param>
    public async void ConnectToServer(string domain, int port) {
        Debug.Log("connectToServer called");

        GameManagerH.Instance.ConnectionStatus = GameManagerH.ConnectionStatusEnum.Connecting;
        try {
            APIDomainWS = GetWSURI(domain, port);
            websocket = new WebSocket(APIDomainWS);
            serverDomain = domain;

            websocket.OnOpen += OnConnected;
            websocket.OnError += OnError;
            websocket.OnClose += OnClose;
            websocket.OnMessage += HandleReceivedData;

            await websocket.Connect();
        } catch (UriFormatException ex) {
            Debug.LogError(ex);

            //     Notifications.Instance.ShowNotification("Failed to parse domain", ex.Message);
            GameManagerH.Instance.ConnectionStatus = GameManagerH.ConnectionStatusEnum.Disconnected;
            GameManagerH.Instance.HideLoadingScreen(true);
        }
    }
    // Update is called once per frame
    void Update() {
        if (websocket != null && websocket.State == WebSocketState.Open)
            websocket.DispatchMessageQueue();
    }


    /// <summary>
    /// Returns the URI of asset service
    /// </summary>
    /// <returns></returns>
    public string GetAssetServiceURI() {
        string uri = PlayerPrefsHelper.LoadString("AssetServiceURI", "");

        // TODO this could (should?) work without connection to the server
        Debug.Assert(!string.IsNullOrEmpty(GetServerDomain()), "GetAssetServiceURI was probably used without connection to the server.");

        if (string.IsNullOrEmpty(uri))
            return "http://" + GetServerDomain() + ":6790";
        else {
            return uri.Trim('/');
        }
    }

    public string GetAssetFileURI(string file_id) {
        return $"{GetAssetServiceURI()}/assets/{file_id}/data";
    }


    /// <summary>
    /// Method for parsing recieved message and invoke proper callback
    /// </summary>
    /// <param name="message">Recieved message</param>
    private async void HandleReceivedData(byte[] message) {
        string data = Encoding.Default.GetString(message);
        var dispatchType = new {
            id = 0,
            response = "",
            @event = "",
            request = ""
        };
        var dispatch = JsonConvert.DeserializeAnonymousType(data, dispatchType);

        if (dispatch?.response == null && dispatch?.request == null && dispatch?.@event == null)
            return;
        if (dispatch?.@event == null || (dispatch?.@event != "RobotEef" && dispatch?.@event != "RobotJoints"))
            Debug.Log("Recieved new data: " + data);
        if (dispatch.response != null) {

            if (responses.ContainsKey(dispatch.id)) {
                responses[dispatch.id] = data;
            } else if (responsesCallback.TryGetValue(dispatch.id, out Tuple<string, UnityAction<string, string>> callbackData)) {
                callbackData.Item2.Invoke(callbackData.Item1, data);
            }

        } else if (dispatch.@event != null) {
            switch (dispatch.@event) {
                case "SceneClosed":
                    HandleCloseScene(data);
                    break;
                case "SceneObjectChanged":
                    HandleSceneObjectChanged(data);
                    break;
                case "ProjectClosed":
                    HandleCloseProject(data);
                    break;
                case "ObjectsLocked":
                    HandleObjectLocked(data);
                    break;
                case "ChangedObjectTypes":
                    HandleChangedObjectTypesEvent(data);
                    break;
                case "ActionPointChanged":
                    HandleActionPointChanged(data);
                    break;
                case "ActionChanged":
                    HandleActionChanged(data);
                    break;
                case "SceneState":
                    HandleSceneState(data);
                    break;
                case "RobotEef":
                    HandleRobotEef(data);
                    break;
                case "RobotJoints":
                    HandleRobotJoints(data);
                    break;
                case "ShowMainScreen":
                    HandleShowMainScreen(data);
                    break;
                case "OpenScene":
                    await HandleOpenScene(data);
                    break;
                case "OpenProject":
                    HandleOpenProject(data);
                    break;
                case "LogicItemChanged":
                    HandleLogicItemChanged(data);
                    break;
                case "ObjectsUnlocked":
                    HandleObjectUnlocked(data);
                    break;
                case "OrientationChanged":
                    HandleOrientationChanged(data);
                    break;
                    /*  case "PackageState":
                          HandlePackageState(data);
                          break;
                      case "PackageInfo":
                          HandlePackageInfo(data);
                          break;*/

            }
        }

    }


    /// <summary>
    /// Register or unregister to/from subsription of robots joints or end effectors pose.
    /// </summary>
    /// <param name="robotId">ID of robot</param>
    /// <param name="send">To subscribe or to unsubscribe</param>
    /// <param name="what">Pose of end effectors or joints</param>
    /// <returns>True if request successfull, false otherwise</returns>
    public async Task<bool> RegisterForRobotEvent(string robotId, bool send, IO.Swagger.Model.RegisterForRobotEventRequestArgs.WhatEnum what) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.RegisterForRobotEventRequestArgs args = new IO.Swagger.Model.RegisterForRobotEventRequestArgs(robotId: robotId, send: send, what: what);
        IO.Swagger.Model.RegisterForRobotEventRequest request = new IO.Swagger.Model.RegisterForRobotEventRequest(r_id, "RegisterForRobotEvent", args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.RegisterForRobotEventResponse response = await WaitForResult<IO.Swagger.Model.RegisterForRobotEventResponse>(r_id);

        // TODO: is this correct?
        return response == null ? false : response.Result;
    }

    /// <summary>
    /// Universal method for sending data to server
    /// </summary>
    /// <param name="data">String to send</param>
    /// <param name="key">ID of request (used to obtain result)</param>
    /// <param name="storeResult">Flag whether or not store result</param>
    /// <param name="logInfo">Flag whether or not log sended message</param>
    public void SendDataToServer(string data, int key = -1, bool storeResult = false, bool logInfo = true) {
        if (key < 0) {
            key = Interlocked.Increment(ref requestID);
        }


        if (storeResult) {
            responses[key] = null;
        }
        SendWebSocketMessage(data, logInfo);
    }


    /// <summary>
    /// Sends data to server
    /// </summary>
    /// <param name="data"></param>
    private async void SendWebSocketMessage(string data, bool logInfo) {
        try {
            if (websocket.State == WebSocketState.Open) {
                await websocket.SendText(data);
                if (logInfo)
                    Debug.Log("Sent data to server: " + data);
            }
        } catch (WebSocketException ex) {
            Debug.Log("socketexception in sendwebsocketmessage: " + ex.Message);


        }
    }

    /// <summary>
    /// Invoked when openning of scene is requested
    /// </summary>
    /// <param name="data"Message from server></param>
    /// <returns></returns>
    private async Task HandleOpenScene(string data) {
        IO.Swagger.Model.OpenScene openSceneEvent = JsonConvert.DeserializeObject<IO.Swagger.Model.OpenScene>(data);
        await GameManagerH.Instance.SceneOpened(openSceneEvent.Data.Scene);
    }

    private void HandleSceneState(string obj) {
        SceneState sceneState = JsonConvert.DeserializeObject<SceneState>(obj);
        OnSceneStateEvent?.Invoke(this, new SceneStateEventArgs(sceneState.Data));
    }

    /// <summary>
    /// Invoked when openning of project is requested
    /// </summary>
    /// <param name="data">Message from server</param>
    private async void HandleOpenProject(string data) {
        IO.Swagger.Model.OpenProject openProjectEvent = JsonConvert.DeserializeObject<IO.Swagger.Model.OpenProject>(data);
        GameManagerH.Instance.ProjectOpened(openProjectEvent.Data.Scene, openProjectEvent.Data.Project);
    }


    private void HandleShowMainScreen(string data) {
        IO.Swagger.Model.ShowMainScreen showMainScreenEvent = JsonConvert.DeserializeObject<IO.Swagger.Model.ShowMainScreen>(data);
        OnShowMainScreen?.Invoke(this, new ShowMainScreenEventArgs(showMainScreenEvent.Data));
    }

    /// <summary>
    /// Invoked when closing of scene is requested
    /// </summary>
    /// <param name="data">Message from server</param>
    private void HandleCloseScene(string data) {
        GameManagerH.Instance.SceneClosed();
    }

    /// <summary>
    /// Invoked when closing of project is requested
    /// </summary>
    /// <param name="data">Message from server</param>
    private void HandleCloseProject(string data) {
        GameManagerH.Instance.ProjectClosed();
    }

    /// <summary>
    /// Loads actions for selected object type. Throws RequestFailedException when request failed
    /// </summary>
    /// <param name="name">Object type</param>
    /// <returns>List of actions</returns>
    public void GetActions(string name, UnityAction<string, string> callback) {
        int id = Interlocked.Increment(ref requestID);
        responsesCallback.Add(id, Tuple.Create(name, callback));
        SendDataToServer(new IO.Swagger.Model.GetActionsRequest(id: id, request: "GetActions", args: new IO.Swagger.Model.TypeArgs(type: name)).ToJson(), id, false);
    }


    public async Task<List<string>> GetEndEffectors(string robotId, string armId = null) {
        int r_id = Interlocked.Increment(ref requestID);
        GetEndEffectorsRequestArgs args = new GetEndEffectorsRequestArgs(robotId: robotId, armId: armId);
        IO.Swagger.Model.GetEndEffectorsRequest request = new IO.Swagger.Model.GetEndEffectorsRequest(r_id, "GetEndEffectors", args: args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.GetEndEffectorsResponse response = await WaitForResult<IO.Swagger.Model.GetEndEffectorsResponse>(r_id);
        if (response == null || !response.Result) {
            HNotificationManager.Instance.ShowNotification("Failed to get robot end effectors /n");
            throw new RequestFailedException(response == null ? new List<string>() { "Failed to get robot end effectors" } : response.Messages);
        } else {
            return response.Data;
        }
    }

    /// <summary>
    /// Asks server to remove object from scene.
    /// 
    /// </summary>
    /// <param name="id">ID of action object</param>
    /// <param name="force">Indicates whether or not it should be forced</param>
    /// <returns>Response from server</returns>
    public async Task<IO.Swagger.Model.RemoveFromSceneResponse> RemoveFromScene(string id, bool force, bool dryRun) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.RemoveFromSceneRequestArgs args = new IO.Swagger.Model.RemoveFromSceneRequestArgs(id: id, force: force);
        IO.Swagger.Model.RemoveFromSceneRequest request = new IO.Swagger.Model.RemoveFromSceneRequest(id: r_id, request: "RemoveFromScene", args: args, dryRun: dryRun);
        SendDataToServer(request.ToJson(), r_id, true, logInfo: !dryRun);
        return await WaitForResult<IO.Swagger.Model.RemoveFromSceneResponse>(r_id);
    }

    public void StopScene(bool dryRun, UnityAction<string, string> callback) {
        int id = Interlocked.Increment(ref requestID);
        if (callback != null)
            responsesCallback.Add(id, Tuple.Create("", callback));
        IO.Swagger.Model.StopSceneRequest request = new IO.Swagger.Model.StopSceneRequest(id, "StopScene", dryRun: dryRun);
        SendDataToServer(request.ToJson(), id, false);
    }


    /// <summary>
    /// Asks server to close opened scene.
    /// Throws RequestFailedException when request failed
    /// </summary>
    /// <param name="force">Indicates whether the scene should be closed even when it has unsaved changes.</param>
    /// <param name="dryRun">If true, validates all parameters, but will not execute requested action itself.</param>
    /// <returns></returns>
    public async Task CloseScene(bool force, bool dryRun = false) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.CloseSceneRequestArgs args = new IO.Swagger.Model.CloseSceneRequestArgs(force);
        IO.Swagger.Model.CloseSceneRequest request = new IO.Swagger.Model.CloseSceneRequest(r_id, "CloseScene", args, dryRun);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.CloseSceneResponse response = await WaitForResult<IO.Swagger.Model.CloseSceneResponse>(r_id);
        if (response == null || !response.Result)
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
    }

    /// <summary>
    /// Asks server to close currently opened project.
    /// Throws RequestFailedException when request failed
    /// </summary>
    /// <param name="force">Indicates if it should be closed even with unsaved changes.</param>
    /// <param name="dryRun">If true, validates all parameters, but will not execute requested action itself.</param>
    /// <returns></returns>
    public async Task CloseProject(bool force, bool dryRun = false) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.CloseProjectRequestArgs args = new IO.Swagger.Model.CloseProjectRequestArgs(force);
        IO.Swagger.Model.CloseProjectRequest request = new IO.Swagger.Model.CloseProjectRequest(r_id, "CloseProject", args, dryRun: dryRun);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.CloseProjectResponse response = await WaitForResult<IO.Swagger.Model.CloseProjectResponse>(r_id);

        if (response == null || !response.Result)
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
    }
    public async Task<List<string>> GetRobotArms(string robotId) {
        int r_id = Interlocked.Increment(ref requestID);
        GetRobotArmsRequestArgs args = new GetRobotArmsRequestArgs(robotId: robotId);
        IO.Swagger.Model.GetRobotArmsRequest request = new IO.Swagger.Model.GetRobotArmsRequest(r_id, "GetRobotArms", args: args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.GetRobotArmsResponse response = await WaitForResult<IO.Swagger.Model.GetRobotArmsResponse>(r_id);
        if (response == null || !response.Result) {
            throw new RequestFailedException(response == null ? new List<string>() { "Failed to get robot arms" } : response.Messages);
        } else {
            return response.Data;
        }
    }

    public async Task WriteLock(string objId, bool lockTree) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.WriteLockRequestArgs args = new WriteLockRequestArgs(lockTree: lockTree, objectId: objId);

        IO.Swagger.Model.WriteLockRequest request = new IO.Swagger.Model.WriteLockRequest(r_id, "WriteLock", args: args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.WriteLockResponse response = await WaitForResult<IO.Swagger.Model.WriteLockResponse>(r_id);
        if (response == null || !response.Result) {
            throw new RequestFailedException(response == null ? new List<string>() { "Failed to lock object" } : response.Messages);
        }
    }

    /// <summary>
    /// Invoked when an object was locked
    /// </summary>
    /// <param name="data">Message from server</param>
    private void HandleObjectLocked(string data) {
        ObjectsLocked objectsLockedEvent = JsonConvert.DeserializeObject<ObjectsLocked>(data);
        HLockingEventCache.Instance.Add(new ObjectLockingEventArgs(objectsLockedEvent.Data.ObjectIds, true, objectsLockedEvent.Data.Owner));
    }

    /// <summary>
    /// Invoked when an object was unlocked
    /// </summary>
    /// <param name="data">Message from server</param>
    private void HandleObjectUnlocked(string data) {
        IO.Swagger.Model.ObjectsUnlocked objectsUnlockedEvent = JsonConvert.DeserializeObject<ObjectsUnlocked>(data);
        HLockingEventCache.Instance.Add(new ObjectLockingEventArgs(objectsUnlockedEvent.Data.ObjectIds, false, objectsUnlockedEvent.Data.Owner));
    }

    /// <summary>
    /// Updates position and orientation of action object.
    /// Throws RequestFailedException when request failed
    /// </summary>
    /// <param name="actionObjectId">Id of action object</param>
    /// <param name="pose">Desired pose (position and orientation)</param>
    /// <returns></returns>
    public async Task UpdateActionObjectPose(string actionObjectId, IO.Swagger.Model.Pose pose, bool dryRun = false) {
        if (dryRun)
            return;
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.UpdateObjectPoseRequestArgs args = new IO.Swagger.Model.UpdateObjectPoseRequestArgs
            (objectId: actionObjectId, pose: pose);
        IO.Swagger.Model.UpdateObjectPoseRequest request = new IO.Swagger.Model.UpdateObjectPoseRequest
            (id: r_id, request: "UpdateObjectPose", args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.UpdateObjectPoseResponse response = await WaitForResult<IO.Swagger.Model.UpdateObjectPoseResponse>(r_id);
        if (response == null || !response.Result)
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);

    }

    /// <summary>
    /// Removes project parameter
    /// </summary>
    /// <param name="id">ID of project parameter to remove</param>
    /// <param name="dryRun"></param>
    /// <returns></returns>
    public async Task UpdateObjectModel(string id, ObjectModel objectModel, bool dryRun = false) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.UpdateObjectModelRequestArgs args = new UpdateObjectModelRequestArgs(objectModel: objectModel, objectTypeId: id);

        IO.Swagger.Model.UpdateObjectModelRequest request = new IO.Swagger.Model.UpdateObjectModelRequest(r_id, "UpdateObjectModel", args: args, dryRun: dryRun);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.UpdateObjectModelResponse response = await WaitForResult<IO.Swagger.Model.UpdateObjectModelResponse>(r_id);
        if (response == null || !response.Result) {
            throw new RequestFailedException(response == null ? new List<string>() { "Failed to update object model" } : response.Messages);
        }
    }

    /// <summary>
    /// Decodes changes on scene objects
    /// </summary>
    /// <param name="data">Message from server</param>
    /// <returns></returns>
    private void HandleSceneObjectChanged(string data) {
        IO.Swagger.Model.SceneObjectChanged sceneObjectChanged = JsonConvert.DeserializeObject<IO.Swagger.Model.SceneObjectChanged>(data);
        switch (sceneObjectChanged.ChangeType) {
            case IO.Swagger.Model.SceneObjectChanged.ChangeTypeEnum.Add:
                SceneManagerH.Instance.SceneObjectAdded(sceneObjectChanged.Data);
                break;
            case IO.Swagger.Model.SceneObjectChanged.ChangeTypeEnum.Remove:
                SceneManagerH.Instance.SceneObjectRemoved(sceneObjectChanged.Data);
                break;
            case IO.Swagger.Model.SceneObjectChanged.ChangeTypeEnum.Update:
                SceneManagerH.Instance.SceneObjectUpdated(sceneObjectChanged.Data);
                break;
            case IO.Swagger.Model.SceneObjectChanged.ChangeTypeEnum.Updatebase:
                SceneManagerH.Instance.SceneObjectBaseUpdated(sceneObjectChanged.Data);
                break;
            default:
                throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Asks server to add new logic item (actions connection).
    /// Throws RequestFailedException when request failed
    /// </summary>
    /// <param name="startActionId">UUID of first action (from)</param>
    /// <param name="endActionId">UUID of second action (to)</param>
    /// <param name="dryRun">If true, validates all parameters, but will not execute requested action itself.</param>
    /// <returns></returns>
    public async Task AddLogicItem(string startActionId, string endActionId, IO.Swagger.Model.ProjectLogicIf condition, bool dryRun) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.AddLogicItemRequestArgs args = new IO.Swagger.Model.AddLogicItemRequestArgs(start: startActionId, end: endActionId, condition: condition);
        IO.Swagger.Model.AddLogicItemRequest request = new IO.Swagger.Model.AddLogicItemRequest(r_id, "AddLogicItem", args, dryRun: dryRun);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.AddLogicItemResponse response = await WaitForResult<IO.Swagger.Model.AddLogicItemResponse>(r_id);

        if (response == null || !response.Result)
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
    }


    public async Task WriteUnlock(string objId) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.WriteUnlockRequestArgs args = new WriteUnlockRequestArgs(objectId: objId);

        IO.Swagger.Model.WriteUnlockRequest request = new IO.Swagger.Model.WriteUnlockRequest(r_id, "WriteUnlock", args: args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.WriteUnlockResponse response = await WaitForResult<IO.Swagger.Model.WriteUnlockResponse>(r_id);
        if (response == null || !response.Result) {
            throw new RequestFailedException(response == null ? new List<string>() { "Failed to unlock object" } : response.Messages);
        }
    }
    /// <summary>
    /// If connected to server, returns domain name or IP addr of server
    /// </summary>
    /// <returns>Domain name or IP addr of server</returns>
    public string GetServerDomain() {
        if (websocket.State != WebSocketState.Open) {
            return null;
        }
        return serverDomain;
    }


    /// <summary>
    /// Gets available values for selected parameter.
    /// </summary>
    /// <param name="actionProviderId">ID of action provider (only action object at the moment"</param>
    /// <param name="param_id">ID of parameter</param>
    /// <param name="parent_params">List of parent parameters (e.g. to obtain list of available end effectors, robot_id has to be provided"</param>
    /// <returns>List of available options or empty list when request failed</returns>
    public async Task<List<string>> GetActionParamValues(string actionProviderId, string param_id, List<IO.Swagger.Model.IdValue> parent_params) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.ActionParamValuesRequestArgs args = new IO.Swagger.Model.ActionParamValuesRequestArgs(id: actionProviderId, paramId: param_id, parentParams: parent_params);
        IO.Swagger.Model.ActionParamValuesRequest request = new IO.Swagger.Model.ActionParamValuesRequest(id: r_id, request: "ActionParamValues", args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.ActionParamValuesResponse response = await WaitForResult<IO.Swagger.Model.ActionParamValuesResponse>(r_id);
        if (response != null && response.Result)
            return response.Data;
        else
            return new List<string>();
    }

    /// <summary>
    /// Waits until response with selected ID is recieved.
    /// </summary>
    /// <typeparam name="T">Type of reposnse</typeparam>
    /// <param name="key">ID of response</param>
    /// <param name="timeout">Time [ms] after which timeout exception is thrown</param>
    /// <returns>Decoded response</returns>
    private Task<T> WaitForResult<T>(int key, int timeout = 15000) {
        return Task.Run(() => {
            if (responses.TryGetValue(key, out string value)) {
                if (value == null) {
                    Task<string> result = WaitForResponseReady(key, timeout);
                    if (!result.Wait(timeout)) {
                        Debug.LogError("The timeout interval elapsed.");
                        //TODO: throw an exception and handle it properly
                        //throw new TimeoutException();
                        return default;
                    } else {
                        value = result.Result;
                    }
                }
                return JsonConvert.DeserializeObject<T>(value);
            } else {
                return default;
            }
        });
    }



    /// <summary>
    /// Loads all projects from server.
    /// Throws RequestFailedException when request failed
    /// </summary>
    /// <returns>List of projects metadata</returns>
    public void LoadProjects(UnityAction<string, string> callback) {
        int r_id = Interlocked.Increment(ref requestID);
        responsesCallback.Add(r_id, Tuple.Create("", callback));
        IO.Swagger.Model.ListProjectsRequest request = new IO.Swagger.Model.ListProjectsRequest(id: r_id, request: "ListProjects");
        SendDataToServer(request.ToJson(), r_id, false);

    }

    public void LoadScenes(UnityAction<string, string> callback) {
        int r_id = Interlocked.Increment(ref requestID);
        responsesCallback.Add(r_id, Tuple.Create("", callback));
        IO.Swagger.Model.ListScenesRequest request = new IO.Swagger.Model.ListScenesRequest(id: r_id, request: "ListScenes");
        SendDataToServer(request.ToJson(), r_id, false);
    }

    /// <summary>
    /// Asks server to open scene
    /// </summary>
    /// <param name="scene_id">Id of scene to be openned</param>
    /// <returns></returns>
    public async Task<IO.Swagger.Model.Project> GetProject(string project_id) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.IdArgs args = new IO.Swagger.Model.IdArgs(id: project_id);
        IO.Swagger.Model.GetProjectRequest request = new IO.Swagger.Model.GetProjectRequest(id: r_id, request: "GetProject", args: args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.GetProjectResponse response = await WaitForResult<IO.Swagger.Model.GetProjectResponse>(r_id, 30000);
        if (response == null || !response.Result) {
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
        }
        return response.Data;
    }

    /// <summary>
    /// Asks server to open scene
    /// </summary>
    /// <param name="scene_id">Id of scene to be openned</param>
    /// <returns></returns>
    public async Task<IO.Swagger.Model.Scene> GetScene(string scene_id) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.IdArgs args = new IO.Swagger.Model.IdArgs(id: scene_id);
        IO.Swagger.Model.GetSceneRequest request = new IO.Swagger.Model.GetSceneRequest(id: r_id, request: "GetScene", args: args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.GetSceneResponse response = await WaitForResult<IO.Swagger.Model.GetSceneResponse>(r_id, 30000);
        if (response == null || !response.Result) {
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
        }
        return response.Data;
    }

    // TODO: add timeout!
    /// <summary>
    /// WWaits until response with selected ID is recieved.
    /// </summary>
    /// <param name="key">ID of reponse</param>
    /// <param name="timeout">Not used</param>
    /// <returns>Raw response</returns>
    private Task<string> WaitForResponseReady(int key, int timeout) {
        return Task.Run(() => {
            while (true) {
                if (responses.TryGetValue(key, out string value)) {
                    if (value != null) {
                        return value;
                    } else {
                        Thread.Sleep(10);
                    }
                }
            }
        });

    }

    /// <summary>
    /// Gets metadata about robots
    /// </summary>
    /// <returns>List of metadatas of robots</returns>
    public async Task<List<IO.Swagger.Model.RobotMeta>> GetRobotMeta() {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.GetRobotMetaRequest request = new IO.Swagger.Model.GetRobotMetaRequest(r_id, "GetRobotMeta");
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.GetRobotMetaResponse response = await WaitForResult<IO.Swagger.Model.GetRobotMetaResponse>(r_id);
        if (response == null || !response.Result) {
            HNotificationManager.Instance.ShowNotification("Failed to get robot meta");
            throw new RequestFailedException(response == null ? new List<string>() { "Failed to get robot meta" } : response.Messages);
        } else {
            return response.Data;
        }
    }


    /// <summary>
    /// Gets information about server (server version, api version, list of supported parameters and list of available RPCs).
    /// Throws RequestFailedException when request failed
    /// </summary>
    /// <returns>Information about server</returns>
    public async Task<IO.Swagger.Model.SystemInfoResponseData> GetSystemInfo() {
        int r_id = Interlocked.Increment(ref requestID);

        IO.Swagger.Model.SystemInfoRequest request = new IO.Swagger.Model.SystemInfoRequest(id: r_id, request: "SystemInfo");
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.SystemInfoResponse response = await WaitForResult<IO.Swagger.Model.SystemInfoResponse>(r_id);
        if (response == null || !response.Result)
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
        return response.Data;
    }


    public async Task RegisterUser(string username) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.RegisterUserRequestArgs args = new RegisterUserRequestArgs(username);
        IO.Swagger.Model.RegisterUserRequest request = new RegisterUserRequest(r_id, "RegisterUser", args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.RegisterUserResponse response = await WaitForResult<IO.Swagger.Model.RegisterUserResponse>(r_id);
        if (response == null || !response.Result) {
            throw new RequestFailedException(response == null ? new List<string>() { "Failed to register user" } : response.Messages);
        }
    }

    /// <summary>
    /// Loads object types from server. Throws RequestFailedException when request failed
    /// </summary>
    /// <returns>List of object types</returns>
    public async Task<List<IO.Swagger.Model.ObjectTypeMeta>> GetObjectTypes() {
        int id = Interlocked.Increment(ref requestID);
        SendDataToServer(new IO.Swagger.Model.GetObjectTypesRequest(id: id, request: "GetObjectTypes").ToJson(), id, true);
        IO.Swagger.Model.GetObjectTypesResponse response = await WaitForResult<IO.Swagger.Model.GetObjectTypesResponse>(id);
        if (response != null && response.Result)
            return response.Data;
        else {
            throw new RequestFailedException("Failed to load object types");
        }

    }

    /// <summary>
    /// Asks server to rename action object.
    /// Throws RequestFailedException when request failed
    /// </summary>
    /// <param name="id">UUID of action object</param>
    /// <param name="newName">New human readable name of action objects</param>
    /// <returns></returns>
    public async Task RenameObject(string id, string newName) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.RenameArgs args = new IO.Swagger.Model.RenameArgs(id: id, newName: newName);
        IO.Swagger.Model.RenameObjectRequest request = new IO.Swagger.Model.RenameObjectRequest(r_id, "RenameObject", args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.RenameObjectResponse response = await WaitForResult<IO.Swagger.Model.RenameObjectResponse>(r_id);

        if (response == null || !response.Result)
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
    }

    /// <summary>
    /// Asks server to open scene
    /// </summary>
    /// <param name="scene_id">Id of scene to be openned</param>
    /// <returns></returns>
    public async Task OpenScene(string scene_id) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.IdArgs args = new IO.Swagger.Model.IdArgs(id: scene_id);
        IO.Swagger.Model.OpenSceneRequest request = new IO.Swagger.Model.OpenSceneRequest(id: r_id, request: "OpenScene", args: args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.OpenSceneResponse response = await WaitForResult<IO.Swagger.Model.OpenSceneResponse>(r_id, 30000);
        if (response == null || !response.Result) {
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
        }
    }


    /// <summary>
    /// Asks server to open project. Throws RequestFailedException when request failed
    /// </summary>
    /// <param name="id">Id of project</param>
    /// <returns></returns>
    public async Task OpenProject(string id) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.IdArgs args = new IO.Swagger.Model.IdArgs(id: id);
        IO.Swagger.Model.OpenProjectRequest request = new IO.Swagger.Model.OpenProjectRequest(id: r_id, request: "OpenProject", args);
        Debug.Log("PROJEEECT: " + request.ToJson());
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.OpenProjectResponse response = await WaitForResult<IO.Swagger.Model.OpenProjectResponse>(r_id, 30000);
        if (response == null || !response.Result)
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
    }

    /// <summary>
    /// Removes project parameter
    /// </summary>
    /// <param name="id">ID of project parameter to remove</param>
    /// <param name="dryRun"></param>
    /// <returns></returns>
    public Task AddVirtualCollisionObjectToScene(string name, ObjectModel objectModel, IO.Swagger.Model.Pose pose, UnityAction<string, string> callback, bool dryRun = false) {
        Debug.Assert(callback != null);
        int r_id = Interlocked.Increment(ref requestID);
        responsesCallback.Add(r_id, Tuple.Create("", callback));
        AddVirtualCollisionObjectToSceneRequestArgs args = new AddVirtualCollisionObjectToSceneRequestArgs(model: objectModel, name: name, pose: pose);
        AddVirtualCollisionObjectToSceneRequest request = new AddVirtualCollisionObjectToSceneRequest(r_id, "AddVirtualCollisionObjectToScene", args: args, dryRun: dryRun);
        SendDataToServer(request.ToJson(), r_id, false);
        return Task.CompletedTask;
    }


    /// <summary>
    /// Asks server to update action point position.
    /// Throws RequestFailedException when request failed
    /// </summary>
    /// <param name="id">UUID of action point</param>
    /// <param name="position">New position of action point.</param>
    /// <returns></returns>
    public async Task UpdateActionPointPosition(string id, IO.Swagger.Model.Position position, bool dryRun = false) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.UpdateActionPointPositionRequestArgs args = new IO.Swagger.Model.UpdateActionPointPositionRequestArgs(actionPointId: id, newPosition: position);
        IO.Swagger.Model.UpdateActionPointPositionRequest request = new IO.Swagger.Model.UpdateActionPointPositionRequest(r_id, "UpdateActionPointPosition", args, dryRun: dryRun);
        SendDataToServer(request.ToJson(), key: r_id, true);
        IO.Swagger.Model.UpdateActionPointPositionResponse response = await WaitForResult<IO.Swagger.Model.UpdateActionPointPositionResponse>(r_id);

        if (response == null || !response.Result)
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
    }


    /// <summary>
    /// Asks server to remove action.
    /// Throws RequestFailedException when request failed
    /// </summary>
    /// <param name="actionId">UUID of action</param>
    /// <param name="dryRun">If true, validates all parameters, but will not execute requested action itself.</param>
    /// <returns></returns>
    public async Task RemoveAction(string actionId, bool dryRun) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.IdArgs args = new IO.Swagger.Model.IdArgs(id: actionId);
        IO.Swagger.Model.RemoveActionRequest request = new IO.Swagger.Model.RemoveActionRequest(r_id, "RemoveAction", args, dryRun: dryRun);
        SendDataToServer(request.ToJson(), r_id, true, logInfo: !dryRun);
        IO.Swagger.Model.RemoveActionResponse response = await WaitForResult<IO.Swagger.Model.RemoveActionResponse>(r_id);

        if (response == null || !response.Result)
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
    }

    /// <summary>
    /// Asks server to rename action.
    /// Throws RequestFailedException when request failed
    /// </summary>
    /// <param name="actionId">UUID of action</param>
    /// <param name="newName">New human readable name of action.</param>
    /// <returns></returns>
    public async Task RenameAction(string actionId, string newName) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.RenameActionRequestArgs args = new IO.Swagger.Model.RenameActionRequestArgs(actionId: actionId, newName: newName);
        IO.Swagger.Model.RenameActionRequest request = new IO.Swagger.Model.RenameActionRequest(r_id, "RenameAction", args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.RenameActionResponse response = await WaitForResult<IO.Swagger.Model.RenameActionResponse>(r_id);

        if (response == null || !response.Result)
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
    }


    /// <summary>
    /// Asks server to remove action point.
    /// Throws RequestFailedException when request failed
    /// </summary>
    /// <param name="actionPointId">UUID of aciton point</param>
    /// <param name="dryRun">If true, validates all parameters, but will not execute requested action itself.</param>
    /// <returns></returns>
    public async Task RemoveActionPoint(string actionPointId, bool dryRun = false) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.IdArgs args = new IO.Swagger.Model.IdArgs(actionPointId);
        IO.Swagger.Model.RemoveActionPointRequest request = new IO.Swagger.Model.RemoveActionPointRequest(r_id, "RemoveActionPoint", args, dryRun: dryRun);
        SendDataToServer(request.ToJson(), r_id, true, logInfo: !dryRun);
        IO.Swagger.Model.RemoveActionPointResponse response = await WaitForResult<IO.Swagger.Model.RemoveActionPointResponse>(r_id);

        if (response == null || !response.Result)
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);

    }

    /// <summary>
    /// Asks server to add object to scene.
    /// Throws RequestFailedException when request failed
    /// </summary>
    /// <param name="name">Human readable name of action object</param>
    /// <param name="type">Action object type</param>
    /// <param name="pose">Pose of new object</param>
    /// <param name="parameters">List of settings of object</param>
    /// <returns></returns>
    public async Task AddObjectToScene(string name, string type, IO.Swagger.Model.Pose pose, List<IO.Swagger.Model.Parameter> parameters) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.AddObjectToSceneRequestArgs args = new IO.Swagger.Model.AddObjectToSceneRequestArgs(pose: pose, type: type, name: name, parameters: parameters);
        IO.Swagger.Model.AddObjectToSceneRequest request = new IO.Swagger.Model.AddObjectToSceneRequest(id: r_id, request: "AddObjectToScene", args: args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.AddObjectToSceneResponse response = await WaitForResult<IO.Swagger.Model.AddObjectToSceneResponse>(r_id, 30000);
        if (response == null || !response.Result) {
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
        }
    }


    /// <summary>
    /// Asks server to rename aciton point.
    /// </summary>
    /// <param name="id">UUID of action point</param>
    /// <param name="name">New human readable name of action point</param>
    /// <returns></returns>
    public async Task RenameActionPoint(string id, string name) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.RenameActionPointRequestArgs args = new IO.Swagger.Model.RenameActionPointRequestArgs(actionPointId: id, newName: name);
        IO.Swagger.Model.RenameActionPointRequest request = new IO.Swagger.Model.RenameActionPointRequest(r_id, "RenameActionPoint", args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.RenameActionPointResponse response = await WaitForResult<IO.Swagger.Model.RenameActionPointResponse>(r_id);

        if (response == null || !response.Result)
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
    }


    /// <summary>
    /// Asks server to remove action point orientation.
    /// Throws RequestFailedException when request failed
    /// </summary>
    /// <param name="actionPointId">UUID of action point</param>
    /// <param name="orientationId">UUID of orientation</param>
    /// <returns></returns>
    public async Task RemoveActionPointOrientation(string orientationId, bool dryRun) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.RemoveActionPointOrientationRequestArgs args = new IO.Swagger.Model.RemoveActionPointOrientationRequestArgs(orientationId: orientationId);
        IO.Swagger.Model.RemoveActionPointOrientationRequest request = new IO.Swagger.Model.RemoveActionPointOrientationRequest(r_id, "RemoveActionPointOrientation", args, dryRun: dryRun);
        SendDataToServer(request.ToJson(), r_id, true, logInfo: !dryRun);
        IO.Swagger.Model.RemoveActionPointOrientationResponse response = await WaitForResult<IO.Swagger.Model.RemoveActionPointOrientationResponse>(r_id);

        if (response == null || !response.Result)
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
    }


    /// <summary>
    /// Asks server to rename action point orientation
    /// Throws RequestFailedException when request failed
    /// </summary>
    /// <param name="orientationId">Id of orientation</param>
    /// <param name="newName">New human-readable name</param>
    /// <returns></returns>
    public async Task RenameActionPointOrientation(string orientationId, string newName) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.RenameActionPointOrientationRequestArgs args = new IO.Swagger.Model.RenameActionPointOrientationRequestArgs(newName: newName, orientationId: orientationId);
        IO.Swagger.Model.RenameActionPointOrientationRequest request = new IO.Swagger.Model.RenameActionPointOrientationRequest(r_id, "RenameActionPointOrientation", args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.RenameActionPointJointsResponse response = await WaitForResult<IO.Swagger.Model.RenameActionPointJointsResponse>(r_id);

        if (response == null || !response.Result)
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
    }


    /// <summary>
    /// Decodes changes on object types and invoke proper callback
    /// </summary>
    /// <param name="data">Message from server</param>
    private void HandleChangedObjectTypesEvent(string data) {
        IO.Swagger.Model.ChangedObjectTypes objectTypesChangedEvent = JsonConvert.DeserializeObject<IO.Swagger.Model.ChangedObjectTypes>(data);
        switch (objectTypesChangedEvent.ChangeType) {
            case IO.Swagger.Model.ChangedObjectTypes.ChangeTypeEnum.Add:
                ActionsManagerH.Instance.ActionsReady = false;
                OnObjectTypeAdded?.Invoke(this, new ObjectTypesEventArgs(objectTypesChangedEvent.Data));

                break;
            case IO.Swagger.Model.ChangedObjectTypes.ChangeTypeEnum.Remove:
                List<string> removed = new List<string>();
                foreach (ObjectTypeMeta type in objectTypesChangedEvent.Data)
                    removed.Add(type.Type);
                OnObjectTypeRemoved?.Invoke(this, new StringListEventArgs(removed));
                break;

            case ChangedObjectTypes.ChangeTypeEnum.Update:
                ActionsManagerH.Instance.ActionsReady = false;
                OnObjectTypeUpdated?.Invoke(this, new ObjectTypesEventArgs(objectTypesChangedEvent.Data));
                break;
            default:
                throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Decodes changes on actions and invokes proper callback 
    /// </summary>
    /// <param name="data">Message from server</param>
    private void HandleActionChanged(string data) {
        IO.Swagger.Model.ActionChanged actionChanged = JsonConvert.DeserializeObject<IO.Swagger.Model.ActionChanged>(data);
        var actionChangedFields = new {
            data = new IO.Swagger.Model.Action(id: "", name: "", type: "", flows: new List<Flow>(), parameters: new List<ActionParameter>())
        };
        HProjectManager.Instance.ProjectChanged = true;
        switch (actionChanged.ChangeType) {
            case IO.Swagger.Model.ActionChanged.ChangeTypeEnum.Add:
                var action = JsonConvert.DeserializeAnonymousType(data, actionChangedFields);
                HProjectManager.Instance.ActionAdded(action.data, actionChanged.ParentId);
                break;
            case IO.Swagger.Model.ActionChanged.ChangeTypeEnum.Remove:
                HProjectManager.Instance.ActionRemoved(actionChanged.Data);
                break;
            case IO.Swagger.Model.ActionChanged.ChangeTypeEnum.Update:
                var actionUpdate = JsonConvert.DeserializeAnonymousType(data, actionChangedFields);
                HProjectManager.Instance.ActionUpdated(actionUpdate.data);
                break;
            case IO.Swagger.Model.ActionChanged.ChangeTypeEnum.Updatebase:
                HProjectManager.Instance.ActionBaseUpdated(actionChanged.Data);
                break;
            default:
                throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Decodes changes of action points
    /// </summary>
    /// <param name="data">Message from server</param>
    private void HandleActionPointChanged(string data) {
        HProjectManager.Instance.ProjectChanged = true;
        IO.Swagger.Model.ActionPointChanged actionPointChangedEvent = JsonConvert.DeserializeObject<IO.Swagger.Model.ActionPointChanged>(data);
        var actionPointChangedFields = new {
            data = new IO.Swagger.Model.ActionPoint(id: "", name: "string", parent: "", position: new Position(),
                actions: new List<IO.Swagger.Model.Action>(), orientations: new List<NamedOrientation>(),
                robotJoints: new List<ProjectRobotJoints>())
        };

        switch (actionPointChangedEvent.ChangeType) {
            case IO.Swagger.Model.ActionPointChanged.ChangeTypeEnum.Add:
                var actionPoint = JsonConvert.DeserializeAnonymousType(data, actionPointChangedFields);
                OnActionPointAdded?.Invoke(this, new ProjectActionPointEventArgs(actionPoint.data));
                break;
            case IO.Swagger.Model.ActionPointChanged.ChangeTypeEnum.Remove:
                OnActionPointRemoved?.Invoke(this, new StringEventArgs(actionPointChangedEvent.Data.Id));
                break;
            case IO.Swagger.Model.ActionPointChanged.ChangeTypeEnum.Update:
                var actionPointUpdate = JsonConvert.DeserializeAnonymousType(data, actionPointChangedFields);
                OnActionPointUpdated?.Invoke(this, new ProjectActionPointEventArgs(actionPointUpdate.data));
                break;
            case IO.Swagger.Model.ActionPointChanged.ChangeTypeEnum.Updatebase:
                OnActionPointBaseUpdated?.Invoke(this, new BareActionPointEventArgs(actionPointChangedEvent.Data));
                break;
            default:
                throw new NotImplementedException();
        }
    }

    public async Task StartScene(bool dryRun) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.StartSceneRequest request = new IO.Swagger.Model.StartSceneRequest(r_id, "StartScene", dryRun: dryRun);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.StartSceneResponse response = await WaitForResult<IO.Swagger.Model.StartSceneResponse>(r_id);
        if (response == null || !response.Result) {
            throw new RequestFailedException(response == null ? new List<string>() { "Failed to start scene" } : response.Messages);
        }
    }

    /// <summary>
    /// Handles message with info about robots end effector
    /// </summary>
    /// <param name="data">Message from server</param>
    private void HandleRobotEef(string data) {
        IO.Swagger.Model.RobotEef robotEef = JsonConvert.DeserializeObject<IO.Swagger.Model.RobotEef>(data);
        OnRobotEefUpdated?.Invoke(this, new RobotEefUpdatedEventArgs(robotEef.Data));
    }

    /// <summary>
    /// Handles message with infou about robots joints
    /// </summary>
    /// <param name="data">Message from server</param>
    private void HandleRobotJoints(string data) {
        IO.Swagger.Model.RobotJoints robotJoints = JsonConvert.DeserializeObject<IO.Swagger.Model.RobotJoints>(data);
        OnRobotJointsUpdated?.Invoke(this, new RobotJointsUpdatedEventArgs(robotJoints.Data));
    }

    /// <summary>
    /// Asks server to save currently openned scene
    /// </summary>
    /// <returns>Response form server</returns>
    public async Task<IO.Swagger.Model.SaveSceneResponse> SaveScene(bool dryRun = false) {
        int id = Interlocked.Increment(ref requestID);
        SendDataToServer(new IO.Swagger.Model.SaveSceneRequest(id: id, request: "SaveScene", dryRun: dryRun).ToJson(), id, true);
        return await WaitForResult<IO.Swagger.Model.SaveSceneResponse>(id);
    }

    /// <summary>
    /// Asks server to save currently openned project
    /// </summary>
    /// <returns>Response form server</returns>
    public async Task<IO.Swagger.Model.SaveProjectResponse> SaveProject(bool dryRun = false) {
        int id = Interlocked.Increment(ref requestID);
        SendDataToServer(new IO.Swagger.Model.SaveProjectRequest(id, "SaveProject", dryRun).ToJson(), id, false);
        return await WaitForResult<IO.Swagger.Model.SaveProjectResponse>(id);

    }

    /// <summary>
    /// Asks server to add new action point.
    /// Throws RequestFailedException when request failed
    /// </summary>
    /// <param name="name">Human readable name of action point</param>
    /// <param name="parent">UUID of action point parent. Null if action point should be global.</param>
    /// <param name="position">Offset from parent (or scene origin for global AP)</param>
    /// <returns></returns>
    public async Task AddActionPoint(string name, string parent, IO.Swagger.Model.Position position) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.AddActionPointRequestArgs args = new IO.Swagger.Model.AddActionPointRequestArgs(parent: parent, position: position, name: name);
        IO.Swagger.Model.AddActionPointRequest request = new IO.Swagger.Model.AddActionPointRequest(r_id, "AddActionPoint", args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.AddActionPointResponse response = await WaitForResult<IO.Swagger.Model.AddActionPointResponse>(r_id);

        if (response == null || !response.Result)
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
    }


    /// <summary>
    /// Asks server to add new action.
    /// Throws RequestFailedException when request failed
    /// </summary>
    /// <param name="actionPointId">UUID of action point to which action should be added</param>
    /// <param name="actionParameters">Parameters of action</param>
    /// <param name="type">Type of action</param>
    /// <param name="name">Human readable name of action</param>
    /// <param name="flows">List of logical flows from action</param>
    /// <returns></returns>
    public async Task AddAction(string actionPointId, List<IO.Swagger.Model.ActionParameter> actionParameters, string type, string name, List<Flow> flows) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.AddActionRequestArgs args = new IO.Swagger.Model.AddActionRequestArgs(actionPointId: actionPointId, parameters: actionParameters, type: type, name: name, flows: flows);
        IO.Swagger.Model.AddActionRequest request = new IO.Swagger.Model.AddActionRequest(r_id, "AddAction", args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.AddActionResponse response = await WaitForResult<IO.Swagger.Model.AddActionResponse>(r_id);

        if (response == null || !response.Result)
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
    }

    /// <summary>
    /// Asks server to update logic item (e.g. change connection between actions).
    /// Throws RequestFailedException when request failed
    /// </summary>
    /// <param name="logicItemId">UUID of logic item</param>
    /// <param name="startActionId">UUID of first action (from)</param>
    /// <param name="endActionId">UUID of second action (to)</param>
    /// <returns></returns>
    public async Task UpdateLogicItem(string logicItemId, string startActionId, string endActionId) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.UpdateLogicItemRequestArgs args = new IO.Swagger.Model.UpdateLogicItemRequestArgs(start: startActionId, end: endActionId, logicItemId: logicItemId);
        IO.Swagger.Model.UpdateLogicItemRequest request = new IO.Swagger.Model.UpdateLogicItemRequest(r_id, "UpdateLogicItem", args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.UpdateLogicItemResponse response = await WaitForResult<IO.Swagger.Model.UpdateLogicItemResponse>(r_id);

        if (response == null || !response.Result)
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
    }

    /// <summary>
    /// Asks server to remove logic item (destroy connection of actions).
    /// Throws RequestFailedException when request failed
    /// </summary>
    /// <param name="logicItemId">UUID of connection.</param>
    /// <returns></returns>
    public async Task RemoveLogicItem(string logicItemId) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.RemoveLogicItemRequestArgs args = new IO.Swagger.Model.RemoveLogicItemRequestArgs(logicItemId: logicItemId);
        IO.Swagger.Model.RemoveLogicItemRequest request = new IO.Swagger.Model.RemoveLogicItemRequest(r_id, "RemoveLogicItem", args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.RemoveLogicItemResponse response = await WaitForResult<IO.Swagger.Model.RemoveLogicItemResponse>(r_id);

        if (response == null || !response.Result)
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
    }


    /// <summary>
    /// Decodes changes in program logic
    /// </summary>
    /// <param name="data">Message from server</param>
    private void HandleLogicItemChanged(string data) {
        LogicItemChanged logicItemChanged = JsonConvert.DeserializeObject<LogicItemChanged>(data);
        HProjectManager.Instance.ProjectChanged = true;
        switch (logicItemChanged.ChangeType) {
            case LogicItemChanged.ChangeTypeEnum.Add:
                OnLogicItemAdded?.Invoke(this, new LogicItemChangedEventArgs(logicItemChanged.Data));
                break;
            case LogicItemChanged.ChangeTypeEnum.Remove:
                OnLogicItemRemoved?.Invoke(this, new StringEventArgs(logicItemChanged.Data.Id));
                break;
            case LogicItemChanged.ChangeTypeEnum.Update:
                OnLogicItemUpdated?.Invoke(this, new LogicItemChangedEventArgs(logicItemChanged.Data));
                break;
            default:
                throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Asks server to create new orientation for action point.
    /// Throws RequestFailedException when request failed
    /// </summary>
    /// <param name="id">UUID of action point</param>
    /// <param name="orientation">Orientation</param>
    /// <param name="name">Human readable name of orientation</param>
    /// <returns></returns>
    public async Task AddActionPointOrientation(string id, IO.Swagger.Model.Orientation orientation, string name) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.AddActionPointOrientationRequestArgs args = new IO.Swagger.Model.AddActionPointOrientationRequestArgs(actionPointId: id, orientation: orientation, name: name);
        IO.Swagger.Model.AddActionPointOrientationRequest request = new IO.Swagger.Model.AddActionPointOrientationRequest(r_id, "AddActionPointOrientation", args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.AddActionPointOrientationResponse response = await WaitForResult<IO.Swagger.Model.AddActionPointOrientationResponse>(r_id);

        if (response == null || !response.Result)
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
    }

    /// <summary>
    /// Decodes changes on orientations
    /// </summary>
    /// <param name="data">Message from server</param>
    private void HandleOrientationChanged(string data) {
        HProjectManager.Instance.ProjectChanged = true;
        IO.Swagger.Model.OrientationChanged orientationChanged = JsonConvert.DeserializeObject<IO.Swagger.Model.OrientationChanged>(data);
        switch (orientationChanged.ChangeType) {
            case IO.Swagger.Model.OrientationChanged.ChangeTypeEnum.Add:
                OnActionPointOrientationAdded?.Invoke(this, new ActionPointOrientationEventArgs(orientationChanged.Data, orientationChanged.ParentId));
                break;
            case IO.Swagger.Model.OrientationChanged.ChangeTypeEnum.Remove:
                OnActionPointOrientationRemoved?.Invoke(this, new StringEventArgs(orientationChanged.Data.Id));
                break;
            case IO.Swagger.Model.OrientationChanged.ChangeTypeEnum.Update:
                OnActionPointOrientationUpdated?.Invoke(this, new ActionPointOrientationEventArgs(orientationChanged.Data, null));
                break;
            case IO.Swagger.Model.OrientationChanged.ChangeTypeEnum.Updatebase:
                OnActionPointOrientationBaseUpdated?.Invoke(this, new ActionPointOrientationEventArgs(orientationChanged.Data, null));

                break;
            default:
                throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Asks server to create and run temporary package. This package is not saved on execution unit and it is
    /// removed immideately after package execution. Throws RequestFailedException when request failed
    /// </summary>
    /// <returns></returns>
    public async Task TemporaryPackage(List<string> apBreakpoints, bool pauseOnFirstAction = false) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.TemporaryPackageRequestArgs args = new TemporaryPackageRequestArgs(breakpoints: apBreakpoints, startPaused: pauseOnFirstAction);
        IO.Swagger.Model.TemporaryPackageRequest request = new IO.Swagger.Model.TemporaryPackageRequest(id: r_id, request: "TemporaryPackage", args: args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.TemporaryPackageResponse response = await WaitForResult<IO.Swagger.Model.TemporaryPackageResponse>(r_id, 30000);
        if (response == null || !response.Result)
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
    }

    /// <summary>
    /// Asks server to create project.
    /// Throws RequestFailedException when request failed
    /// </summary>
    /// <param name="name">Human readable name of project</param>
    /// <param name="sceneId">UUID of scene</param>
    /// <param name="description">Description of project</param>
    /// <param name="hasLogic">Flags indicating if project specifies logical flow of thr program.</param>
    /// <param name="dryRun">If true, validates all parameters, but will not execute requested action itself.</param>
    /// <returns></returns>
    public async Task CreateProject(string name, string sceneId, string description, bool hasLogic, bool dryRun) {
        int r_id = Interlocked.Increment(ref requestID);
        IO.Swagger.Model.NewProjectRequestArgs args = new IO.Swagger.Model.NewProjectRequestArgs(name: name, sceneId: sceneId, description: description, hasLogic: hasLogic);
        IO.Swagger.Model.NewProjectRequest request = new IO.Swagger.Model.NewProjectRequest(r_id, "NewProject", args, dryRun: dryRun);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.NewProjectResponse response = await WaitForResult<IO.Swagger.Model.NewProjectResponse>(r_id);

        if (response == null || !response.Result)
            throw new RequestFailedException(response == null ? "Request timed out" : response.Messages[0]);
    }

    /*      /// <summary>
     /// Decodes package state
     /// </summary>
     /// <param name="obj"></param>
     private void HandlePackageState(string obj) {
         IO.Swagger.Model.PackageState projectState = JsonConvert.DeserializeObject<IO.Swagger.Model.PackageState>(obj);
         GameManagerH.Instance.PackageStateUpdated(projectState.Data);
     }

     /// <summary>
     /// Decodes package info
     /// </summary>
     /// <param name="obj">Message from server</param>
     private void HandlePackageInfo(string obj) {
         IO.Swagger.Model.PackageInfo packageInfo = JsonConvert.DeserializeObject<IO.Swagger.Model.PackageInfo>(obj);
         GameManagerH.Instance.PackageInfo = packageInfo.Data;
     }

*/
    public async Task<List<IO.Swagger.Model.Joint>> InverseKinematics(string robotId, string endEffectorId, bool avoidCollisions, IO.Swagger.Model.Pose pose, List<IO.Swagger.Model.Joint> startJoints) {
        int r_id = Interlocked.Increment(ref requestID);

        IO.Swagger.Model.InverseKinematicsRequestArgs args = new InverseKinematicsRequestArgs(robotId: robotId,
            endEffectorId: endEffectorId, pose: pose, avoidCollisions: avoidCollisions, startJoints: startJoints, armId: "");

        IO.Swagger.Model.InverseKinematicsRequest request = new IO.Swagger.Model.InverseKinematicsRequest(r_id, "InverseKinematics", args: args);
        SendDataToServer(request.ToJson(), r_id, true);
        IO.Swagger.Model.InverseKinematicsResponse response = await WaitForResult<IO.Swagger.Model.InverseKinematicsResponse>(r_id);

        if (response == null || !response.Result) {
            throw new RequestFailedException(response == null ? new List<string>() { "Failed to delete override of object parameter" } : response.Messages);
        } else {
            return response.Data;
        }
    }


}

