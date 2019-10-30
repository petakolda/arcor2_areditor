using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Threading.Tasks;


namespace Base {
    public class WebsocketManager : Singleton<WebsocketManager> {
        public string APIDomainWS = "";

        private ClientWebSocket clientWebSocket;

        private List<string> actionObjectsToBeUpdated = new List<string>();
        private Queue<KeyValuePair<int, string>> sendingQueue = new Queue<KeyValuePair<int, string>>();
        private string waitingForObjectActions;

        private bool waitingForMessage = false;

        private string receivedData;

        private bool readyToSend, ignoreProjectChanged, connecting;

        private Dictionary<int, string> responses = new Dictionary<int, string>();

        int requestID = 1;

        private void Awake() {
            waitingForMessage = false;
            readyToSend = true;
            ignoreProjectChanged = false;
            connecting = false;

            receivedData = "";
            waitingForObjectActions = "";
        }

        private void Start() {

        }

        async public void ConnectToServer(string domain, int port) {
            connecting = true;
            APIDomainWS = GetWSURI(domain, port);
            clientWebSocket = new ClientWebSocket();
            Debug.Log("[WS]:Attempting connection.");
            try {
                Uri uri = new Uri(APIDomainWS);
                await clientWebSocket.ConnectAsync(uri, CancellationToken.None);

                Debug.Log("[WS][connect]:" + "Connected");
            } catch (Exception e) {
                Debug.Log("[WS][exception]:" + e.Message);
                if (e.InnerException != null) {
                    Debug.Log("[WS][inner exception]:" + e.InnerException.Message);
                }
            }

            Debug.Log("End");
            connecting = false;
            if (clientWebSocket.State == WebSocketState.Open) {

                GameManager.Instance.ConnectionStatus = GameManager.ConnectionStatusEnum.Connected;
                
            } else {
                GameManager.Instance.ConnectionStatus = GameManager.ConnectionStatusEnum.Disconnected;
            }
        }

        async public void DisconnectFromSever() {
            Debug.Log("Disconnecting");
            await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
            clientWebSocket = null;
        }



        // Update is called once per frame
        async void Update() {
            if (clientWebSocket == null)
                return;
            if (clientWebSocket.State == WebSocketState.Open && GameManager.Instance.ConnectionStatus == GameManager.ConnectionStatusEnum.Disconnected) {
                GameManager.Instance.ConnectionStatus = GameManager.ConnectionStatusEnum.Connected;
            } else if (clientWebSocket.State != WebSocketState.Open && GameManager.Instance.ConnectionStatus == GameManager.ConnectionStatusEnum.Connected) {
                GameManager.Instance.ConnectionStatus = GameManager.ConnectionStatusEnum.Disconnected;
            }

            if (!waitingForMessage && clientWebSocket.State == WebSocketState.Open) {
                WebSocketReceiveResult result = null;
                waitingForMessage = true;
                ArraySegment<byte> bytesReceived = new ArraySegment<byte>(new byte[8192]);
                do {
                    result = await clientWebSocket.ReceiveAsync(
                        bytesReceived,
                        CancellationToken.None
                    );
                    receivedData += Encoding.Default.GetString(bytesReceived.Array);
                } while (!result.EndOfMessage);
                HandleReceivedData(receivedData);
                receivedData = "";
                waitingForMessage = false;

            }
            if (waitingForObjectActions == "" && actionObjectsToBeUpdated.Count > 0) {
                waitingForObjectActions = actionObjectsToBeUpdated[0];
                actionObjectsToBeUpdated.RemoveAt(0);
                UpdateObjectActions(waitingForObjectActions);
            }

            if (sendingQueue.Count > 0 && readyToSend) {
                SendDataToServer();
            }

        }

        public string GetWSURI(string domain, int port) {
            return "ws://" + domain + ":" + port.ToString();
        }

        void OnApplicationQuit() {
            DisconnectFromSever();
        }

        public void SendDataToServer(string data, int key = -1, bool storeResult = false) {
            if (key < 0) {
                key = requestID++;
            }
            Debug.Log("Sending data to server: " + data);
            
            if (storeResult) {
                responses[key] = null;
            }
            sendingQueue.Enqueue(new KeyValuePair<int, string>(key, data));
        }

