using System.Collections;
using System.Collections.Generic;
using CloudXR;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
// using UnityEngine.XR;

public class CxruClientSampleOculusUI : MonoBehaviour
{
    public string serverAddress = "192.168.0.1";

    public Material mat = null;
    private CxruLogHandler logger = null;
    CloudXRManager cxrManager = null;
    public GameObject PanelError = null;
    public GameObject PanelMain = null;
    // private XRInputSubsystem m_inputSubsystem = null;
    public TMP_InputField AddressInput;
    public InputAction ToggleWindowAction;
    private bool running = false, connecting = false;
    public float Opacity=0.0f;
    public bool readLastAddress = false;
    public bool forceMenuPosition = false;
    private bool menuMoving=false;
    private Vector3 menuTargetPosition = new Vector3(0.0f, 0.0f, 0.0f);
    public float smoothSpeed = 0.1f;
    
    public int ViewBorderMode=0;
    public int EffectIndex=0;
    private Vector3 GetMenuForwardPosition(){
        Vector3 menu_offset = new Vector3(0.0f, 0.0f, 1.5f);
        Vector3 direction = Camera.main.transform.rotation * menu_offset;
        Vector3 dest=Camera.main.transform.position + direction;
        return dest;
    }
    public bool isConnected {
        get {
            return running;
        }
    }

    private XRInteractorLineVisual right_ray_visual,left_ray_visual;
    void Start()
    {

        logger = new CxruLogHandler(Debug.unityLogger.logHandler,"CxruGuiSampleLog");
        Debug.unityLogger.logHandler = logger;

        if (readLastAddress && PlayerPrefs.HasKey("CXR_LastServerAddress"))
        {
            AddressInput.text = PlayerPrefs.GetString("CXR_LastServerAddress");
        }else{
            AddressInput.text = serverAddress;
        }
        Opacity=0.0f;
        ToggleWindowAction.Enable();
        menuTargetPosition = GetMenuForwardPosition();
        menuMoving = true;


        cxrManager = Camera.main.GetComponent<CloudXRManager>();
        if (cxrManager == null)
        {
            Debug.Assert(false,"CloudXRManager is not attached to the main camera");
            return;
        }
        cxrManager.statusChanged += OnCxrStatusChange;

        // Change this to "true" if there are color space problems on Android.
        cxrManager.config.cxrLibraryConfig.cxrDebugConfig.outputLinearRGBColor = false;

        // Setup bindings.
        cxrManager.SetBindings(CxruSampleBindings.oculusTouchBindings);


        right_ray_visual = GameObject.Find("RightHand Controller").GetComponent<XRInteractorLineVisual>();
        left_ray_visual = GameObject.Find("LeftHand Controller").GetComponent<XRInteractorLineVisual>();
    }


    // ============================================================================
    // Managing input focus (e.g. dimming during system menu)
    private bool _isPaused = false;
    public bool isPaused {
        get {
            return _isPaused;
        }
    }
    void OnApplicationFocus(bool hasFocus)
    {
        Log.V($"Focus changed: {hasFocus}");
        _isPaused = !hasFocus;
        if(hasFocus){
            if(!PanelError.activeSelf){
                ResetWindow();
            }
            if(!running){
                ShowControllers();
            }
        }else{
            HideControllers();
        }
    }


    // ============================================================================
    // GUI specific code
    public void ResetWindow()
    {
        PanelMain.SetActive(true);
        Vector3 menu_offset = new Vector3(0.0f, 0.0f, 1.5f);
        Vector3 direction = Camera.main.transform.rotation * menu_offset;
        gameObject.transform.position = GetMenuForwardPosition();
        gameObject.transform.up = new Vector3(0.0f, 1.0f, 0.0f);
        gameObject.transform.rotation = Quaternion.LookRotation(direction);

        ShowControllers();
    }

    public void HideWindow()
    {
        PanelMain.SetActive(false);
        HideControllers();
    }

    public void ErrorShowWindow(string message = "")
    {
        PanelError.SetActive(true);
        PanelError.transform.GetChild(0).transform.Find("TextIP").GetComponent<TMP_Text>().text=serverAddress;
        PanelError.transform.GetChild(0).transform.Find("TextDescription").GetComponent<TMP_Text>().text=message;
        PanelMain.SetActive(false);

        ShowControllers();
    }

    public void ErrorAcknowledge()
    {
        PanelError.SetActive(false);
        PanelMain.SetActive(true);

        ShowControllers();
    }

    public void StartConnection()
    {
        serverAddress = AddressInput.text;
        Debug.Log($"CXR Address: {serverAddress}");



        cxrManager.server_ip = serverAddress;

        cxrManager.Connect();
        connecting = true;
    }
    public void StopConnection()
    {
        cxrManager.Disconnect();
    }
    public void HideControllers()
    {
        right_ray_visual.enabled = false;
        left_ray_visual.enabled = false;
    }

