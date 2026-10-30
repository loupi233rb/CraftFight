using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using InputDevice = UnityEngine.InputSystem.InputDevice;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

/// <summary>Runs before XR Device Simulator so simulated and physical headsets never compete.</summary>
[DefaultExecutionOrder(-32000)]
[DisallowMultipleComponent]
public sealed class CraftXRSimulatorGate : MonoBehaviour
{
    public enum InputMode { Auto, Simulator, Headset }
    [SerializeField] private XRDeviceSimulator simulator;
    [SerializeField] private InputMode mode = InputMode.Auto;
    public static CraftXRSimulatorGate Active { get; private set; }
    public bool UsingSimulation { get; private set; }
    public InputMode Mode { get => mode; set { mode = value; RefreshMode(); } }
    private bool initialized;
    private float nextRefresh;
    private static readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();

    private void Awake()
    {
        Active = this;
        if (simulator == null) simulator = GetComponentInChildren<XRDeviceSimulator>(true);
        if (simulator != null) simulator.removeOtherHMDDevices = false;
        InputSystem.onDeviceChange += OnDeviceChange;
        RefreshMode();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .25f;
        RefreshMode();
    }
    private void OnDeviceChange(InputDevice device, InputDeviceChange change) => nextRefresh = 0f;

    public static bool IsPhysicalHeadset(InputDevice device) =>
        device is XRHMD && !(device is XRSimulatedHMD) && device.added && device.enabled;

    public static bool HasPhysicalHeadset()
    {
        foreach (var device in InputSystem.devices)
            if (IsPhysicalHeadset(device)) return true;
        displays.Clear();
        SubsystemManager.GetInstances(displays);
        foreach (var display in displays)
            if (display.running) return true;
        return XRSettings.isDeviceActive;
    }

    public void RefreshMode()
    {
        bool simulation = mode == InputMode.Simulator ||
            (mode == InputMode.Auto && !HasPhysicalHeadset());
        bool changed = !initialized || UsingSimulation != simulation;
        UsingSimulation = simulation;
        initialized = true;
        if (simulator != null)
        {
            simulator.removeOtherHMDDevices = false;
            if (simulator.gameObject.activeSelf != simulation)
                simulator.gameObject.SetActive(simulation);
        }
        if (changed)
            Debug.Log("[CraftXR] Input mode: " + (simulation ? "Simulator" : "Physical headset") +
                ". Head and hands use one tracking space.", this);
    }

    private void OnDestroy()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        if (Active == this) Active = null;
    }
}
