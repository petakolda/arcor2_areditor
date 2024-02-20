using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Events;
using Microsoft.MixedReality.Toolkit.UI;
using Base;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;
using IO.Swagger.Model;
using TMPro;





public class HHandMenuManager : Singleton<HHandMenuManager>
{
    // Start is called before the first frame update


    public GameObject transfomButton;
    public GameObject copyObjectButton;
  //  public GameObject closeMenuButton;
 //   public gameobject closedetailbutton;

    public GameObject deleteButton;

    public GameObject addAPButton;
    public GameObject addConnectionButton;
    public Interactable addModelButton;
    public Interactable addObjectButton;

    public GameObject oneMeterButton;
    public GameObject rightAngleButton;

    public Interactable closeButton;

    public Interactable allAddButtons;
    public Interactable allMoreButtons;
    public Interactable allBpTestButtons;

    public Interactable showScenesButton;

    public Interactable showProjectsButton;

    public Interactable createProject;

    public GameObject models;

    public GameObject collisons;

    public GameObject moreButtons;
    public GameObject addButtons;
    public GameObject BpTestButtons;

    public TextMeshPro editorStatus;

     public enum AllClickedEnum {
        Transform,
        Delete,
        Rename,
        Copy,
        AddConnection,
        Close,
      
        AddAP,
        AddAction,
        AddModel,
        AddObject,
        OpenScenes,
        OpenProjects,
        OneMeter,
        RightAngle,

        AllAdd,
        AllMore,
        AllBpTest,

        CreateProject,
        None
    }

    AllClickedEnum actualClick;
    AllClickedEnum lastClick;

   
    private bool scenesLoaded, projectsLoaded, packagesLoaded, scenesUpdating, projectsUpdating, packagesUpdating;
    private bool wasLastUpdate = false;

    public Dictionary<AllClickedEnum, UnityAction> listOfPreviousActions =  new Dictionary<AllClickedEnum, UnityAction>();
        public Dictionary<AllClickedEnum, UnityAction> listOfNextActions =  new Dictionary<AllClickedEnum, UnityAction>();


   private void Awake() {
        scenesLoaded = projectsLoaded = scenesUpdating = projectsUpdating = packagesLoaded = packagesUpdating = false;
    }

