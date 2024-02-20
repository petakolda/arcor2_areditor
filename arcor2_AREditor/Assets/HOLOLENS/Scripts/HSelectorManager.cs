using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Base;
using Microsoft.MixedReality.Toolkit.UI;
using System.Threading.Tasks;
using Hololens;
using System;
using UnityEngine.Events;



public class HSelectorManager : Singleton<HSelectorManager>
{

    public HConfirmDialog confirmDialog;

    public HRenameDialog renameDialog;


    private HInteractiveObject selectedObject;

    private UnityAction selectedAction;

    ClickedEnum lastClicked;
    protected List<HInteractiveObject> lockedObjects = new List<HInteractiveObject>();

    public HInteractiveObject getSelecetedObject(){
        return selectedObject;
    }

    private HAction Output;

 
    public enum ClickedEnum {
        Transform,
        Delete,
        Rename,
        Copy,
        AddInputConection,
        AddOutputConnection,
        AddAP,
        AddAction,

        None
    }

   
    // Start is called before the first frame update
    void Start()
    {
        setLastClicked(ClickedEnum.None);
    }
    
     public void setLastClicked(ClickedEnum lastClicked){
        this.lastClicked = lastClicked;

    }


    public void clickedAddAPButton() {
        setSelectedAction(addActionPointClicked);
        AddActionPointHandler.Instance.registerHandlers();

        /*   setLastClicked(ClickedEnum.AddAction);
          setSelectedAction(addActionPointClicked);
          AddActionPointHandler.Instance.registerHandlers();*/
    }


    public void clickedOneMeterButton() {
        setSelectedAction(addActionPointClicked);
        AddActionPointHandler.Instance.registerHandlers();

        /*   setLastClicked(ClickedEnum.AddAction);
            setSelectedAction(addActionPointClicked);
            AddActionPointHandler.Instance.registerHandlers();*/
    }

    public bool isClickedOutputConnection(){
        return lastClicked == ClickedEnum.AddOutputConnection;
    }

    public bool isClickedAddAP(){
        return  lastClicked == ClickedEnum.AddAP;
    }

    public void setSelectedAction(UnityAction selectedAction) {   
        AddActionPointHandler.Instance.unregisterHandlers();
        this.selectedAction = selectedAction;
    }


    public void renameClicked(bool removeOnCancel, UnityAction confirmCallback = null, bool keepObjectLocked = false) {

        if (selectedObject is null)
            return;

        if (removeOnCancel)
            renameDialog.Open(selectedObject, true, keepObjectLocked, () => selectedObject.Remove(), confirmCallback);
        else
            renameDialog.Open(selectedObject, false, keepObjectLocked, null, confirmCallback);
       // RenameDialog.Open();
    }

    public void deleteClicked(){

        HDeleteActionManager.Instance.Hide();
        if(!(selectedObject is ActionObjectH actionO) ||  GameManagerH.Instance.GetGameState().Equals(GameManagerH.GameStateEnum.SceneEditor)){

            if(selectedObject is HAction action){
                if(!action.Output.AnyConnection()){
                    if(action is HAction3D action3D){
                        deleteObject();
                    }
                    
                }
                else{
                    HDeleteActionManager.Instance.Show(action);
                    if(action is HStartAction start){
                        HDeleteActionManager.Instance.setActiveActionButton(false);
                    }
                }

            } else {
                deleteObject();
            }
        }

    }

    public void deleteObject(){
        
        
        confirmDialog.Open($"Remove {selectedObject.GetObjectTypeName().ToLower()}",
          $"Do you want to remove {selectedObject.GetName()}",
          () => RemoveObject(selectedObject),
            null);

        setLastClicked(ClickedEnum.Delete);
    }


    public async void copyObjectClicked(){


          if (selectedObject is ActionObjectH actionObject && GameManagerH.Instance.GetGameState().Equals(GameManagerH.GameStateEnum.SceneEditor)) {
            List<IO.Swagger.Model.Parameter> parameters = new List<IO.Swagger.Model.Parameter>();
            foreach (Base.Parameter p in actionObject.ObjectParameters.Values) {
                parameters.Add(DataHelper.ActionParameterToParameter(p));
            }
            string newName = SceneManagerH.Instance.GetFreeAOName(actionObject.GetName());
            await WebSocketManagerH.Instance.AddObjectToScene(newName,
                actionObject.ActionObjectMetadata.Type, new IO.Swagger.Model.Pose(
                    orientation: DataHelper.QuaternionToOrientation(TransformConvertor.UnityToROS(actionObject.transform.localRotation)),
                    position: DataHelper.Vector3ToPosition(TransformConvertor.UnityToROS(actionObject.transform.localPosition))), parameters);
        }
    }


    public void addActionPointClicked() {

        if (selectedObject is HAction3D action) {
            setSelectedAction(actionAddClicked);
            HActionPickerMenu.Instance.Show((HActionPoint) action.GetParentObject(), false);
            HProjectManager.Instance.OnActionAddedToScene += OnActionAddedToScene;


        } else if (selectedObject is IActionPointParentH parent) {
            CreateActionPoint(HProjectManager.Instance.GetFreeAPName(parent.GetName()), parent);
        } else {
            CreateActionPoint(HProjectManager.Instance.GetFreeAPName("global"), default);
        }
        setLastClicked(ClickedEnum.AddAP);
    }

