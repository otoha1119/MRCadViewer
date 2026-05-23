using System.Collections.Generic;
using UnityEngine;

public class CADHandInteractionController : MonoBehaviour
{
    public enum InteractionMode
    {
        Idle,
        SingleHand,
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
    public LineRenderer leftRayLine;
    public float rayWidth = 0.005f;
    public Color rayIdleColor = Color.white;
    public Color rayHitColor = Color.yellow;
    public Color rayGrabbingColor = Color.green;
    public Color rayTranslatingColor = Color.cyan;

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
    [SerializeField] private bool rightRotating;
    [SerializeField] private bool leftTranslating;
    [SerializeField] private bool debugRightRayHit;
    [SerializeField] private bool debugLeftRayHit;
    [SerializeField] private bool debugLeftPinching;
    [SerializeField] private bool debugRightPinching;
    [SerializeField] private bool debugRightPointerValid;
    [SerializeField] private bool debugLeftPointerValid;

    private Quaternion grabInitialRightHandRot;
    private Quaternion grabInitialObjRot;

    private Vector3 grabInitialLeftHandPos;
    private Vector3 grabInitialObjPos;

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

        InitRayLine(rightRayLine);
        InitRayLine(leftRayLine);
    }

    private void InitRayLine(LineRenderer line)
    {
        if (line == null) return;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = rayWidth;
        line.endWidth = rayWidth;
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

        bool hasRightRay = TryGetRightHandRay(out Ray rightRay);
        bool hasLeftRay = TryGetLeftHandRay(out Ray leftRay);
        debugRightPointerValid = hasRightRay;
        debugLeftPointerValid = hasLeftRay;

        bool rightRayHit = hasRightRay && RayHitsTarget(rightRay);
        bool leftRayHit = hasLeftRay && RayHitsTarget(leftRay);
        debugRightRayHit = rightRayHit;
        debugLeftRayHit = leftRayHit;

        if (currentMode == InteractionMode.TwoHandScaling)
        {
            if (!debugLeftPinching || !debugRightPinching)
            {
                currentMode = InteractionMode.Idle;
            }
            else
            {
                UpdateTwoHandScale();
            }
        }
        else
        {
            // Both pinching simultaneously takes priority and overrides single-hand actions.
            if (debugLeftPinching && debugRightPinching)
            {
                rightRotating = false;
                leftTranslating = false;
                BeginTwoHandScale();
                currentMode = InteractionMode.TwoHandScaling;
            }
            else
            {
                UpdateRightHandState(rightRayHit);
                UpdateLeftHandState(leftRayHit);

                currentMode = (rightRotating || leftTranslating)
                    ? InteractionMode.SingleHand
                    : InteractionMode.Idle;
            }
        }

        UpdateRayVisual(rightRayLine, hasRightRay, rightRay, rightRayHit, rightRotating, rayGrabbingColor);
        UpdateRayVisual(leftRayLine, hasLeftRay, leftRay, leftRayHit, leftTranslating, rayTranslatingColor);
        UpdateDebugVisual(rightRayHit || leftRayHit);
    }

    private void UpdateRightHandState(bool rightRayHit)
    {
        if (rightRotating)
        {
            if (!debugRightPinching) rightRotating = false;
            else UpdateRightRotation();
            return;
        }
        if (debugRightPinching && rightRayHit && BeginRightRotation())
        {
            rightRotating = true;
        }
    }

    private void UpdateLeftHandState(bool leftRayHit)
    {
        if (leftTranslating)
        {
            if (!debugLeftPinching) leftTranslating = false;
            else UpdateLeftTranslation();
            return;
        }
        if (debugLeftPinching && leftRayHit && BeginLeftTranslation())
        {
            leftTranslating = true;
        }
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

    private bool TryGetLeftHandRay(out Ray ray)
    {
        ray = default;
        if (leftHand == null || !leftHand.IsTracked) return false;
        if (!leftHand.IsPointerPoseValid) return false;
        Transform pose = leftHand.PointerPose;
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

    private bool BeginRightRotation()
    {
        if (rightHand == null || !rightHand.IsPointerPoseValid) return false;
        Transform pose = rightHand.PointerPose;
        if (pose == null) return false;
        grabInitialRightHandRot = pose.rotation;
        grabInitialObjRot = targetObject.rotation;
        return true;
    }

    private void UpdateRightRotation()
    {
        if (rightHand == null || !rightHand.IsPointerPoseValid) return;
        Transform pose = rightHand.PointerPose;
        if (pose == null) return;

        Quaternion rotDelta = pose.rotation * Quaternion.Inverse(grabInitialRightHandRot);

        // Invert pitch (X) and yaw (Y) so the model turns toward the direction
        // the user moves the hand. Roll (Z) is preserved.
        rotDelta.ToAngleAxis(out float angle, out Vector3 axis);
        axis.x = -axis.x;
        axis.y = -axis.y;
        Quaternion invertedDelta = Quaternion.AngleAxis(angle, axis);

        targetObject.rotation = invertedDelta * grabInitialObjRot;
    }

    private bool BeginLeftTranslation()
    {
        if (leftHand == null || !leftHand.IsPointerPoseValid) return false;
        Transform pose = leftHand.PointerPose;
        if (pose == null) return false;
        grabInitialLeftHandPos = pose.position;
        grabInitialObjPos = targetObject.position;
        return true;
    }

    private void UpdateLeftTranslation()
    {
        if (leftHand == null || !leftHand.IsPointerPoseValid) return;
        Transform pose = leftHand.PointerPose;
        if (pose == null) return;

        Vector3 handDelta = pose.position - grabInitialLeftHandPos;
        targetObject.position = grabInitialObjPos + handDelta;
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

    private void UpdateRayVisual(LineRenderer line, bool hasRay, Ray ray, bool hit, bool active, Color activeColor)
    {
        if (line == null) return;
        if (!hasRay)
        {
            line.enabled = false;
            return;
        }
        line.enabled = true;
        line.positionCount = 2;
        line.SetPosition(0, ray.origin);
        line.SetPosition(1, ray.origin + ray.direction * maxRayDistance);

        Color c = rayIdleColor;
        if (active) c = activeColor;
        else if (hit) c = rayHitColor;
        line.startColor = c;
        line.endColor = c;
    }

    private void UpdateDebugVisual(bool anyRayHit)
    {
        if (debugMaterial == null) return;
        Color c = colorIdle;
        switch (currentMode)
        {
            case InteractionMode.SingleHand:
                c = colorGrabbing;
                break;
            case InteractionMode.TwoHandScaling:
                c = colorScaling;
                break;
            case InteractionMode.Idle:
                if (anyRayHit) c = colorRayHit;
                break;
        }
        if (debugMaterial.HasProperty("_BaseColor")) debugMaterial.SetColor("_BaseColor", c);
        else if (debugMaterial.HasProperty("_Color")) debugMaterial.SetColor("_Color", c);
    }
}