    public void ShowControllers()
    {
        right_ray_visual.enabled = true;
        left_ray_visual.enabled = true;
    }


    
    void OnCxrStatusChange(object sender, CloudXRManager.StatusChangeArgs e) {
        Debug.Log($"event fired! {e.new_status}");
        if (e.new_status == CloudXRManager.S.error) {
            Debug.Log($"Oh no! Error!");
            if (e.result != null) {
                Debug.Log($"{e.result.message}, cxr error {e.result.api_cxrError}: '{e.result.api_cxrErrorString}'");
            } 
            // cxrManager.Disconnect();
            // ResetWindow();
            running = false;
            ErrorShowWindow(e.result.message);
        }
        if (e.new_status == CloudXRManager.S.disconnected) {
            Debug.Log($"Disconnected");
            // cxrManager.Disconnect();
            // ResetWindow();
            running = false;
            ErrorShowWindow(e.result.message);
        }
        if (e.new_status == CloudXRManager.S.running) {
            running = true;
            connecting = false;
            PlayerPrefs.SetString("CXR_LastServerAddress",serverAddress);
            HideWindow();
        }
        else {
            running = false;
        }
    }
    public void NextEffect()
    {
        EffectIndex=(EffectIndex+1)%9;
    }
    public void NextBorderMode()
    {
        ViewBorderMode=(ViewBorderMode+1)%4;
    }

    XrPlatform? currentPlatform = null;
    void UpdatePlatform() {
        if (currentPlatform == null) {
            XrPlatform? platformCheck = cxrManager.GetXrPlatform();
            if (platformCheck == null) 
                return;
            XrPlatform runningPlatform = (XrPlatform)platformCheck;
            int actualWidth = runningPlatform.displays[0].width;
            int unityWidth = (int)cxrManager.GetUnityResolutionWidth();
            float scale = (float)actualWidth / (float)unityWidth;
            Log.W($"For platform {runningPlatform.make} {runningPlatform.model}, setting scaling factor to: {scale}");
            cxrManager.SetUnityResolutionScaling(scale);
            currentPlatform = runningPlatform;
        }
    }

    void Update()
    {
        UpdatePlatform();

        if(running){
            Opacity=Mathf.Min(1.0f,Opacity+0.03f);
        }else{
            Opacity=Mathf.Max(0.0f,Opacity-0.02f);
        }
        if (ToggleWindowAction.triggered)
        {
            ResetWindow();
            // _isPaused=!_isPaused;
        }
        if(forceMenuPosition){
            Vector3 menu_offset = new Vector3(0.0f, 0.0f, 1.5f);
            Vector3 direction = Camera.main.transform.rotation * menu_offset;
            Vector3 dest=GetMenuForwardPosition();
            double mag=Mathf.Max((gameObject.transform.position - menuTargetPosition).magnitude,(menuTargetPosition - dest).magnitude);

            // menuTargetPosition = GetMenuForwardPosition();
            //Camera.main.transform.position
            if(!menuMoving && (mag>0.9)){
                menuMoving=true;
                menuTargetPosition = dest;
            }
            if(menuMoving){       
                if(mag>0.15){
                    menuTargetPosition = dest;
                } 
                gameObject.transform.position = Vector3.Lerp(gameObject.transform.position, menuTargetPosition,smoothSpeed/Mathf.Sqrt((float)(1.0+mag*10.0)));            
                gameObject.transform.up = new Vector3(0.0f, 1.0f, 0.0f);
                gameObject.transform.rotation = Quaternion.LookRotation(direction);
                if((gameObject.transform.position - menuTargetPosition).magnitude<0.02){

                    menuMoving=false;
                }
            }
        }
        if(mat!=null){
            // mat.SetFloat("_Temp",0.33f);
            StereoPosedFrame theFrame = cxrManager.latestValidFrame;
            if(theFrame!=null){
                if(theFrame.leftFrame.tex != null && theFrame.rightFrame.tex != null){
                    mat.SetTexture("_LeftEyeTex", theFrame.leftFrame.tex);
                    mat.SetTexture("_RightEyeTex", theFrame.rightFrame.tex);
                }
                mat.SetMatrix("_StreamingRotation", Matrix4x4.TRS(Vector3.zero, theFrame.pose.poseInUnityCoords.angular_position, Vector3.one));
            }
            mat.SetFloat("_TransitionTime",Opacity);
            if (isConnected) {
                mat.SetFloat("_Connected",1.0f);
            }
            else{
                mat.SetFloat("_Connected",0.0f);
            }
            if (isPaused) {
                mat.SetFloat("_Paused",1.0f);
            }
            else{
                mat.SetFloat("_Paused",0.0f);
            }
            mat.SetInt("EffectIndex", EffectIndex);
            mat.SetInt("ViewBorderMode", ViewBorderMode);
        }
    }
}