    public void addInputConncetionClicked(){
        if (selectedObject is null){
             HConnectionManagerArcoro.Instance.DestroyConnectionToMouse(); 
             AddActionPointHandler.Instance.unregisterHandlers(); 
             
        }
        else  if (selectedObject is HAction action){
             Output.GetOtherAction(action);
               
         }
         else {
             return;
         }
          Output = null;
          selectedAction = addOutputConnctionClicked;
          setLastClicked(ClickedEnum.AddInputConection);

    }


    public void addOutputConnctionClicked(){
        if (selectedObject is HAction action){
            action.AddConnection();
            Output = action;
            selectedAction = addInputConncetionClicked;
            setLastClicked(ClickedEnum.AddOutputConnection);
            AddActionPointHandler.Instance.registerHandlers(false); 

            
        }
    }

    
    public void transformClicked(){    

        if(!(selectedObject is ActionObjectH actionO) ||  GameManagerH.Instance.GetGameState().Equals(GameManagerH.GameStateEnum.SceneEditor)){
              transformObject();
            setLastClicked(ClickedEnum.Transform);
        }
    }

    public void actionAddClicked(){
        if(selectedObject is ActionObjectH actionObjectH){
            HActionPickerMenu.Instance.actionObjectClicked(actionObjectH);

        }
    }


    private void RemoveObject(HInteractiveObject obj) {
        if(obj is HAction action){
          HDeleteActionManager.Instance.Hide();

        }
        obj.Remove();
        confirmDialog.Close();
    }

    
    public async void transformObject(){

        ///wait 
        if(!(getSelecetedObject() is HStartEndAction e)){
            if (!await LockObject(getSelecetedObject(), true)) {
                return;
            }
            if (getSelecetedObject() is CollisionObjectH co) {
                if (!await co.WriteLockObjectType()) {
                Debug.Log("Failed to lock the object");
                    await UnlockAllObjects();
                    return;
                }
            } 
        }
        HTransformMenu.Instance.activeTransform(getSelecetedObject());
    }

   
    
    public void OnActionAddedToScene(object sender, HololensActionEventArgs args){
        setLastClicked(ClickedEnum.AddAction);
        setSelectedAction(addActionPointClicked);
        AddActionPointHandler.Instance.registerHandlers();
        HProjectManager.Instance.OnActionAddedToScene -= OnActionAddedToScene;

    }
    /// <summary>
    /// Creates new action point
    /// </summary>
    /// <param name="name">Name of the new action point</param>
    /// <param name="parentId">Id of AP parent. Global if null </param>
    private async void CreateActionPoint(string name, IActionPointParentH parentId = null) {

        bool result = await HProjectManager.Instance.AddActionPoint(name, parentId);
        if (result) {
            // HActionPickerMenu.Instance.Show(selectedObject);
            setSelectedAction(actionAddClicked);
            //lockObject
            HProjectManager.Instance.OnActionAddedToScene += OnActionAddedToScene;

        }

    }


     public async Task<bool> LockObject(HInteractiveObject interactiveObject, bool lockTree) {

       
        if (await interactiveObject.WriteLock(lockTree)) {
            lockedObjects.Add(interactiveObject);
            return true;
        }
      
        return false;
    }

    public async Task<RequestResult> UnlockAllObjects() {

        if (GameManagerH.Instance.ConnectionStatus == GameManagerH.ConnectionStatusEnum.Disconnected) {
            lockedObjects.Clear();
            return new RequestResult(true);
        }
        for (int i = lockedObjects.Count - 1; i >= 0; --i) {
            if (lockedObjects[i].IsLockedByMe) {
                if (!await lockedObjects[i].WriteUnlock()) {
                    return new RequestResult(false, $"Failed to unlock {lockedObjects[i].GetName()}");
                }
                if (lockedObjects[i] is CollisionObjectH co) {
                    await co.WriteUnlockObjectType();
                }
                
                lockedObjects.RemoveAt(i);
            }
        }
        return new RequestResult(true);
    }

    public void removeObjectFromLocked(HInteractiveObject interactiveObject) {
        lockedObjects.Remove(interactiveObject);

    }

    public bool isSomethingLocked(){
        return lockedObjects.Count > 0;
    }

    public async void updateTransformBeforeUnlock() {
        await HTransformMenu.Instance.updatePosition();
           HTransformMenu.Instance.deactiveTransform();
    }
    public async void  unlockObject(){


        if (lastClicked == ClickedEnum.Transform){
            updateTransformBeforeUnlock();
        }

        if (isSomethingLocked()) {

            await UnlockAllObjects();
        }
    }

    /// <summary>
    /// Waits until websocket is null and calls callback method (because after application pause disconnecting isn't finished completely)
    /// </summary>
    /// <param name="callback"></param>
    /// <returns></returns>
    private IEnumerator WaitUntilLastTransformDestroyed(UnityAction callback) {
        yield return new WaitWhile(() => !HTransformMenu.Instance.isDeactivated());
        callback();
    }

    public void OnSelectObject(HInteractiveObject selectedObject){
       // lastClicked = null;
       this.selectedObject = selectedObject;
        if (lastClicked == ClickedEnum.Transform && HHandMenuManager.Instance.getActualClicked().Equals(HHandMenuManager.AllClickedEnum.Transform)){
            unlockObject();
            if(! HTransformMenu.Instance.isDeactivated()){
                StartCoroutine(WaitUntilLastTransformDestroyed(() =>  selectedAction?.Invoke() ));
            }
            else {
                selectedAction?.Invoke();
            }

        }
       else{
            selectedAction?.Invoke();
       }
        
       

    }
}
