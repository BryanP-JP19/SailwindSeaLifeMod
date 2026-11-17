using System.Collections.Generic;
using UnityEngine;
using Crest;

// Shared navigation, Boid steering, wave-following and despawning.
// No Rigidbody force is applied: swimming is kinematic as in the original mod.
public class MarineAnimalAI : MonoBehaviour
{
    // Lightweight registry for collision-only queries across separate pods.
    // This does NOT change the membership of any MarinePod or its Boid forces.
    internal static readonly List<MarineAnimalAI> ActiveAnimals = new List<MarineAnimalAI>();

    protected virtual void OnDestroy()
    {
        ActiveAnimals.Remove(this);
    }

    public MarineSpecies Species { get; private set; }
    public MarinePod Pod { get; private set; }
    protected Animator animator;
    protected float currentSpeed;
    protected float targetSpeed;
    protected Vector3 velocity;
    private float nextSpeedChange;
    private float nextCruiseJitterChange;
    private float cruiseJitter;
    private float cruiseJitterGoal;
    private float nextWakePaceChange;
    private float nextWakeDriftChange;
    private float wakePaceVariation;
    private float wakePaceVariationGoal;
    private float wakeApproachBonusKnots;
    private float wakeApproachBonusGoalKnots;
    private float nextWakeApproachBonusChange;
    private bool avoidingWakeShip;
    private Vector2 wakeDrift;
    private Vector2 wakeDriftGoal;
    private bool wasWakeEngaged;
    public float WakePaceVariation { get { return wakePaceVariation; } }
    public float WakeApproachBonusKnots { get { return wakeApproachBonusKnots; } }
    public float CurrentSwimSpeed { get { return currentSpeed; } }
    // Multipliers relative to the prefab's original transform.localScale.
    // Both values are fixed at spawn; derived species may add modifiers.
    public float RandomSizeMultiplier { get; private set; }
    public float TotalSizeMultiplier { get; private set; }
    public Vector3 SwimVelocity { get { return velocity; } }
    public bool AvoidingObstacle { get { return obstacleNearby || Time.time < avoidUntil; } }
    public Vector2 WakeDrift { get { return wakeDrift; } }
    private bool obstacleNearby;
    private float currentAnimationMultiplier = 1f;
    private bool hasSwimMultiplierParameter;
    private int swimParameterHash;
    private bool hasBendParameter;
    private int bendParameterHash;
    private bool hasSurfaceTrigger;
    private bool hasBreachTrigger;
    private int surfaceTriggerHash;
    private int breachTriggerHash;
    protected float turnRate;
    private float yawVelocity;
    protected Transform player;
    protected float bendAmount;
    private Vector3 wanderHeading;
    private Vector3 desiredHeading;
    private Vector3 avoidanceHeading;
    private float avoidUntil;
    private float nextNavigationCheck;
    private float nextDespawnCheck;
    private float nextSteeringCheck;
    private float nextWanderChange;
    private float nextAnimation;
    private bool initialized;
    private readonly Vector3[] queryPoints = new Vector3[1];
    private readonly Vector3[] resultDisps = new Vector3[1];
    private readonly float[] candidateAngles = { 0f, 25f, -25f, 50f, -50f, 85f, -85f, 130f, -130f };

