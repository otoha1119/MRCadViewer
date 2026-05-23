using System.Collections.Generic;
using UnityEngine;

public class CADHandInteractionController : MonoBehaviour
{
    public enum InteractionMode
    {
        Idle,
        RemoteGrabbing,
        TwoHandScaling
    }

    [Header("Hand References")]
    public OVRHand leftHand;
    public OVRHand rightHand;
    public OVRSkeleton leftSkeleton;
    public OVRSkeleton rightSkeleton;

    [Header("Target")]
    public Transform targetObject;
    public BoxCollider targetCollider;

    [Header("Remote Grab Ray")]
    public float maxRayDistance = 10f;
    public LineRenderer rightRayLine;
    public float rayWidth = 0.005f;
    public Color rayIdleColor = Color.white;
    public Color rayHitColor = Color.yellow;
    public Color rayGrabbingColor = Color.green;

    [Header("Scale (Two-hand Pinch)")]
    public float minScaleFactor = 0.1f;
    public float maxScaleFactor = 10f;

    [Header("Coexistence")]
    public CADJoystickController joystickController;
    public bool disableJoystickWhenHandsTracked = true;

    [Header("Debug Visual")]
    public Renderer debugVisual;
    public Color colorIdle = Color.gray;
    public Color colorRayHit = Color.yellow;
    public Color colorGrabbing = Color.green;
    public Color colorScaling = Color.blue;

    [Header("Debug (read-only)")]
    [SerializeField] private InteractionMode currentMode = InteractionMode.Idle;
    [SerializeField] private bool debugRayHit;
    [SerializeField] private bool debugLeftPinching;
    [SerializeField] private bool debugRightPinching;
    [SerializeField] private bool debugPointerPoseValid;

    private Vector3 grabInitialHandPos;
    private Quaternion grabInitialHandRot;
    private Vector3 grabInitialObjPos;
    private Quaternion grabInitialObjRot;

    private float scaleInitialDistance;
    private Vector3 scaleInitialScale;

    private readonly Dictionary<OVRSkeleton.BoneId, Transform> leftBoneCache =
        new Dictionary<OVRSkeleton.BoneId, Transform>();
    private readonly Dictionary<OVRSkeleton.BoneId, Transform> rightBoneCache =
        new Dictionary<OVRSkeleton.BoneId, Transform>();

    private Material debugMaterial;

    private void Reset()
    {
        if (targetCollider == null) targetCollider = GetComponent<BoxCollider>();
        if (targetObject == null) targetObject = transform;
    }

    private void Awake()
    {
        if (targetCollider == null) targetCollider = GetComponent<BoxCollider>();
        if (targetObject == null) targetObject = transform;
        if (joystickController == null) joystickController = GetComponent<CADJoystickController>();
        if (debugVisual != null) debugMaterial = debugVisual.material;

        if (rightRayLine != null)
        {
            rightRayLine.useWorldSpace = true;
            rightRayLine.positionCount = 2;
            rightRayLine.startWidth = rayWidth;
            rightRayLine.endWidth = rayWidth;
        }
    }

    private void Update()
    {
        if (leftHand == null || !leftHand.IsTracked) leftBoneCache.Clear();
        if (rightHand == null || !rightHand.IsTracked) rightBoneCache.Clear();

        UpdateJoystickCoexistence();

        if (targetObject == null || targetCollider == null) return;

        debugLeftPinching = leftHand != null && leftHand.IsTracked &&
                            leftHand.GetFingerIsPinching(OVRHand.HandFinger.Index);
        debugRightPinching = rightHand != null && rightHand.IsTracked &&
                             rightHand.GetFingerIsPinching(OVRHand.HandFinger.Index);

        bool hasRay = TryGetRightHandRay(out Ray ray);
        debugPointerPoseValid = hasRay;
        bool rayHit = hasRay && RayHitsTarget(ray);
        debugRayHit = rayHit;

        switch (currentMode)
        {
            case InteractionMode.Idle:
                if (debugLeftPinching && debugRightPinching)
                {
                    BeginTwoHandScale();
                    currentMode = InteractionMode.TwoHandScaling;
                }
                else if (debugRightPinching && rayHit)
                {
                    BeginRemoteGrab();
                    currentMode = InteractionMode.RemoteGrabbing;
                }
                break;

            case InteractionMode.RemoteGrabbing:
                if (!debugRightPinching) currentMode = InteractionMode.Idle;
                else UpdateRemoteGrab();
                break;

            case InteractionMode.TwoHandScaling:
                if (!debugLeftPinching || !debugRightPinching) currentMode = InteractionMode.Idle;
                else UpdateTwoHandScale();
                break;
        }

        UpdateRayVisual(hasRay, ray, rayHit);
        UpdateDebugVisual(rayHit);
    }

    private void UpdateJoystickCoexistence()
    {
        if (!disableJoystickWhenHandsTracked || joystickController == null) return;

        bool bothHandsTracked =
            leftHand != null && leftHand.IsTracked &&
            rightHand != null && rightHand.IsTracked;

        joystickController.enabled = !bothHandsTracked;
    }