    public void changeEditorStatus(object sender, HololensGameStateEventArgs args) {
        if(args.Data == (GameManagerH.GameStateEnum.SceneEditor)) {
            editorStatus.text =  "Scene";
        }
        else if(args.Data == (GameManagerH.GameStateEnum.ProjectEditor)) {
            editorStatus.text =  "Project";
            }
        else {
            editorStatus.text = "Menu";
        }
    }
    void Start()
    {
        actualClick = AllClickedEnum.None;

        listOfPreviousActions.Add(AllClickedEnum.Transform, UnselectTransform);   
        listOfPreviousActions.Add(AllClickedEnum.AddAP, UnselectAddActionPoint);   
        listOfPreviousActions.Add(AllClickedEnum.AddConnection, UnselectAddConnection);   
        listOfPreviousActions.Add(AllClickedEnum.AddModel, UnselectAddModel);   
        listOfPreviousActions.Add(AllClickedEnum.AddObject, UnselectAddObject);
        listOfPreviousActions.Add(AllClickedEnum.OpenScenes, UnselectOpenProjectsScenes);
        listOfPreviousActions.Add(AllClickedEnum.OpenProjects, UnselectOpenProjectsScenes);
        listOfPreviousActions.Add(AllClickedEnum.AllAdd,  UnselectAddAll);
        listOfPreviousActions.Add(AllClickedEnum.AllMore, UnselectAddMore);
        listOfPreviousActions.Add(AllClickedEnum.AllBpTest, UnselectAddBpTest);
        //listOfPreviousActions.Add(AllClickedEnum.OneMeter, UnselectOneMeter);
        //listOfPreviousActions.Add(AllClickedEnum.RightAngle, UnselectRightAngle);

        listOfNextActions.Add(AllClickedEnum.Transform, ( () => HSelectorManager.Instance.setSelectedAction(HSelectorManager.Instance.transformClicked) ) );
        listOfNextActions.Add(AllClickedEnum.Delete,  ( () => HSelectorManager.Instance.setSelectedAction(HSelectorManager.Instance.deleteClicked) ));
        listOfNextActions.Add(AllClickedEnum.Copy,  ( () => HSelectorManager.Instance.setSelectedAction(HSelectorManager.Instance.copyObjectClicked) ));
        listOfNextActions.Add(AllClickedEnum.AddConnection,  ( () => HSelectorManager.Instance.setSelectedAction(HSelectorManager.Instance.addOutputConnctionClicked) ));
        listOfNextActions.Add(AllClickedEnum.AddAP,  ( () => HSelectorManager.Instance.clickedAddAPButton() ));
        listOfNextActions.Add(AllClickedEnum.OpenScenes,  OpenScenes);
        listOfNextActions.Add(AllClickedEnum.OpenProjects, OpenProjects);
        listOfNextActions.Add(AllClickedEnum.OneMeter, (() => HSelectorManager.Instance.clickedOneMeterButton()));
        listOfNextActions.Add(AllClickedEnum.RightAngle, (() => Debug.Log("Pridavam ap na KOLMICI")));
        listOfNextActions.Add(AllClickedEnum.Close, HEditorMenuScreen.Instance.CloseScene);
        listOfNextActions.Add(AllClickedEnum.AllAdd,( () => addButtons.SetActive(true)));
        listOfNextActions.Add(AllClickedEnum.AllMore, (() => moreButtons.SetActive(true)));
        listOfNextActions.Add(AllClickedEnum.AllBpTest, (() => BpTestButtons.SetActive(true)));
        listOfNextActions.Add(AllClickedEnum.CreateProject,( () => CreateProject()));

        /**ALL BUTTONS*/

        transfomButton.GetComponent<Interactable>().OnClick.AddListener(() => onSeletectedChanged(AllClickedEnum.Transform));
        deleteButton.GetComponent<Interactable>().OnClick.AddListener(() => onSeletectedChanged(AllClickedEnum.Delete));
        copyObjectButton.GetComponent<Interactable>().OnClick.AddListener(() => onSeletectedChanged(AllClickedEnum.Copy));
        addConnectionButton.GetComponent<Interactable>().OnClick.AddListener(() => onSeletectedChanged(AllClickedEnum.AddConnection));
        addAPButton.GetComponent<Interactable>().OnClick.AddListener(() => onSeletectedChanged(AllClickedEnum.AddAP));
        addModelButton.OnClick.AddListener(() => onSeletectedChanged(AllClickedEnum.AddModel));
        addObjectButton.OnClick.AddListener(() => onSeletectedChanged(AllClickedEnum.AddObject));
        showScenesButton.OnClick.AddListener(() => {onSeletectedChanged(AllClickedEnum.OpenScenes); });
        showProjectsButton.OnClick.AddListener(() => {onSeletectedChanged(AllClickedEnum.OpenProjects);});
        oneMeterButton.GetComponent<Interactable>().OnClick.AddListener(() => onSeletectedChanged(AllClickedEnum.OneMeter));
        rightAngleButton.GetComponent<Interactable>().OnClick.AddListener(() => onSeletectedChanged(AllClickedEnum.RightAngle));

        //closeButton.OnClick.AddListener(() => {onSeletectedChanged(AllClickedEnum.Close);});

        allAddButtons.OnClick.AddListener(() => onSeletectedChanged(AllClickedEnum.AllAdd));

        allMoreButtons.OnClick.AddListener(() => onSeletectedChanged(AllClickedEnum.AllMore));
        createProject.OnClick.AddListener(() => onSeletectedChanged(AllClickedEnum.CreateProject));

        allBpTestButtons.OnClick.AddListener(() => onSeletectedChanged(AllClickedEnum.AllBpTest)); 

        GameManagerH.Instance.OnGameStateChanged += changeEditorStatus;



    }


     public AllClickedEnum getActualClicked(){
         return actualClick;
     }