    public void Initialize(MarineSpecies species, Transform playerTransform, MarinePod pod)
    {
        Species = species;
        player = playerTransform;
        Pod = pod;
        animator = GetComponentInChildren<Animator>(true);
        currentSpeed = Random.Range(species.MinSpeed, species.MaxSpeed);
        targetSpeed = currentSpeed;
        velocity = transform.forward * currentSpeed;
        nextSpeedChange = Time.time + Random.Range(species.SpeedChangeMinSeconds, species.SpeedChangeMaxSeconds);
        nextCruiseJitterChange = Time.time + Random.Range(3f, 8f);
        cruiseJitterGoal = Random.Range(-species.CruiseSpeedJitterFraction,
            species.CruiseSpeedJitterFraction);
        nextWakePaceChange = Time.time + Random.Range(2.5f, 6f);
        nextWakeDriftChange = Time.time + Random.Range(2f, 7f);
        // Same random-size function for every spawned species (including
        // Fin Whales, Orcas, and future marine animals).
        RandomSizeMultiplier = MarineScale.RollIndividualScale();
        TotalSizeMultiplier = RandomSizeMultiplier;
        transform.localScale *= RandomSizeMultiplier;
        wanderHeading = transform.forward;
        wanderHeading.y = 0;
        if (wanderHeading.sqrMagnitude < .01f) wanderHeading = Vector3.forward;
        desiredHeading = wanderHeading.normalized;
        nextWanderChange = Time.time + Random.Range(10f, 30f);
        nextAnimation = Time.time + Random.Range(30f, 90f);
        nextDespawnCheck = Time.time + 1f;
        nextNavigationCheck = Time.time + Random.Range(0f, .3f);
        nextSteeringCheck = Time.time;
        PrepareAnimator();
        ConfigureSpecies();
        initialized = true;
        if (!ActiveAnimals.Contains(this)) ActiveAnimals.Add(this);
    }

    // Optional additional modifier for species-specific anatomy, e.g. male
    // Orcas. The universal individual variation is always applied first.
    protected void ApplySpeciesSizeMultiplier(float multiplier)
    {
        float safeMultiplier = Mathf.Max(.01f, multiplier);
        transform.localScale *= safeMultiplier;
        TotalSizeMultiplier *= safeMultiplier;
    }

    // Called after generic initialization, including scale and Animator discovery.
    protected virtual void ConfigureSpecies() { }

