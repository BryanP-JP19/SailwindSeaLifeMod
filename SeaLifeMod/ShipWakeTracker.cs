using UnityEngine;

// Read-only observation of the currently controlled Sailwind vessel.
// Sail Info uses GameState.currentBoat.parent.GetComponent<Rigidbody>() and
// converts Rigidbody.velocity to knots with 1.94384f. We use horizontal speed
// to avoid heave from waves spuriously qualifying for wake riding.
public struct ShipWakeData
{
    public Transform Boat;
    public Rigidbody Body;
    public Vector3 Position;
    public Vector3 Forward;
    public Vector3 Right;
    public Vector3 Velocity;
    public float SpeedMetersPerSecond;
    public float SpeedKnots;
    public float BowExtent;
    public float HalfBeam;
    public bool IsMovingForward;
    public float ForwardVelocityDot;
    public float ChildVelocityDot;
}

public sealed class ShipWakeTracker
{
    private Rigidbody cachedBody;
    private Transform cachedBoat;
    private float bowExtent = 8f;
    private float halfBeam = 3f;
    private float nextLookup;
    private float nextSample;
    private bool available;
    private ShipWakeData last;

    public bool TryGet(out ShipWakeData data)
    {
        float now = Time.time;
        if (now >= nextSample)
        {
            nextSample = now + .15f;
            Refresh(now);
        }
        data = last;
        return available && data.Boat != null;
    }

    private void Refresh(float now)
    {
        if (cachedBody == null || cachedBoat == null || now >= nextLookup)
        {
            nextLookup = now + 1f;
            // Do not assume the player is on a vessel while loading or ashore.
            Transform boat = GameState.currentBoat;
            Rigidbody body = boat != null && boat.parent != null
                ? boat.parent.GetComponent<Rigidbody>() : null;
            if (body == null && boat != null) body = boat.GetComponentInParent<Rigidbody>();
            if (boat != cachedBoat || body != cachedBody)
            {
                cachedBoat = boat;
                cachedBody = body;
                MeasureBoat();
            }
        }
        available = false;
        if (cachedBody == null || cachedBoat == null) return;

        // Sail Info's BoatInfo reads heading from the PurchasableBoat parent.
        // GameState.currentBoat is a CHILD with a different local rotation.
        // Using its .forward made the Dhow report ~90 degrees of sideslip.
        Vector3 forward = Vector3.ProjectOnPlane(cachedBody.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < .01f) return;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(cachedBody.velocity, Vector3.up);
        float speed = horizontalVelocity.magnitude;
        Vector3 childForward = Vector3.ProjectOnPlane(cachedBoat.forward, Vector3.up);
        childForward = childForward.sqrMagnitude > .001f ? childForward.normalized : Vector3.zero;
        float parentDot = speed > .01f ? Vector3.Dot(horizontalVelocity / speed, forward) : 0f;
        float childDot = speed > .01f ? Vector3.Dot(horizontalVelocity / speed, childForward) : 0f;
        last = new ShipWakeData {
            Boat = cachedBoat,
            Body = cachedBody,
            Position = cachedBody.position,
            Forward = forward,
            Right = right,
            Velocity = horizontalVelocity,
            SpeedMetersPerSecond = speed,
            SpeedKnots = speed * 1.94384f,
            BowExtent = bowExtent,
            HalfBeam = halfBeam,
            IsMovingForward = speed > .5f && parentDot > .55f,
            ForwardVelocityDot = parentDot,
            ChildVelocityDot = childDot
        };
        available = true;
    }

    // Estimate the footprint from colliders on the boat's own main Rigidbody.
    // Other rigidbodies (loose cargo, players, etc.) must not expand the footprint.
    // Caps guard against bad/huge non-hull colliders; these are approximations,
    // not authoritative geometry. Existing sphere casts still handle actual hulls.
    private void MeasureBoat()
    {
        bowExtent = 8f;
        halfBeam = 3f;
        if (cachedBody == null || cachedBoat == null) return;
        Vector3 forward = Vector3.ProjectOnPlane(cachedBody.transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float largestForward = 0f;
        float largestSide = 0f;
        int hullCandidates = 0;
        Collider[] colliders = cachedBody.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            if (collider == null || !collider.enabled || collider.isTrigger ||
                collider.attachedRigidbody != cachedBody) continue;
            Bounds b = collider.bounds;
            if (b.size.sqrMagnitude < .01f) continue;
            // Avoid unrelated objects high up the mast.
            if (Mathf.Abs(b.center.y - cachedBody.position.y) > 18f) continue;
            Vector3 centerOffset = b.center - cachedBody.position;
            float fwdExtent = Mathf.Abs(forward.x) * b.extents.x +
                              Mathf.Abs(forward.y) * b.extents.y + Mathf.Abs(forward.z) * b.extents.z;
            float sideExtent = Mathf.Abs(right.x) * b.extents.x +
                               Mathf.Abs(right.y) * b.extents.y + Mathf.Abs(right.z) * b.extents.z;
            largestForward = Mathf.Max(largestForward, Vector3.Dot(centerOffset, forward) + fwdExtent);
            largestSide = Mathf.Max(largestSide, Mathf.Abs(Vector3.Dot(centerOffset, right)) + sideExtent);
            hullCandidates++;
        }
        if (hullCandidates > 0)
        {
            bowExtent = Mathf.Clamp(largestForward, 3f, 35f);
            halfBeam = Mathf.Clamp(largestSide, 2f, 15f);
        }
    }
}