   protected virtual void OnObjectLockingEvent(object sender, ObjectLockingEventArgs args) {
       if(!args.Locked){
            HLockingEventCache.Instance.OnObjectLockingEvent -= OnObjectLockingEvent;
             if(listOfNextActions.TryGetValue(actualClick, out UnityAction nextAction)){
                  nextAction?.Invoke();
             }
       }
    }
    public void onSeletectedChanged(AllClickedEnum clicked){

        if (GameManagerH.Instance.GetGameState().Equals(GameManagerH.GameStateEnum.ProjectEditor) &&
            clicked.Equals(AllClickedEnum.Transform) || clicked.Equals(AllClickedEnum.Delete) || clicked.Equals(AllClickedEnum.Copy) ||
             clicked.Equals(AllClickedEnum.AddConnection) || clicked.Equals(AllClickedEnum.AddAP)){
                GameManagerH.Instance. EnableInteractWthActionObjects(false);
                wasLastUpdate = true;
         }
         else if (wasLastUpdate){
            GameManagerH.Instance. EnableInteractWthActionObjects(true);
            wasLastUpdate = false;
         }

        if(actualClick.Equals(AllClickedEnum.None)){
            if(listOfNextActions.TryGetValue(clicked, out UnityAction nextAction)){ 
                actualClick = clicked;
                nextAction?.Invoke();
               
            }
        }
       
       else if(clicked != actualClick){
            if(listOfPreviousActions.TryGetValue(actualClick, out UnityAction lastAction)){
                 
                actualClick = clicked;
                lastAction?.Invoke();
                
            } 
            else if(listOfNextActions.TryGetValue(clicked, out UnityAction nextAction)){
                actualClick = clicked;
                nextAction?.Invoke();
               
            }
            actualClick = clicked;
           
        }
        else {
            if(listOfPreviousActions.TryGetValue(actualClick, out UnityAction lastAction)){
                actualClick = AllClickedEnum.None;
                lastAction?.Invoke();
            }

        }
        /*else  if(clicked.Equals(AllClickedEnum.Close)){
                HEditorMenuScreen.Instance.CloseScene();
        }*/
    }

    // Update is called once per frame
  
  public void UnselectTransform(){

       HSelectorManager.Instance.setSelectedAction(null);

      if(HSelectorManager.Instance.isSomethingLocked()){
          HLockingEventCache.Instance.OnObjectLockingEvent += OnObjectLockingEvent;
          HSelectorManager.Instance.unlockObject();
      }
      else {
          HSelectorManager.Instance.updateTransformBeforeUnlock();
          if(listOfNextActions.TryGetValue(actualClick, out UnityAction nextAction)){
                  nextAction?.Invoke();
          }
      }
    
  }

  public void UnselectAddActionPoint(){

    if(HSelectorManager.Instance.isClickedAddAP()){
            HActionPickerMenu.Instance.cancelAction();
    }
    HSelectorManager.Instance.setSelectedAction(null);

    if(HSelectorManager.Instance.isSomethingLocked()){
        HLockingEventCache.Instance.OnObjectLockingEvent += OnObjectLockingEvent;
        HSelectorManager.Instance.unlockObject();
    }else if(listOfNextActions.TryGetValue(actualClick, out UnityAction nextAction)){
        nextAction?.Invoke();        
    }

       
  }

  public void UnselectAddConnection(){

      if(HSelectorManager.Instance.isClickedOutputConnection()){
          HConnectionManagerArcoro.Instance.DestroyConnectionToMouse(); 
      }
     
      HSelectorManager.Instance.setSelectedAction(null);
      HSelectorManager.Instance.setLastClicked(HSelectorManager.ClickedEnum.None);
      
       if(HSelectorManager.Instance.isSomethingLocked()){
          HLockingEventCache.Instance.OnObjectLockingEvent += OnObjectLockingEvent;
          HSelectorManager.Instance.unlockObject();
      }else if(listOfNextActions.TryGetValue(actualClick, out UnityAction nextAction)){
                  nextAction?.Invoke();
      }
      
  }

   public void UnselectOpenProjectsScenes(){
      ListScenes.Instance.setActiveMenu(false);
      if(listOfNextActions.TryGetValue(actualClick, out UnityAction nextAction)){
                  nextAction?.Invoke();
      }
  }

  
  public void UnselectAddModel(){
      models.SetActive(false);
      if(listOfNextActions.TryGetValue(actualClick, out UnityAction nextAction)){
                  nextAction?.Invoke();
      }
  }

  public void UnselectAddObject(){
      collisons.SetActive(false);
      if(listOfNextActions.TryGetValue(actualClick, out UnityAction nextAction)){
                  nextAction?.Invoke();
      }

  }

  public void UnselectAddAll(){
      addButtons.SetActive(false);
      if(listOfNextActions.TryGetValue(actualClick, out UnityAction nextAction)){
                  nextAction?.Invoke();
      }

  }