    private bool HasAnimatorParameter(string name, AnimatorControllerParameterType type)
    {
        if (animator == null || string.IsNullOrEmpty(name)) return false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.name == name && parameter.type == type) return true;
        return false;
    }

    private void PrepareAnimator()
    {
        if (animator == null) return;
        hasSwimMultiplierParameter = HasAnimatorParameter(Species.SwimSpeedParameter, AnimatorControllerParameterType.Float);
        swimParameterHash = Animator.StringToHash(Species.SwimSpeedParameter);
        // Use the same BendAmount float for both Fin Whales and Orcas.
        hasBendParameter = HasAnimatorParameter("BendAmount", AnimatorControllerParameterType.Float);
        bendParameterHash = Animator.StringToHash("BendAmount");
        hasSurfaceTrigger = HasAnimatorParameter(Species.SurfaceTrigger, AnimatorControllerParameterType.Trigger);
        hasBreachTrigger = HasAnimatorParameter(Species.BreachTrigger, AnimatorControllerParameterType.Trigger);
        surfaceTriggerHash = Animator.StringToHash(Species.SurfaceTrigger);
        breachTriggerHash = Animator.StringToHash(Species.BreachTrigger);

        // Stagger looping swim phases WITHOUT staggering the pod's spawning.
        // Unity's state hash may include the 'Base Layer.' prefix.
        if (!string.IsNullOrEmpty(Species.SwimState) && animator.layerCount > 0)
        {
            int hash = Animator.StringToHash(Species.SwimState);
            if (!animator.HasState(0, hash))
                hash = Animator.StringToHash("Base Layer." + Species.SwimState);
            if (animator.HasState(0, hash))
            {
                animator.Play(hash, 0, Random.value);
                animator.Update(0f);
            }
        }
        if (hasSwimMultiplierParameter)
        {
            float animationReference = Species.SwimAnimationReferenceMetersPerSecond > 0f
                ? Species.SwimAnimationReferenceMetersPerSecond : Species.MaxSpeed;
            currentAnimationMultiplier = Mathf.Clamp(currentSpeed / Mathf.Max(.01f, animationReference),
                Species.MinSwimAnimationMultiplier, 1f);
            animator.SetFloat(swimParameterHash, currentAnimationMultiplier);
        }
    }

    // Debug triggers are intentionally delivered to one selected animal.
    // The animation clips still control their own particle effects via events.
    public bool TriggerDebugAnimation(bool breach)
    {
        if (!initialized || animator == null) return false;
        if (breach)
        {
            if (!hasBreachTrigger) return false;
            if (hasSurfaceTrigger) animator.ResetTrigger(surfaceTriggerHash);
            animator.SetTrigger(breachTriggerHash);
        }
        else
        {
            if (!hasSurfaceTrigger) return false;
            if (hasBreachTrigger) animator.ResetTrigger(breachTriggerHash);
            animator.SetTrigger(surfaceTriggerHash);
        }
        nextAnimation = Time.time + Random.Range(
            Mathf.Max(1f, Species.AnimationEventMinSeconds),
            Mathf.Max(Species.AnimationEventMinSeconds, Species.AnimationEventMaxSeconds));
        return true;
    }

    protected virtual void Update()
    {
        if (!initialized || Species == null) return;
        float now = Time.time;
        bool wakeEngaged = Pod != null && Pod.IsWakeEngaged;
        if (wakeEngaged && !wasWakeEngaged)
        {
            SeaLifePlugin plugin = SeaLifePlugin.Instance;
            float minimum = plugin != null ? plugin.WakeApproachExtraMinKnots : 5f;
            float maximum = plugin != null ? plugin.WakeApproachExtraMaxKnots : 10f;
            wakeApproachBonusKnots = Random.Range(minimum, maximum);
            wakeApproachBonusGoalKnots = wakeApproachBonusKnots;
            nextWakeApproachBonusChange = now + Random.Range(4f, 8f);
            wakePaceVariation = 0f;
            wakePaceVariationGoal = Random.Range(-.08f, .08f);
            nextWakePaceChange = now + Random.Range(3f, 7f);
        }
        if (!wakeEngaged && wasWakeEngaged)
        {
            // Solitary animals resume wandering from their most recent course.
            wanderHeading = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            nextWanderChange = now + Random.Range(10f, 30f);
        }
        wasWakeEngaged = wakeEngaged;
        if (wakeEngaged)
        {
            if (now >= nextWakePaceChange)
            {
                wakePaceVariationGoal = Random.Range(-.08f, .08f);
                nextWakePaceChange = now + Random.Range(3f, 7f);
            }
            // Small individual speed changes should be fluid, not step changes.
            wakePaceVariation = Mathf.MoveTowards(wakePaceVariation,
                wakePaceVariationGoal, .04f * Time.deltaTime);
            if (now >= nextWakeApproachBonusChange)
            {
                SeaLifePlugin plugin = SeaLifePlugin.Instance;
                if (plugin != null)
                    wakeApproachBonusGoalKnots = Random.Range(
                        plugin.WakeApproachExtraMinKnots, plugin.WakeApproachExtraMaxKnots);
                nextWakeApproachBonusChange = now + Random.Range(4f, 8f);
            }
            // Smoothly wander through the 5–10 knot catch-up range.
            wakeApproachBonusKnots = Mathf.MoveTowards(wakeApproachBonusKnots,
                wakeApproachBonusGoalKnots, 1.2f * Time.deltaTime);
            if (now >= nextWakeDriftChange)
            {
                wakeDriftGoal = new Vector2(Random.Range(-4.5f, 4.5f), Random.Range(-7f, 7f));
                nextWakeDriftChange = now + Random.Range(4f, 9f);
            }
            wakeDrift = Vector2.MoveTowards(wakeDrift, wakeDriftGoal,
                Mathf.Max(.1f, Species.WakeDriftRate) * Time.deltaTime);
        }
        if (now >= nextSteeringCheck)
        {
            nextSteeringCheck = now + .12f;
            if (Pod != null && (Pod.Members.Count > 1 || wakeEngaged ||
                MarinePod.HasNearbyCrossPodNeighbor(this)))
                desiredHeading = Pod.GetSteering(this);
            else desiredHeading = wanderHeading;
            if (now >= nextWanderChange && !wakeEngaged && (Pod == null || Pod.Members.Count <= 1))
            {
                wanderHeading = Quaternion.Euler(0, Random.Range(-75f, 75f), 0) * wanderHeading;
                nextWanderChange = now + Random.Range(10f, 30f);
                desiredHeading = wanderHeading.normalized;
            }
        }
        if (now >= nextNavigationCheck)
        {
            // Adaptive update: close to obstacles, scan more frequently.
            bool shipObstacle;
            bool hazard = FindSafeHeading(out avoidanceHeading, out shipObstacle);
            obstacleNearby = hazard;
            if (hazard) avoidingWakeShip = shipObstacle;
            if (hazard) avoidUntil = now + 1.5f;
            nextNavigationCheck = now + (hazard ? .10f : .35f) + Random.Range(0f, .04f);
        }
        Vector3 heading = now < avoidUntil ? avoidanceHeading : desiredHeading;
        heading.y = 0;
        if (heading.sqrMagnitude > .001f)
        {
            Quaternion goal = Quaternion.LookRotation(heading.normalized, Vector3.up);
            float oldYaw = transform.eulerAngles.y;
            if (Species.TurnAccelerationDegreesPerSecondSquared > 0f)
            {
                // Large animals cannot instantly attain maximum yaw rate.
                // Accelerate and decelerate through turns, including target changes.
                float dt = Mathf.Max(.0001f, Time.deltaTime);
                float error = Mathf.DeltaAngle(oldYaw, goal.eulerAngles.y);
                float accel = Species.TurnAccelerationDegreesPerSecondSquared;
                float maxYawRate = Mathf.Max(0f, Species.TurnDegreesPerSecond);
                // Begin braking early enough to arrive at the heading without
                // snapping through it. sqrt(2*a*d) is the stopping-speed bound.
                float stoppingRate = Mathf.Sqrt(2f * accel * Mathf.Abs(error));
                float requestedRate = Mathf.Sign(error) * Mathf.Min(maxYawRate, stoppingRate);
                yawVelocity = Mathf.MoveTowards(yawVelocity, requestedRate, accel * dt);
                // Never rotate farther than the remaining angular error.
                float yawStep = Mathf.Clamp(yawVelocity * dt,
                    -Mathf.Abs(error), Mathf.Abs(error));
                if (Mathf.Sign(yawStep) == Mathf.Sign(error) || Mathf.Abs(error) < .001f)
                    transform.rotation = Quaternion.Euler(0f, oldYaw + yawStep, 0f);
                else
                    yawVelocity = Mathf.MoveTowards(yawVelocity, 0f, accel * dt);
            }
            else
            {
                // Preserve the existing response for Orcas and other species.
                transform.rotation = Quaternion.RotateTowards(transform.rotation, goal,
                    Species.TurnDegreesPerSecond * Time.deltaTime);
            }
            turnRate = Mathf.DeltaAngle(oldYaw, transform.eulerAngles.y)
                / Mathf.Max(.001f, Time.deltaTime);
        }
        // Momentum-aware, continuous swimming. There is intentionally no hard
        // collision brake: predictive avoidance should do the work, while a
        // genuine interception can still happen without freezing the animal.
        if (now >= nextSpeedChange)
        {
            targetSpeed = Random.Range(Species.MinSpeed, Species.MaxSpeed);
            nextSpeedChange = now + Random.Range(
                Mathf.Max(1f, Species.SpeedChangeMinSeconds),
                Mathf.Max(Species.SpeedChangeMinSeconds, Species.SpeedChangeMaxSeconds));
        }
        // Random cruise jitter is intentionally independent from the slow
        // 18–40 second changes to base cruising speed. Fin Whales remain as-is.
        if (Species.CruiseSpeedJitterFraction > 0f)
        {
            if (now >= nextCruiseJitterChange)
            {
                cruiseJitterGoal = Random.Range(-Species.CruiseSpeedJitterFraction,
                    Species.CruiseSpeedJitterFraction);
                nextCruiseJitterChange = now + Random.Range(4f, 9f);
            }
            cruiseJitter = Mathf.MoveTowards(cruiseJitter, cruiseJitterGoal,
                .025f * Time.deltaTime);
        }
        float requestedSpeed = Mathf.Clamp(targetSpeed * (1f + cruiseJitter),
            Species.MinSpeed, Species.MaxSpeed);
        float wakeSpeed;
        if (Pod != null && Pod.TryGetWakeSpeed(this, out wakeSpeed)) requestedSpeed = wakeSpeed;
        // Keep the existing terrain slowdown. Close to the tracked vessel,
        // however, steering takes priority without forcing a pursuing Orca
        // back to its normal 8 m/s cruising limit.
        bool hazardActive = obstacleNearby || now < avoidUntil;
        if (hazardActive && !(wakeEngaged && avoidingWakeShip))
            requestedSpeed = Mathf.Min(requestedSpeed,
                Mathf.Max(.25f, Species.MaxSpeed * Species.HazardSpeedFraction));
        // A pursuing Orca can approach a slower podmate far faster than ordinary
        // Boid steering can correct. Smoothly match its speed while passing.
        // The speed limit is predictive; it never teleports or freezes movement.
        float podmateLimit = 0f;
        bool followingPodmate = Pod != null && Pod.TryGetPodmateSpeedLimit(this, out podmateLimit);
        if (followingPodmate) requestedSpeed = Mathf.Min(requestedSpeed, podmateLimit);
        float acceleration = wakeEngaged && Species.WakeAcceleration > 0f
            ? Species.WakeAcceleration : Species.SwimAcceleration;
        float deceleration = wakeEngaged && Species.WakeDeceleration > 0f
            ? Species.WakeDeceleration : Species.SwimDeceleration;
        if (followingPodmate)
            deceleration = Mathf.Max(deceleration, Species.PodFollowingDeceleration);
        float speedDelta = requestedSpeed > currentSpeed ? acceleration : deceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, requestedSpeed,
            Mathf.Max(0.01f, speedDelta) * Time.deltaTime);
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 travelDirection = Vector3.ProjectOnPlane(velocity, Vector3.up).normalized;
        if (travelDirection.sqrMagnitude < .0001f) travelDirection = forward;
        travelDirection = Vector3.RotateTowards(travelDirection, forward,
            Mathf.Deg2Rad * Mathf.Max(.01f, Species.VelocityTurnDegreesPerSecond) * Time.deltaTime,
            0f).normalized;
        velocity = travelDirection * currentSpeed;
        transform.position += velocity * Time.deltaTime;
        AdjustHeightToWave();
        if (animator != null)
        {
            if (hasSwimMultiplierParameter)
            {
                float animationReference = Species.SwimAnimationReferenceMetersPerSecond > 0f
                    ? Species.SwimAnimationReferenceMetersPerSecond : Species.MaxSpeed;
                float desiredMultiplier = Mathf.Clamp(currentSpeed / Mathf.Max(.01f, animationReference),
                    Species.MinSwimAnimationMultiplier, 1f);
                currentAnimationMultiplier = Mathf.MoveTowards(currentAnimationMultiplier,
                    desiredMultiplier, Time.deltaTime * .7f);
                animator.SetFloat(swimParameterHash, currentAnimationMultiplier);
            }
            if ((Species.HasFinWhaleAnimations || Species.OrcaAppearanceAndAnimations) && hasBendParameter)
            {
                // Drive the shared left/right blend tree from actual rotation,
                // not target heading. Per-species tuning keeps other behavior intact.
                float referenceRate = Mathf.Max(.01f, Species.TurnAnimationReferenceDegreesPerSecond);
                float bendTarget = Mathf.Clamp(turnRate / referenceRate, -1f, 1f);
                bendAmount = Mathf.Lerp(bendAmount, bendTarget,
                    Time.deltaTime * Mathf.Max(0f, Species.TurnAnimationSmoothing));
                animator.SetFloat(bendParameterHash, bendAmount);
            }
            if ((Species.HasFinWhaleAnimations || Species.OrcaAppearanceAndAnimations) && now >= nextAnimation)
            {
                if (hasBreachTrigger && hasSurfaceTrigger)
                    animator.SetTrigger(Random.Range(0, 2) == 0 ? surfaceTriggerHash : breachTriggerHash);
                else if (hasSurfaceTrigger) animator.SetTrigger(surfaceTriggerHash);
                else if (hasBreachTrigger) animator.SetTrigger(breachTriggerHash);
                nextAnimation = now + Random.Range(
                    Mathf.Max(1f, Species.AnimationEventMinSeconds),
                    Mathf.Max(Species.AnimationEventMinSeconds, Species.AnimationEventMaxSeconds));
            }
        }
        if (player != null && now >= nextDespawnCheck)
        {
            nextDespawnCheck = now + 1f;
            Vector3 delta = player.position - transform.position;
            delta.y = 0;
            if (delta.sqrMagnitude > Species.DespawnRadius * Species.DespawnRadius) Destroy(gameObject);
        }
    }

    private bool FindSafeHeading(out Vector3 safeHeading, out bool isWakeShip)
    {
        isWakeShip = false;
        safeHeading = transform.forward;
        safeHeading.y = 0;
        safeHeading.Normalize();
        float radius = Mathf.Max(.5f, Species.AvoidanceRadius * Mathf.Abs(transform.lossyScale.x));
        float lookAhead = Mathf.Max(Species.LookAhead, currentSpeed * 9f);
        RaycastHit hit;
        // Start ahead of our own body; ignore trigger colliders (including marine animals).
        Vector3 origin = transform.position + safeHeading * Mathf.Max(1f, radius * .3f);
        if (!Physics.SphereCast(origin, radius, safeHeading, out hit, lookAhead,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;

        // Detect ship geometry separately from shore so the hull's avoidance
        // rays don't continually impose the normal-cruise speed cap at 18+ kn.
        if (Pod != null && Pod.IsWakeEngaged && hit.collider != null)
        {
            ShipWakeData ship;
            SeaLifePlugin plugin = SeaLifePlugin.Instance;
            if (plugin != null && plugin.TryGetShipWake(out ship) && ship.Body != null)
                isWakeShip = hit.collider.attachedRigidbody == ship.Body;
        }

        // Sample alternate paths, prefer clearance while discouraging sharp turns.
        Vector3 best = -safeHeading;
        float bestScore = -float.MaxValue;
        for (int i = 1; i < candidateAngles.Length; i++)
        {
            Vector3 candidate = Quaternion.Euler(0, candidateAngles[i], 0) * safeHeading;
            RaycastHit sideHit;
            bool blocked = Physics.SphereCast(origin, radius, candidate, out sideHit, lookAhead,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float clearDistance = blocked ? sideHit.distance : lookAhead;
            float score = clearDistance - Mathf.Abs(candidateAngles[i]) * .08f;
            // Maintain the previously chosen side to avoid flip-flopping on ragged coastlines.
            if (Time.time < avoidUntil) score += Vector3.Dot(candidate, avoidanceHeading) * lookAhead * .3f;
            if (score > bestScore) { bestScore = score; best = candidate; }
        }
        safeHeading = best;
        return true;
    }

    private void AdjustHeightToWave()
    {
        if (OceanRenderer.Instance == null || OceanRenderer.Instance.CollisionProvider == null) return;
        queryPoints[0] = transform.position;
        int status = OceanRenderer.Instance.CollisionProvider.Query(GetHashCode(), 1f, queryPoints, resultDisps, null, null);
        if (OceanRenderer.Instance.CollisionProvider.RetrieveSucceeded(status))
        {
            float height = OceanRenderer.Instance.SeaLevel + resultDisps[0].y + Species.SurfaceOffset;
            Vector3 p = transform.position;
            p.y = Mathf.Lerp(p.y, height, Time.deltaTime * 2.5f);
            transform.position = p;
        }
    }
}
