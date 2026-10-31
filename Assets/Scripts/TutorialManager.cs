using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class TutorialManager : MonoBehaviour
{
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private bool loading;
    private bool bothTriggersWerePressed;
    private InputActionMap actions;
    private InputAction leftTrigger;
    private InputAction rightTrigger;

    private void Awake()
    {
        actions = new InputActionMap("Tutorial Hands");
        leftTrigger = actions.AddAction("Left trigger", InputActionType.Button,
            "<XRController>{LeftHand}/triggerPressed");
        rightTrigger = actions.AddAction("Right trigger", InputActionType.Button,
            "<XRController>{RightHand}/triggerPressed");
    }

    private void OnEnable()
    {
        actions.Enable();
    }

    private void OnDisable()
    {
        actions.Disable();
        bothTriggersWerePressed = false;
    }

    private void OnDestroy()
    {
        actions.Dispose();
    }

    private void Update()
    {
        if (loading) return;

        // The presses only need to overlap; they need not begin on the same frame.
        bool bothPressed = leftTrigger.IsPressed() && rightTrigger.IsPressed();
        bool shouldLoad = bothPressed && !bothTriggersWerePressed;
        bothTriggersWerePressed = bothPressed;
        if (!shouldLoad) return;

        if (!Application.CanStreamedLevelBeLoaded(MainScenePath))
        {
            Debug.LogError($"Add {MainScenePath} to the enabled Build Settings scenes.", this);
            return;
        }

        // Single mode unloads the tutorial. MainScene owns its player and spawn point.
        loading = true;
        SceneManager.LoadSceneAsync(MainScenePath, LoadSceneMode.Single);
    }

}