    public void UnselectAddMore(){
        moreButtons.SetActive(false);
        if(listOfNextActions.TryGetValue(actualClick, out UnityAction nextAction)){
                  nextAction?.Invoke();
      }
      
  }

    public void UnselectAddBpTest() {
        BpTestButtons.SetActive(false);
        if (listOfNextActions.TryGetValue(actualClick, out UnityAction nextAction)) {
            nextAction?.Invoke();
        }
    }

    public async void OpenScenes() {

       

        if (!scenesUpdating) {
            scenesUpdating = true;
            scenesLoaded = false;
            WebSocketManagerH.Instance.LoadScenes(LoadScenesCb);
        }
        try {
            await WaitUntilScenesLoaded();

           // scenesScreen.SetActive(true);

     //       AddNewBtn.SetDescription("Add scene");

        } catch (TimeoutException ex) {            
            HNotificationManager.Instance.ShowNotification("Failed to switch to scenes");
        } finally {
          //  GameManager.Instance.HideLoadingScreen(true);
        }
    }


    private async void CreateProject() {
        string nameOfNewProject = "project_" + Guid.NewGuid().ToString().Substring(0,4); 
  
        try {
            await WebSocketManagerH.Instance.CreateProject(nameOfNewProject,
            SceneManagerH.Instance.SceneMeta.Id,
            "",
            true,
            false);
        } catch (RequestFailedException ex) {
            Debug.LogError("Failed to create new project" + ex.Message);
        }
       
    }


    public async void OpenProjects() {
      /*  if (!scenesUpdating) {
            scenesUpdating = true;
            scenesLoaded = false;
            WebSocketManagerH.Instance.LoadScenes(LoadScenesCb);
        }*/
        try {
           // await WaitUntilScenesLoaded();
            if (!projectsUpdating) {
                projectsUpdating = true;
                projectsLoaded = false;
                WebSocketManagerH.Instance.LoadProjects(LoadProjectsCb);
            }
            await WaitUntilProjectsLoaded();
        
          //  projectsScreen.SetActive(true);
           
        } catch (TimeoutException ex) {
            HNotificationManager.Instance.ShowNotification("Failed to switch to projects");
        } finally {
           // GameManager.Instance.HideLoadingScreen(true);
        }
        
    }

    public void LoadScenesCb(string id, string responseData) {
        IO.Swagger.Model.ListScenesResponse response = JsonConvert.DeserializeObject<IO.Swagger.Model.ListScenesResponse>(responseData);
        
        if (response == null || !response.Result) {
             HNotificationManager.Instance.ShowNotification("Failed to load scenes");
            scenesUpdating = false;
            return;
        }
        GameManagerH.Instance.Scenes = response.Data;
        GameManagerH.Instance.Scenes.Sort(delegate (ListScenesResponseData x, ListScenesResponseData y) {
            return y.Modified.CompareTo(x.Modified);
        });
        scenesUpdating = false;
        scenesLoaded = true;
        GameManagerH.Instance.InvokeScenesListChanged();
    }

    
    public void LoadProjectsCb(string id, string responseData) {
        IO.Swagger.Model.ListProjectsResponse response = JsonConvert.DeserializeObject<IO.Swagger.Model.ListProjectsResponse>(responseData);
        if (response == null) {
            HNotificationManager.Instance.ShowNotification("Failed to load projects");
            return;
        }
        GameManagerH.Instance.Projects = response.Data;
        GameManagerH.Instance.Projects.Sort(delegate (ListProjectsResponseData x, ListProjectsResponseData y) {
            return y.Modified.CompareTo(x.Modified);
        });
        projectsUpdating = false;
        projectsLoaded = true;
        GameManagerH.Instance.InvokeProjectsListChanged();
    }

    private async Task WaitUntilScenesLoaded() {
        await Task.Run(() => {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            while (true) {
                if (sw.ElapsedMilliseconds > 5000)
                    throw new TimeoutException("Failed to load scenes");
                if (scenesLoaded) {
                    return true;
                } else {
                    Thread.Sleep(10);
                }
            }
        });
    }

    private async Task WaitUntilProjectsLoaded() {
        await Task.Run(() => {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            while (true) {
                if (sw.ElapsedMilliseconds > 5000)
                    throw new TimeoutException("Failed to load projects");
                if (projectsLoaded) {
                    return true;
                } else {
                    Thread.Sleep(10);
                }
            }
        });

    }
}