    private bool TryGetRightHandRay(out Ray ray)
    {
        ray = default;
        if (rightHand == null || !rightHand.IsTracked) return false;
        if (!rightHand.IsPointerPoseValid) return false;
        Transform pose = rightHand.PointerPose;
        if (pose == null) return false;
        ray = new Ray(pose.position, pose.forward);
        return true;
    }

    private bool RayHitsTarget(Ray ray)
    {
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance)) return false;
        if (hit.collider == targetCollider) return true;
        if (targetObject != null &&
            (hit.collider.transform == targetObject || hit.collider.transform.IsChildOf(targetObject)))
            return true;
        return false;
    }

    private void BeginRemoteGrab()
    {
        Transform pose = rightHand.PointerPose;
        grabInitialHandPos = pose.position;
        grabInitialHandRot = pose.rotation;
        grabInitialObjPos = targetObject.position;
        grabInitialObjRot = targetObject.rotation;
    }

    private void UpdateRemoteGrab()
    {
        if (!rightHand.IsPointerPoseValid) return;
        Transform pose = rightHand.PointerPose;
        if (pose == null) return;

        Vector3 handDelta = pose.position - grabInitialHandPos;
        targetObject.position = grabInitialObjPos + handDelta;

        Quaternion rotDelta = pose.rotation * Quaternion.Inverse(grabInitialHandRot);
        targetObject.rotation = rotDelta * grabInitialObjRot;
    }

    private void BeginTwoHandScale()
    {
        Vector3 leftPinch = GetPinchPosition(leftSkeleton, leftBoneCache);
        Vector3 rightPinch = GetPinchPosition(rightSkeleton, rightBoneCache);
        scaleInitialDistance = Mathf.Max(Vector3.Distance(leftPinch, rightPinch), 0.0001f);
        scaleInitialScale = targetObject.localScale;
    }

    private void UpdateTwoHandScale()
    {
        Vector3 leftPinch = GetPinchPosition(leftSkeleton, leftBoneCache);
        Vector3 rightPinch = GetPinchPosition(rightSkeleton, rightBoneCache);
        float currentDistance = Vector3.Distance(leftPinch, rightPinch);
        float ratio = currentDistance / scaleInitialDistance;

        Vector3 next = scaleInitialScale * ratio;
        next.x = Mathf.Clamp(next.x, minScaleFactor, maxScaleFactor);
        next.y = Mathf.Clamp(next.y, minScaleFactor, maxScaleFactor);
        next.z = Mathf.Clamp(next.z, minScaleFactor, maxScaleFactor);
        targetObject.localScale = next;
    }

    private Vector3 GetPinchPosition(OVRSkeleton skeleton, Dictionary<OVRSkeleton.BoneId, Transform> cache)
    {
        if (!EnsureBoneCache(skeleton, cache)) return Vector3.zero;
        if (!cache.TryGetValue(OVRSkeleton.BoneId.Hand_IndexTip, out Transform indexTip) || indexTip == null) return Vector3.zero;
        if (!cache.TryGetValue(OVRSkeleton.BoneId.Hand_ThumbTip, out Transform thumbTip) || thumbTip == null) return Vector3.zero;
        return (indexTip.position + thumbTip.position) * 0.5f;
    }

    private static bool EnsureBoneCache(OVRSkeleton skeleton, Dictionary<OVRSkeleton.BoneId, Transform> cache)
    {
        if (skeleton == null) return false;
        if (cache.Count > 0) return true;

        IList<OVRBone> bones = skeleton.Bones;
        if (bones == null || bones.Count == 0) return false;

        for (int i = 0; i < bones.Count; i++)
        {
            OVRBone bone = bones[i];
            if (bone == null || bone.Transform == null) continue;
            cache[bone.Id] = bone.Transform;
        }
        return cache.Count > 0;
    }

    private void UpdateRayVisual(bool hasRay, Ray ray, bool hit)
    {
        if (rightRayLine == null) return;
        if (!hasRay)
        {
            rightRayLine.enabled = false;
            return;
        }
        rightRayLine.enabled = true;
        rightRayLine.positionCount = 2;
        rightRayLine.SetPosition(0, ray.origin);
        rightRayLine.SetPosition(1, ray.origin + ray.direction * maxRayDistance);
        Color c = rayIdleColor;
        if (currentMode == InteractionMode.RemoteGrabbing) c = rayGrabbingColor;
        else if (hit) c = rayHitColor;
        rightRayLine.startColor = c;
        rightRayLine.endColor = c;
    }

    private void UpdateDebugVisual(bool rayHit)
    {
        if (debugMaterial == null) return;
        Color c = colorIdle;
        switch (currentMode)
        {
            case InteractionMode.RemoteGrabbing: c = colorGrabbing; break;
            case InteractionMode.TwoHandScaling: c = colorScaling; break;
            case InteractionMode.Idle:
                if (rayHit) c = colorRayHit;
                break;
        }
        if (debugMaterial.HasProperty("_BaseColor")) debugMaterial.SetColor("_BaseColor", c);
        else if (debugMaterial.HasProperty("_Color")) debugMaterial.SetColor("_Color", c);
    }
}