        async public void SendDataToServer() {
            if (sendingQueue.Count == 0)
                return;
            KeyValuePair<int, string> keyVal = sendingQueue.Dequeue();
            readyToSend = false;
            if (clientWebSocket.State != WebSocketState.Open)
                return;

            ArraySegment<byte> bytesToSend = new ArraySegment<byte>(
                         Encoding.UTF8.GetBytes(keyVal.Value)
                     );
            await clientWebSocket.SendAsync(
                bytesToSend,
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
            readyToSend = true;
        }

        public void UpdateObjectTypes() {
            SendDataToServer(new IO.Swagger.Model.GetObjectTypesRequest(request: "getObjectTypes").ToJson());
        }

        public void UpdateObjectActions(string ObjectId) {
            SendDataToServer(new IO.Swagger.Model.GetObjectActionsRequest(request: "getObjectActions", args: new IO.Swagger.Model.TypeArgs(type: ObjectId)).ToJson());
        }

        public void UpdateScene(IO.Swagger.Model.Scene scene) {

            ARServer.Models.EventSceneChanged eventData = new ARServer.Models.EventSceneChanged {
                Scene = scene
            };

            SendDataToServer(eventData.ToJson());


        }

        // TODO: add action parameters
        public void UpdateProject(IO.Swagger.Model.Project project) {
            ARServer.Models.EventProjectChanged eventData = new ARServer.Models.EventProjectChanged {
                Project = project
            };
            SendDataToServer(eventData.ToJson());

        }


        private void HandleReceivedData(string data) {
            var dispatchType = new {
                id = 0,
                response = "",
                @event = "",
                request = ""
            };
            Debug.Log("Recieved new data: " + data);
            Debug.LogError(data);
            var dispatch = JsonConvert.DeserializeAnonymousType(data, dispatchType);
            
            JSONObject jsonData = new JSONObject(data);
            
            if (dispatch.response == null && dispatch.request == null && dispatch.@event == null)
                return;
            if (dispatch.response != null) {
                switch (dispatch.response) {
                    case "getObjectTypes":
                        HandleGetObjecTypes(data);
                        break;
                    case "getObjectActions":
                        HandleGetObjectActions(data);
                        break;
                    case "newObjectType":
                        HandleNewObjectType(data);
                        break;
                    case "focusObjectDone":
                        HandleFocusObjectDone(data);
                        break;
                    case "focusObject":
                        HandleFocusObject(data);
                        break;
                    case "openProject":
                        HandleOpenProject(data);
                        break;
                    default:
                        if (responses.ContainsKey(dispatch.id)) {
                            responses[dispatch.id] = data;
                        }
                        break;
                }
            } else if (dispatch.@event != null) {
                switch (dispatch.@event) {
                    case "sceneChanged":
                        HandleSceneChanged(jsonData);
                        break;
                    case "currentAction":
                        HandleCurrentAction(jsonData);
                        break;
                    case "projectChanged":
                        if (ignoreProjectChanged)
                            ignoreProjectChanged = false;
                        else
                            HandleProjectChanged(jsonData);
                        break;
                }
            }

        }

        /*private async void WaitForResult(string key) {

        }*/

        private async Task<T> WaitForResult<T>(int key) {
            if (responses.TryGetValue(key, out string value)) {
                if (value == null) {
                    value = await WaitForResponseReady(key);
                }
                return JsonConvert.DeserializeObject<T>(value);
            } else {
                return default;
            }
        }

        // TODO: add timeout!
        private Task<string> WaitForResponseReady(int key) {
            return Task.Run(() => {
                while (true) {
                    if (responses.TryGetValue(key, out string value)) {
                        if (value != null) {
                            return value;
                        } else {
                            Thread.Sleep(100);
                        }
                    }                    
                }
            });
        }

        void HandleProjectChanged(JSONObject obj) {

            try {
                if (obj["event"].str != "projectChanged" || obj["data"].GetType() != typeof(JSONObject)) {
                    return;
                }


                ARServer.Models.EventProjectChanged eventProjectChanged = JsonConvert.DeserializeObject<ARServer.Models.EventProjectChanged>(obj.ToString());
                GameManager.Instance.ProjectUpdated(eventProjectChanged.Project);


            } catch (NullReferenceException e) {
                Debug.Log("Parse error in HandleProjectChanged()");

            }

        }

        void HandleCurrentAction(JSONObject obj) {
            string puck_id;
            try {
                if (obj["event"].str != "currentAction" || obj["data"].GetType() != typeof(JSONObject)) {
                    return;
                }

                puck_id = obj["data"]["action_id"].str;



            } catch (NullReferenceException e) {
                Debug.Log("Parse error in HandleProjectChanged()");
                return;
            }

            GameObject puck = GameManager.Instance.FindPuck(puck_id);

            //Arrow.transform.SetParent(puck.transform);
            //Arrow.transform.position = puck.transform.position + new Vector3(0f, 1.5f, 0f);
        }

        void HandleSceneChanged(JSONObject obj) {           
            ARServer.Models.EventSceneChanged eventSceneChanged = JsonConvert.DeserializeObject<ARServer.Models.EventSceneChanged>(obj.ToString());
            GameManager.Instance.SceneUpdated(eventSceneChanged.Scene);
        }

        private void HandleNewObjectType(string data) {
            IO.Swagger.Model.NewObjectTypeResponse response = JsonConvert.DeserializeObject<IO.Swagger.Model.NewObjectTypeResponse>(data);
            if (response.Result) {
                SSTools.ShowMessage("New object type successfully created", SSTools.Position.bottom, SSTools.Time.twoSecond);
            } else {
                SSTools.ShowMessage("Failed to create new object type: " + response.Messages[0], SSTools.Position.bottom, SSTools.Time.threeSecond);
            }
        }
        
        private void HandleFocusObjectStart(string data) {
            IO.Swagger.Model.FocusObjectStartResponse response = JsonConvert.DeserializeObject<IO.Swagger.Model.FocusObjectStartResponse>(data);
            if (response.Result) {
                SSTools.ShowMessage("Object focusing started", SSTools.Position.bottom, SSTools.Time.twoSecond);
            } else {
                SSTools.ShowMessage("Failed to start object focusing: " + response.Messages[0], SSTools.Position.bottom, SSTools.Time.threeSecond);
            }
        }
        
        private void HandleFocusObjectDone(string data) {
            IO.Swagger.Model.FocusObjectDoneResponse response = JsonConvert.DeserializeObject<IO.Swagger.Model.FocusObjectDoneResponse>(data);
            if (response.Result && response.Messages.Count == 0) {
                SSTools.ShowMessage("Object focused successfully", SSTools.Position.bottom, SSTools.Time.twoSecond);
            } else if (response.Result) {
                SSTools.ShowMessage(response.Messages[0], SSTools.Position.bottom, SSTools.Time.twoSecond);
            } else {
                SSTools.ShowMessage("Failed to focus object: " + response.Messages[0], SSTools.Position.bottom, SSTools.Time.threeSecond);
            }
        }
        
        private void HandleFocusObject(string data) {
            IO.Swagger.Model.FocusObjectResponse response = JsonConvert.DeserializeObject<IO.Swagger.Model.FocusObjectResponse>(data);
            if (response.Result) {
                SSTools.ShowMessage("Point focused", SSTools.Position.bottom, SSTools.Time.twoSecond);
            } else {
                SSTools.ShowMessage("Failed to start object focusing: " + response.Messages[0], SSTools.Position.bottom, SSTools.Time.threeSecond);
            }
        }
        
        private void HandleSaveScene(string data) {
            IO.Swagger.Model.SaveSceneResponse response = JsonConvert.DeserializeObject<IO.Swagger.Model.SaveSceneResponse>(data);
        }
        
        private void HandleSaveProject(string data) {
            IO.Swagger.Model.SaveProjectResponse response = JsonConvert.DeserializeObject<IO.Swagger.Model.SaveProjectResponse>(data);
        }
        
        private void HandleOpenProject(string data) {
            IO.Swagger.Model.OpenProjectResponse response = JsonConvert.DeserializeObject<IO.Swagger.Model.OpenProjectResponse>(data);
        }
        
        private async void HandleListProjects(string data) {
            IO.Swagger.Model.ListProjectsResponse response = JsonConvert.DeserializeObject<IO.Swagger.Model.ListProjectsResponse>(data);
        }
        
        private void HandleListScenes(string data) {
            IO.Swagger.Model.ListScenesResponse response = JsonConvert.DeserializeObject<IO.Swagger.Model.ListScenesResponse>(data);
        }
        
       

        private void HandleGetObjectActions(string data) {
            IO.Swagger.Model.GetObjectActionsResponse response = JsonConvert.DeserializeObject<IO.Swagger.Model.GetObjectActionsResponse>(data);
            if (!response.Result) {
                Debug.LogError("Request getObjectActions failed!");
                return;
            }
            
            if (ActionsManager.Instance.ActionObjectMetadata.TryGetValue(waitingForObjectActions, out ActionObjectMetadata ao)) {
                foreach (IO.Swagger.Model.ObjectAction action in response.Data) {
                    ActionMetadata a = new ActionMetadata(action.Name, action.Meta.Blocking, action.Meta.Free, action.Meta.Composite, action.Meta.Blackbox);
                    foreach (IO.Swagger.Model.ObjectActionArgs arg in action.ActionArgs) {
                        switch (arg.Type) {
                            //case IO.Swagger.Model.ActionParameter.TypeEnum.String:
                            //case IO.Swagger.Model.ActionParameter.TypeEnum.ActionPoint:
                            case "str":
                            case "ActionPoint":
                                a.Parameters[arg.Name] = new ActionParameterMetadata(arg.Name, arg.Type, "");
                                break;
                            //case IO.Swagger.Model.ActionParameter.TypeEnum.Double:
                            case "double":
                                a.Parameters[arg.Name] = new ActionParameterMetadata(arg.Name, arg.Type, 0d);
                                break;
                            //case IO.Swagger.Model.ActionParameter.TypeEnum.Integer:
                            case "int":
                                a.Parameters[arg.Name] = new ActionParameterMetadata(arg.Name, arg.Type, (long) 0);
                                break;


                        }
                        
                    }
                    ao.ActionsMetadata[a.Name] = a;
                }
                ao.ActionsLoaded = true;
                waitingForObjectActions = "";
            }
        }

        private void HandleGetObjecTypes(string data) {
            IO.Swagger.Model.GetObjectTypesResponse response = JsonConvert.DeserializeObject<IO.Swagger.Model.GetObjectTypesResponse>(data);
            if (!response.Result) {
                Debug.LogError("Request getObjectTypes failed!");
                return;
            }
            Dictionary<string, ActionObjectMetadata> newActionObjects = new Dictionary<string, ActionObjectMetadata>();

            foreach (IO.Swagger.Model.ObjectTypeMeta objectType in response.Data) {
                ActionObjectMetadata ao = new ActionObjectMetadata(objectType.Type, objectType.Description, objectType.Base, objectType.ObjectModel);
                newActionObjects[ao.Type] = ao;
                actionObjectsToBeUpdated.Add(ao.Type);
            }
            ActionsManager.Instance.UpdateObjects(newActionObjects);
        }

        private bool CheckHeaders(JSONObject obj, string method) {
            try {
                if (obj["response"].str != method)
                    return false;
                if (!obj["result"].b)
                    return false;

            } catch (NullReferenceException e) {
                Debug.Log("Parse error in HandleGetObjecTypes()");
                return false;
            }
            return true;
        }


        public async Task<IO.Swagger.Model.SaveSceneResponse> SaveScene() {
            int id = requestID++;
            SendDataToServer(new IO.Swagger.Model.SaveSceneRequest(id: id, request: "saveScene", args: "").ToJson(), id, true);
            return await WaitForResult<IO.Swagger.Model.SaveSceneResponse>(id);
        }

        public async Task<IO.Swagger.Model.SaveProjectResponse> SaveProject() {
            int id = requestID++;
            SendDataToServer(new IO.Swagger.Model.SaveProjectRequest(id: id, request: "saveProject", args: "").ToJson(), id, true);
            return await WaitForResult<IO.Swagger.Model.SaveProjectResponse>(id);
        }

        public void LoadProject(string id) {
            SendDataToServer(new IO.Swagger.Model.OpenProjectRequest(request: "openProject", args: new IO.Swagger.Model.IdArgs(id: id)).ToJson());
        }

        public void RunProject(string projectId) {
            SendDataToServer(new IO.Swagger.Model.RunProjectRequest(request: "runProject", args: new IO.Swagger.Model.IdArgs(id: projectId)).ToJson());
        }

        public void StopProject() {
            SendDataToServer(new IO.Swagger.Model.StopProjectRequest(request: "stopProject", args: "").ToJson());
        }

        public void PauseProject() {
            SendDataToServer(new IO.Swagger.Model.PauseProjectRequest(request: "pauseProject", args: "").ToJson());
        }

        public void ResumeProject() {
            SendDataToServer(new IO.Swagger.Model.ResumeProjectRequest(request: "resumeProject", args: "").ToJson());
        }

        public void UpdateActionPointPosition(string actionPointId, string robotId, string endEffectorId) {
            IO.Swagger.Model.RobotArg robotArg = new IO.Swagger.Model.RobotArg(endEffector: endEffectorId, id: robotId);
            IO.Swagger.Model.UpdateActionPointPoseRequestArgs args = new IO.Swagger.Model.UpdateActionPointPoseRequestArgs(id: actionPointId, robot: robotArg);
            IO.Swagger.Model.UpdateActionPointPoseRequest request = new IO.Swagger.Model.UpdateActionPointPoseRequest(request: "updateActionPointPose", args: args);
            SendDataToServer(request.ToJson());
        }

        public void UpdateActionObjectPosition(string actionObjectId, string robotId, string endEffectorId) {
            IO.Swagger.Model.RobotArg robotArg = new IO.Swagger.Model.RobotArg(endEffector: endEffectorId, id: robotId);
            IO.Swagger.Model.UpdateActionObjectPoseRequestArgs args = new IO.Swagger.Model.UpdateActionObjectPoseRequestArgs(robot: robotArg);
            IO.Swagger.Model.UpdateActionObjectPoseRequest request = new IO.Swagger.Model.UpdateActionObjectPoseRequest(request: "updateActionObjectPose", args: args);
            SendDataToServer(request.ToJson());
        }

        public void CreateNewObjectType(IO.Swagger.Model.ObjectTypeMeta objectType) {
            IO.Swagger.Model.NewObjectTypeRequest request = new IO.Swagger.Model.NewObjectTypeRequest(request: "newObjectType", args: objectType);
            SendDataToServer(request.ToJson());
            UpdateObjectTypes();
        }

        public async Task<IO.Swagger.Model.FocusObjectStartResponse> StartObjectFocusing(string objectId, string robotId, string endEffector) {
            IO.Swagger.Model.RobotArg robotArg = new IO.Swagger.Model.RobotArg(endEffector, robotId);
            IO.Swagger.Model.FocusObjectStartRequestArgs args = new IO.Swagger.Model.FocusObjectStartRequestArgs(objectId: objectId, robot: robotArg);
            IO.Swagger.Model.FocusObjectStartRequest request = new IO.Swagger.Model.FocusObjectStartRequest(id: requestID++, request: "focusObjectStart", args: args);
            SendDataToServer(request.ToJson(), request.Id, true);
            return await WaitForResult<IO.Swagger.Model.FocusObjectStartResponse>(request.Id);
        }

        

        public void SavePosition(string objectId, int pointIdx) {
            IO.Swagger.Model.FocusObjectRequestArgs args = new IO.Swagger.Model.FocusObjectRequestArgs(objectId: objectId, pointIdx: pointIdx);
            IO.Swagger.Model.FocusObjectRequest request = new IO.Swagger.Model.FocusObjectRequest(request: "focusObject", args: args);
            SendDataToServer(request.ToJson());
        }

        public void FocusObjectDone(string objectId) {
            IO.Swagger.Model.IdArgs args = new IO.Swagger.Model.IdArgs(id: objectId);
            IO.Swagger.Model.FocusObjectDoneRequest request = new IO.Swagger.Model.FocusObjectDoneRequest(request: "focusObjectDone", args: args);
            SendDataToServer(request.ToJson());
        }

        public async Task<List<IO.Swagger.Model.IdDesc>> LoadScenes() {
            IO.Swagger.Model.ListScenesRequest request = new IO.Swagger.Model.ListScenesRequest(id: ++requestID, request: "listScenes");
            SendDataToServer(request.ToJson(), requestID, true);
            IO.Swagger.Model.ListScenesResponse response = await WaitForResult<IO.Swagger.Model.ListScenesResponse>(requestID);
            return response.Data;
        }

        public async Task<List<IO.Swagger.Model.IdDesc>> LoadProjects() {
            IO.Swagger.Model.ListProjectsRequest request = new IO.Swagger.Model.ListProjectsRequest(id: ++requestID, request: "listProjects");
            SendDataToServer(request.ToJson(), requestID, true);
            IO.Swagger.Model.ListProjectsResponse response = await WaitForResult<IO.Swagger.Model.ListProjectsResponse>(requestID);
            return response.Data;
        }
    }
}